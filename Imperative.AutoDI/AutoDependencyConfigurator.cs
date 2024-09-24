using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace Imperative.AutoDI
{
    file static class StringExtensions
    {
        public static bool StartsWithAny(this string value, params string[] matches)
        {
            if (value == null) return false;
            if (matches == null || matches.Length == 0) return false;
            
            foreach (var match in matches)
            {
                if (value.StartsWith(match))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal class AutoDependencyConfigurator : IAutoDependencyConfigurator
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly ILogger<AutoDependencyConfigurator> _logger;
        private readonly IReadOnlyDictionary<string, List<Type>> _typesByNamespace;
        // Has to stay List to get BinarySearch
        private readonly List<string> _alphabetizedKeys;
        // The framework assembly name prefixes to typically exclude
        private readonly string[] _frameworkAssemblyNamePrefixes = ["Microsoft.", "System."];

        public AutoDependencyConfigurator(IServiceCollection serviceCollection, ILoggerFactory loggerFactory = null, bool includeFrameworkAssemblies = false)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            _serviceCollection = serviceCollection;

            // Set up logging
            if (loggerFactory == null)
            {
                _logger = NullLogger<AutoDependencyConfigurator>.Instance;
            }
            else
            {
                _logger = loggerFactory.CreateLogger<AutoDependencyConfigurator>();
            }

            _logger.LogInformation("[AutoDI]: Init - {IncludingOrSkipping} framework assemblies", includeFrameworkAssemblies ? "including" : "skipping");

            // Get all types and store them by namespace for "fast" access
            var timer = Stopwatch.StartNew();
            var typesByNamespace = new Dictionary<string, List<Type>>();

            // Load the current domain assemblies (which don't include referenced project assemblies) and the referenced assemblies (which need to be manually loaded)
            var currentDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var referencedAssemblyNames = Assembly.GetEntryAssembly().GetReferencedAssemblies();
            // The loaded referenced assemblies
            var referencedAssemblies = new List<Assembly>(referencedAssemblyNames.Length);
            foreach (var referencedAssemblyName in referencedAssemblyNames)
            {
                if (!includeFrameworkAssemblies && referencedAssemblyName.FullName.StartsWithAny(_frameworkAssemblyNamePrefixes))
                {
                    // Skip it
                    continue;
                }

                referencedAssemblies.Add(Assembly.Load(referencedAssemblyName));
            }
            
            // Now marry the two sets of assemblies
            var assemblies = new HashSet<Assembly>();
            foreach (var assembly in currentDomainAssemblies.Union(referencedAssemblies))
            {
                if (!includeFrameworkAssemblies && assembly.FullName.StartsWithAny(_frameworkAssemblyNamePrefixes))
                {
                    // Skip it
                    continue;
                }

                assemblies.Add(assembly);
            }

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

                        // Only support constructed generic types (those whose type parameters are all defined)
                        // See: https://stackoverflow.com/a/70111165/2420979
                        if (type.IsGenericType && !type.IsConstructedGenericType)
                        {
                            continue;
                        }

                        if (!typesByNamespace.TryGetValue(type.Namespace, out var types))
                        {
                            types = [];
                            typesByNamespace.Add(type.Namespace, types);
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

            // Store as readonly
            _typesByNamespace = typesByNamespace;

            // Store the keys ordered alphabetically so we can do BinarySearch for wildcards
            _alphabetizedKeys = _typesByNamespace.Keys.OrderBy(i => i).ToList();

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

        public IAutoDependencyConfigurator AddSingletons(params Type[] types)
        {
            ArgumentNullException.ThrowIfNull(types);

            AddTypes(types, _serviceCollection.AddSingleton, nameof(AddSingletons));

            return this;
        }

        public IAutoDependencyConfigurator AddScopeds(params string[] namespaces)
        {
            ArgumentNullException.ThrowIfNull(namespaces);

            AddTypes(namespaces, _serviceCollection.AddScoped, nameof(AddScopeds));

            return this;
        }

        public IAutoDependencyConfigurator AddScopeds(params Type[] types)
        {
            ArgumentNullException.ThrowIfNull(types);

            AddTypes(types, _serviceCollection.AddScoped, nameof(AddScopeds));

            return this;
        }

        public IAutoDependencyConfigurator AddTransients(params string[] namespaces)
        {
            ArgumentNullException.ThrowIfNull(namespaces);

            AddTypes(namespaces, _serviceCollection.AddTransient, nameof(AddTransients));

            return this;
        }

        public IAutoDependencyConfigurator AddTransients(params Type[] types)
        {
            ArgumentNullException.ThrowIfNull(types);

            AddTypes(types, _serviceCollection.AddTransient, nameof(AddTransients));

            return this;
        }

        private void AddTypes(string[] namespaces, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            ArgumentNullException.ThrowIfNull(namespaces);
            ArgumentNullException.ThrowIfNull(addMethod);
            ArgumentException.ThrowIfNullOrWhiteSpace(methodNameForDebugLogging);

            var types = new List<Type>(100 * (namespaces.Length + 1));

            // Aggregate all types from all namespaces
            foreach (var @namespace in namespaces)
            {
                List<Type> typesInNamespace;
                // Handle wildcard namespaces which map all child namespaces
                if (@namespace.EndsWith('*'))
                {
                    typesInNamespace = new List<Type>(100);

                    var nameSpaceRoot = @namespace.TrimEnd('*');
                    // Find where this root would be in the ordered list
                    var index = _alphabetizedKeys.BinarySearch(nameSpaceRoot);
                    if (index < 0)
                    {
                        // This is the index of next larger (in the alphabet) namespace in the list, because the exact match wasn't found
                        index = ~index;
                    }

                    // iterate the ordered keys until our wildcard no longer matches
                    while (index < _alphabetizedKeys.Count)
                    {
                        var currentKey = _alphabetizedKeys[index];
                        // If we have moved past the root in terms of alphabetized namespaces, we're done
                        if (!currentKey.StartsWith(nameSpaceRoot))
                        {
                            break;
                        }

                        typesInNamespace.AddRange(_typesByNamespace[currentKey]);
                        index++;
                    }

                    if (typesInNamespace.Count == 0)
                    {
                        _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no types found in wildcard namespace: {namespace}", methodNameForDebugLogging, @namespace);
                        continue;
                    }
                }
                else if (!_typesByNamespace.TryGetValue(@namespace, out typesInNamespace))
                {
                    _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no types found in namespace: {namespace}", methodNameForDebugLogging, @namespace);
                    continue;
                }

                // Add to types
                types.AddRange(typesInNamespace);
            }

            // Now register types
            var serviceTypes = types.Where(i => i.IsInterface || i.IsAbstract).ToList();
            var concreteTypes = types.Where(i => !i.IsInterface && !i.IsAbstract).ToList();

            if (serviceTypes.Count == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no service types found in any of the provided namespaces", methodNameForDebugLogging);
                return;
            }
            if (concreteTypes.Count == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - no concrete types found in any of the provided namespaces", methodNameForDebugLogging);
                return;
            }

            // Order concrete types by name, because when multiple implementations can be assigned from the interface or abstract clas, we'll take the first one
            concreteTypes = concreteTypes.OrderBy(i => i.Name).ToList();

            // For each service type, register a concrete type
            foreach (var serviceType in serviceTypes)
            {
                foreach (var concreteType in concreteTypes)
                {
                    if (!serviceType.IsAssignableFrom(concreteType))
                    {
                        // Can't be assigned
                        continue;
                    }

                    // Register the type mapping
                    addMethod(serviceType, concreteType);

                    _logger.LogDebug("[AutoDI]: {methodNameForDebugLogging} - mapped '{serviceType}' to '{concreteType}'", methodNameForDebugLogging, serviceType, concreteType);
                        
                    break;
                }
            }
        }

        private void AddTypes(Type[] types, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            ArgumentNullException.ThrowIfNull(types);
            ArgumentNullException.ThrowIfNull(addMethod);
            ArgumentException.ThrowIfNullOrWhiteSpace(methodNameForDebugLogging);

            if (types.Length == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} - {types} collection has 0 elements", methodNameForDebugLogging, nameof(types));
                return;
            }

            // Get the service and implementation types
            var serviceTypes = types.Where(i => i.IsInterface || i.IsAbstract);
            // Alphabetize the implementation types, we'll take the first one alphabetically that can be assigned from a given service type
            var implementationTypes = types.Where(i => !i.IsInterface && !i.IsAbstract).OrderBy(i => i.Name);

            // For each service type, register a concrete type
            foreach (var serviceType in serviceTypes)
            {
                // Ensure an implementation can be assigned from the interface - take the first one that qualifies alphabetically
                foreach (var implementationType in implementationTypes)
                {
                    if (!serviceType.IsAssignableFrom(implementationType))
                    {
                        // Can't be assigned
                        continue;
                    }

                    // Register the type mapping
                    addMethod(serviceType, implementationType);

                    _logger.LogDebug("[AutoDI]: {methodNameForDebugLogging} - mapped {serviceType} to {concreteType}", methodNameForDebugLogging, serviceType, implementationType);

                    break;
                }
            }
        }
    }
}
