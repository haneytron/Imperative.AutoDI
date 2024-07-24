using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

namespace Imperative.AutoDI
{
    internal class AutoDependencyConfigurator : IAutoDependencyConfigurator
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly ILogger<AutoDependencyConfigurator> _logger;
        private readonly Dictionary<string, List<Type>> _typesByNamespace;

        public AutoDependencyConfigurator(IServiceCollection serviceCollection, ILoggerFactory loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            _serviceCollection = serviceCollection;

            // Set up logging
            if (loggerFactory == null)
            {
                loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Debug);
                    builder.AddConsole();
                });
            }

            _logger = loggerFactory.CreateLogger<AutoDependencyConfigurator>();

            // Get all types and store them by namespace for fast access
            var timer = Stopwatch.StartNew();
            _typesByNamespace = [];
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var typeCount = 0;

            foreach (var assembly in assemblies)
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var type in assemblyTypes)
                    {
                        // Skip null and empty namespaces
                        if (string.IsNullOrWhiteSpace(type.Namespace))
                        {
                            continue;
                        }

                        // Skip generics
                        // TODO: create support for generics
                        if (type.IsGenericType || type.IsGenericTypeDefinition)
                        {
                            continue;
                        }

                        if (!_typesByNamespace.TryGetValue(type.Namespace, out var types))
                        {
                            types = [];
                            _typesByNamespace.Add(type.Namespace, types);
                        }

                        types.Add(type);
                        typeCount++;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.LogWarning("[AutoDI]: Init - error caught while caching types by namespace: {ex}", ex);
                }
            }

            timer.Stop();
            _logger.LogInformation("[AutoDI]: Init - cached types in {ellapsed}ms, total namespaces: {typesByNamespaceCount}, total types: {typeCount}", timer.ElapsedMilliseconds, _typesByNamespace.Count, typeCount);
            Console.WriteLine();
        }

        public IAutoDependencyConfigurator AddSingletons(params string[] namespaces)
        {
            ArgumentNullException.ThrowIfNull(namespaces);

            AddTypes(namespaces, _serviceCollection.AddSingleton, nameof(AddSingletons));

            return this;
        }

        public IAutoDependencyConfigurator AddSingletons(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector)
        {
            ArgumentNullException.ThrowIfNull(servicesTypesSelector);
            ArgumentNullException.ThrowIfNull(implementationTypesSelector);

            AddTypes(servicesTypesSelector, implementationTypesSelector, _serviceCollection.AddSingleton, nameof(AddSingletons));

            return this;
        }

        public IAutoDependencyConfigurator AddScopeds(params string[] namespaces)
        {
            ArgumentNullException.ThrowIfNull(namespaces);

            AddTypes(namespaces, _serviceCollection.AddScoped, nameof(AddScopeds));

            return this;
        }

        public IAutoDependencyConfigurator AddScopeds(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector)
        {
            ArgumentNullException.ThrowIfNull(servicesTypesSelector);
            ArgumentNullException.ThrowIfNull(implementationTypesSelector);

            AddTypes(servicesTypesSelector, implementationTypesSelector, _serviceCollection.AddScoped, nameof(AddScopeds));

            return this;
        }

        public IAutoDependencyConfigurator AddTransients(params string[] namespaces)
        {
            ArgumentNullException.ThrowIfNull(namespaces);

            AddTypes(namespaces, _serviceCollection.AddTransient, nameof(AddTransients));

            return this;
        }

        public IAutoDependencyConfigurator AddTransients(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector)
        {
            ArgumentNullException.ThrowIfNull(servicesTypesSelector);
            ArgumentNullException.ThrowIfNull(implementationTypesSelector);

            AddTypes(servicesTypesSelector, implementationTypesSelector, _serviceCollection.AddTransient, nameof(AddTransients));

            return this;
        }

        private void AddTypes(string[] namespaces, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            ArgumentNullException.ThrowIfNull(namespaces);
            ArgumentNullException.ThrowIfNull(addMethod);
            if (string.IsNullOrWhiteSpace(methodNameForDebugLogging)) throw new ArgumentException("cannot be null, empty, or white space", nameof(methodNameForDebugLogging));

            foreach (var @namespace in namespaces)
            {
                List<Type> typesInNamespace;

                // Handle wildcard namespaces which map all child namespaces
                if (@namespace.EndsWith('*'))
                {
                    typesInNamespace = _typesByNamespace.Where(i => i.Key.StartsWith(@namespace.TrimEnd('*'))).SelectMany(i => i.Value).ToList();
                    if (typesInNamespace.Count == 0)
                    {
                        _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no types found for namespace: {namespace}", methodNameForDebugLogging, @namespace);
                        continue;
                    }
                }
                else if (!_typesByNamespace.TryGetValue(@namespace, out typesInNamespace))
                {
                    _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no types found for namespace: {namespace}", methodNameForDebugLogging, @namespace);
                    continue;
                }

                var serviceTypes = typesInNamespace.Where(i => i.IsInterface || i.IsAbstract).ToList();
                var concreteTypes = typesInNamespace.Where(i => !i.IsInterface && !i.IsAbstract).ToList();

                if (serviceTypes.Count == 0)
                {
                    _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no service types found for namespace: {namespace}", methodNameForDebugLogging, @namespace);
                    continue;
                }
                if (concreteTypes.Count == 0)
                {
                    _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no concrete types found for namespace: {namespace}", methodNameForDebugLogging, @namespace);
                    continue;
                }

                // For each service type, register a concrete type
                foreach (var serviceType in serviceTypes)
                {
                    // Ensure an implementation can be assigned from the interface - take the first one that qualifies alphabetically
                    foreach (var concreteType in concreteTypes.OrderBy(i => i.Name))
                    {
                        if (!serviceType.IsAssignableFrom(concreteType))
                        {
                            // Can't be assigned
                            continue;
                        }

                        // Register the type mapping
                        addMethod(serviceType, concreteType);

                        _logger.LogDebug("[AutoDI]: {methodNameForDebugLogging} - mapped {serviceType} to {concreteType}", methodNameForDebugLogging, serviceType, concreteType);
                        
                        break;
                    }
                }
            }
        }

        private void AddTypes(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            ArgumentNullException.ThrowIfNull(servicesTypesSelector);
            ArgumentNullException.ThrowIfNull(implementationTypesSelector);
            ArgumentNullException.ThrowIfNull(addMethod);
            if (string.IsNullOrWhiteSpace(methodNameForDebugLogging)) throw new ArgumentException("cannot be null, empty, or white space", nameof(methodNameForDebugLogging));

            var serviceTypes = (servicesTypesSelector() ?? Enumerable.Empty<Type>()).ToList();
            var concreteTypes = (implementationTypesSelector() ?? Enumerable.Empty<Type>()).ToList();

            if (serviceTypes.Count == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no service types found for {servicesTypesSelector}", methodNameForDebugLogging, nameof(servicesTypesSelector));
                return;
            }
            if (concreteTypes.Count == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no concrete types found for {implementationTypesSelector}", methodNameForDebugLogging, nameof(implementationTypesSelector));
                return;
            }

            // For each service type, register a concrete type
            foreach (var serviceType in serviceTypes)
            {
                // Ensure an implementation can be assigned from the interface - take the first one that qualifies alphabetically
                foreach (var concreteType in concreteTypes.OrderBy(i => i.Name))
                {
                    if (!serviceType.IsAssignableFrom(concreteType))
                    {
                        // Can't be assigned
                        continue;
                    }

                    // Register the type mapping
                    addMethod(serviceType, concreteType);

                    _logger.LogDebug("[AutoDI]: {methodNameForDebugLogging} - mapped {serviceType} to {concreteType}", methodNameForDebugLogging, serviceType, concreteType);

                    break;
                }
            }
        }
    }
}
