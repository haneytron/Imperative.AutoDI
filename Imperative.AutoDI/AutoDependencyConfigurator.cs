using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Imperative.AutoDI
{
    internal static class StringExtensions
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
        // The most common framework assembly name prefixes to typically exclude
        private readonly string[] _frameworkAssemblyNamePrefixes = new[] { "Microsoft.", "System." };

        public AutoDependencyConfigurator(IServiceCollection serviceCollection, ILoggerFactory loggerFactory = null, bool includeFrameworkAssemblies = false)
        {
            _serviceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));

            // Set up logging
            if (loggerFactory == null)
            {
                _logger = NullLogger<AutoDependencyConfigurator>.Instance;
            }
            else
            {
                _logger = loggerFactory.CreateLogger<AutoDependencyConfigurator>();
            }

            _logger.LogInformation(@"[AutoDI]: Init Started |
  {IncludingOrSkipping} framework assemblies", includeFrameworkAssemblies ? "including" : "skipping");

            // Get all types and store them by namespace for "fast" access
            var timer = Stopwatch.StartNew();
            var typesByNamespace = new Dictionary<string, List<Type>>(500);

            // The total set of assemblies
            var assemblies = new HashSet<Assembly>(50);

            // Collect the current domain assemblies (which don't include referenced project assemblies)
            var currentDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var currentDomainAssembly in currentDomainAssemblies)
            {
                if (!includeFrameworkAssemblies && currentDomainAssembly.FullName.StartsWithAny(_frameworkAssemblyNamePrefixes))
                {
                    // Skip it
                    continue;
                }

                assemblies.Add(currentDomainAssembly);
            }

            // Now load the referenced assemblies (which need to be manually loaded)
            var referencedAssemblyNames = Assembly.GetEntryAssembly().GetReferencedAssemblies();
            foreach (var referencedAssemblyName in referencedAssemblyNames)
            {
                if (!includeFrameworkAssemblies && referencedAssemblyName.FullName.StartsWithAny(_frameworkAssemblyNamePrefixes))
                {
                    // Skip it
                    continue;
                }

                assemblies.Add(Assembly.Load(referencedAssemblyName));
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
                            types = new List<Type>(20);
                            typesByNamespace.Add(type.Namespace, types);
                        }

                        types.Add(type);
                        typeCount++;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.LogWarning("[AutoDI]: Init Error |\n  error caught while caching types by namespace: {ex}", ex);
                }
            }

            // Store as readonly
            _typesByNamespace = typesByNamespace;

            // Store the keys ordered alphabetically so we can do BinarySearch for wildcards
            _alphabetizedKeys = _typesByNamespace.Keys.OrderBy(i => i).ToList();

            timer.Stop();
            _logger.LogInformation(@"[AutoDI]: Init Completed |
  cached all types in     {ellapsed} ms
  total assemblies:       {assembliesCount}
  total namespaces:       {typesByNamespaceCount}
  total types:            {typeCount}", timer.ElapsedMilliseconds, assemblies.Count, _typesByNamespace.Count, typeCount);
            Console.WriteLine();
        }

        public IAutoDependencyConfigurator AddSingletons(params string[] namespaces)
        {
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            if (namespaces.Length == 0) throw new ArgumentException("must provide at least one namespace", nameof(namespaces));

            AddTypes(namespaces, _serviceCollection.AddSingleton, nameof(AddSingletons));

            return this;
        }

        public IAutoDependencyConfigurator AddSingletons(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length == 0) throw new ArgumentException("must provide at least one type", nameof(types));

            AddTypes(types, _serviceCollection.AddSingleton, nameof(AddSingletons));

            return this;
        }

        public IAutoDependencyConfigurator AddScopeds(params string[] namespaces)
        {
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            if (namespaces.Length == 0) throw new ArgumentException("must provide at least one namespace", nameof(namespaces));

            AddTypes(namespaces, _serviceCollection.AddScoped, nameof(AddScopeds));

            return this;
        }

        public IAutoDependencyConfigurator AddScopeds(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length == 0) throw new ArgumentException("must provide at least one type", nameof(types));

            AddTypes(types, _serviceCollection.AddScoped, nameof(AddScopeds));

            return this;
        }

        public IAutoDependencyConfigurator AddTransients(params string[] namespaces)
        {
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            if (namespaces.Length == 0) throw new ArgumentException("must provide at least one namespace", nameof(namespaces));

            AddTypes(namespaces, _serviceCollection.AddTransient, nameof(AddTransients));

            return this;
        }

        public IAutoDependencyConfigurator AddTransients(params Type[] types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length == 0) throw new ArgumentException("must provide at least one type", nameof(types));

            AddTypes(types, _serviceCollection.AddTransient, nameof(AddTransients));

            return this;
        }

        private void AddTypes(string[] namespaces, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            if (namespaces.Length == 0) throw new ArgumentException("must provide at least one namespace", nameof(namespaces));
            if (addMethod == null) throw new ArgumentNullException(nameof(addMethod));
            if (string.IsNullOrWhiteSpace(methodNameForDebugLogging)) throw new ArgumentException("cannot be null, empty, or whitespace", nameof(methodNameForDebugLogging));

            var types = new List<Type>(20);

            // Aggregate all types from all namespaces
            foreach (var @namespace in namespaces)
            {
                List<Type> typesInNamespace;
                // Handle wildcard namespaces which inclusively map all child namespaces
                if (@namespace.EndsWith('*'))
                {
                    typesInNamespace = new List<Type>(20);

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
                        _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} |\n  no types found in wildcard namespace: {namespace}", methodNameForDebugLogging, @namespace);
                        continue;
                    }
                }
                else if (!_typesByNamespace.TryGetValue(@namespace, out typesInNamespace))
                {
                    _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} |\n  no types found in namespace: {namespace}", methodNameForDebugLogging, @namespace);
                    continue;
                }

                // Add to types
                types.AddRange(typesInNamespace);
            }

            // Now register types
            AddTypes(types.ToArray(), addMethod, methodNameForDebugLogging);
        }

        private void AddTypes(Type[] types, Func<Type, Type, IServiceCollection> addMethod, string methodNameForDebugLogging)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length == 0) throw new ArgumentException("must provide at least one type", nameof(types));
            if (addMethod == null) throw new ArgumentNullException(nameof(addMethod));
            if (string.IsNullOrWhiteSpace(methodNameForDebugLogging)) throw new ArgumentException("cannot be null, empty, or whitespace", nameof(methodNameForDebugLogging));

            if (types.Length == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} |\n  parameter '{types}' has 0 elements", methodNameForDebugLogging, nameof(types));
                return;
            }

            // Get the service and concrete types
            var serviceTypes = types.Where(i => i.IsInterface || i.IsAbstract).ToList();
            var concreteTypes = types.Where(i => !i.IsInterface && !i.IsAbstract).ToList();

            if (serviceTypes.Count == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} |\n  no service types found in any of the provided namespaces", methodNameForDebugLogging);
                return;
            }
            if (concreteTypes.Count == 0)
            {
                _logger.LogWarning("[AutoDI]: {methodNameForDebugLogging} |\n  no concrete types found in any of the provided namespaces", methodNameForDebugLogging);
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

                    _logger.LogDebug("[AutoDI]: {methodNameForDebugLogging} |\n  mapped '{serviceType}' to '{concreteType}'", methodNameForDebugLogging, serviceType, concreteType);

                    break;
                }
            }
        }
    }
}
