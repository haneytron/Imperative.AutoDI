using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;

namespace Imperative.AutoDI
{
    internal class AutoDependencyConfigurator : IAutoDependencyConfigurator
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly Dictionary<string, List<Type>> _typesByNamespace;

        public AutoDependencyConfigurator(IServiceCollection serviceCollection)
        {
            _serviceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));

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
                    Debug.WriteLine($"*** [AutoDI]: Init - error caught while caching types by namespace: {ex}");
                }
            }

            timer.Stop();
            Debug.WriteLine($"*** [AutoDI]: Init - caching types by namespace done: {timer.ElapsedMilliseconds}ms");
            Debug.WriteLine($"*** [AutoDI]: Init - total namespaces: {_typesByNamespace.Count}, total types: {typeCount}");
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
                if (!_typesByNamespace.TryGetValue(@namespace, out var typesInNamespace))
                {
                    Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - no types found for namespace: {@namespace}");
                    continue;
                }

                var serviceTypes = typesInNamespace.Where(i => i.IsInterface || i.IsAbstract).ToList();
                var concreteTypes = typesInNamespace.Where(i => !i.IsInterface && !i.IsAbstract).ToList();

                if (serviceTypes.Count == 0)
                {
                    Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - no service types found for namespace: {@namespace}");
                    continue;
                }
                if (concreteTypes.Count == 0)
                {
                    Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - no concrete types found for namespace: {@namespace}");
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
                        Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - mapped {serviceType} to {concreteType}");
                        break;
                    }
                }
            }
        }

        private static void AddTypes(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            ArgumentNullException.ThrowIfNull(servicesTypesSelector);
            ArgumentNullException.ThrowIfNull(implementationTypesSelector);
            ArgumentNullException.ThrowIfNull(addMethod);
            if (string.IsNullOrWhiteSpace(methodNameForDebugLogging)) throw new ArgumentException("cannot be null, empty, or white space", nameof(methodNameForDebugLogging));

            var serviceTypes = servicesTypesSelector().ToList();
            var concreteTypes = implementationTypesSelector().ToList();

            if (serviceTypes.Count == 0)
            {
                Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - no service types found for {nameof(servicesTypesSelector)}");
                return;
            }
            if (concreteTypes.Count == 0)
            {
                Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - no concrete types found for {nameof(implementationTypesSelector)}");
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
                    Debug.WriteLine($"*** [AutoDI]: {methodNameForDebugLogging} - mapped {serviceType} to {concreteType}");
                    break;
                }
            }
        }
    }
}
