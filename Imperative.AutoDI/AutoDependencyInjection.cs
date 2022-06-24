using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;

namespace Imperative.AutoDI
{
    public static class AutoDependencyInjection
    {
        public static void ConfigureDependencies(IServiceCollection services, Scope scope, params string[] namespaces)
        {
            var timer = Stopwatch.StartNew();
            var currentNamespace = typeof(AutoDependencyInjection).Namespace;

            // Get all types and store them by namespace for fast access
            var configurationNamespaces = namespaces;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(i => configurationNamespaces.Any(x => x.StartsWith(i.GetName().Name)));
            var typesByNamespace = new Dictionary<string, List<Type>>();
            List<Type> types = null;

            foreach (var assembly in assemblies)
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (Type type in assemblyTypes)
                    {
                        // Skip null namespaces
                        if (type.Namespace == null)
                        {
                            continue;
                        }

                        // Skip generics
                        if (type.IsGenericType || type.IsGenericTypeDefinition)
                        {
                            continue;
                        }

                        if (!typesByNamespace.TryGetValue(type.Namespace, out types))
                        {
                            types = new List<Type>();
                            typesByNamespace.Add(type.Namespace, types);
                        }

                        types.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.WriteLine($"*** {currentNamespace}: Error Caught: {ex}");
                }
            }

            // Get all interfaces or abstract classes in the specified namespaces
            foreach (var typesByNamespaceKvp in typesByNamespace)
            {
                // Get the qualifying namespace types
                if (!configurationNamespaces.Any(i => typesByNamespaceKvp.Key.StartsWith(i)))
                {
                    // Skip
                    continue;
                }

                foreach (var namespaceType in typesByNamespaceKvp.Value)
                {
                    // Skip non interfaces and non-abstract classes
                    if (!namespaceType.IsInterface && !namespaceType.IsAbstract)
                    {
                        continue;
                    }

                    // See if an instance exists that ends with the same name without the leading I, for example: DapperDataSource would match for IDataSource
                    var implementationSuffix = namespaceType.Name.Substring(1);
                    var implementations = typesByNamespaceKvp.Value.Where(i => !string.Equals(i.AssemblyQualifiedName, namespaceType.AssemblyQualifiedName) && i.Name.EndsWith(implementationSuffix));
                    if (!implementations.Any())
                    {
                        // Nope
                        continue;
                    }

                    // Ensure an implementation can be assigned from the interface - take the first one that qualifies
                    foreach (var implementation in implementations)
                    {
                        if (!namespaceType.IsAssignableFrom(implementation))
                        {
                            // Can't be assigned
                            continue;
                        }

                        // Register the type mapping
                        var _ = scope switch
                        {
                            Scope.Singleton => services.AddSingleton(namespaceType, implementation),
                            Scope.Scoped => services.AddScoped(namespaceType, implementation),
                            Scope.Transient => services.AddTransient(namespaceType, implementation),
                            _ => throw new NotImplementedException()
                        };

                        Debug.WriteLine($"*** {currentNamespace}: Mapped {namespaceType} to {implementation}");

                        break;
                    }
                }
            }

            timer.Stop();

            Debug.WriteLine($"*** {currentNamespace}: Time Spent Configuring Dependencies: " + timer.ElapsedMilliseconds + "ms");
        }
    }

    public enum Scope : byte
    {
        Singleton = 1,
        Scoped = 2,
        Transient = 3
    }
}