using Microsoft.Extensions.DependencyInjection;

namespace Imperative.AutoDI
{
    /// <summary>
    /// Represents an Auto Dependency Configurator. Allows configuring how dependencies are registered to an IServiceCollection.
    /// </summary>
    public interface IAutoDependencyConfigurator
    {
        /// <summary>
        /// Adds multiple singleton services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="namespaces">The namespaces to search for service types and their implementations. Namespaces which end with a trailing wildcard character '*' will map all child namespaces.</param>
        /// <seealso cref="ServiceLifetime.Singleton"/>
        IAutoDependencyConfigurator AddSingletons(params string[] namespaces);
        /// <summary>
        /// Adds multiple singleton services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="servicesTypesSelector">A function that selects the service types to register. These will usually be interfaces and abstract classes.</param>
        /// <param name="implementationTypesSelector">A function that selects the implementation types to register. These will usually be classes and will implement the interfaces or abstract classes selected in <paramref name="servicesTypesSelector" /></param>
        /// <returns></returns>
        IAutoDependencyConfigurator AddSingletons(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector);
        /// <summary>
        /// Adds multiple scoped services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="namespaces">The namespaces to search for service types and their implementations. Namespaces which end with a trailing wildcard character '*' will map all child namespaces.</param>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        IAutoDependencyConfigurator AddScopeds(params string[] namespaces);
        /// <summary>
        /// Adds multiple scoped services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="servicesTypesSelector">A function that selects the service types to register. These will usually be interfaces and abstract classes.</param>
        /// <param name="implementationTypesSelector">A function that selects the implementation types to register. These will usually be classes and will implement the interfaces or abstract classes selected in <paramref name="servicesTypesSelector" /></param>
        /// <returns></returns>
        IAutoDependencyConfigurator AddScopeds(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector);
        /// <summary>
        /// Adds multiple transient services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="namespaces">The namespaces to search for service types and their implementations. Namespaces which end with a trailing wildcard character '*' will map all child namespaces.</param>
        /// <seealso cref="ServiceLifetime.Transient"/>
        IAutoDependencyConfigurator AddTransients(params string[] namespaces);
        /// <summary>
        /// Adds multiple transient services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="servicesTypesSelector">A function that selects the service types to register. These will usually be interfaces and abstract classes.</param>
        /// <param name="implementationTypesSelector">A function that selects the implementation types to register. These will usually be classes and will implement the interfaces or abstract classes selected in <paramref name="servicesTypesSelector" /></param>
        /// <returns></returns>
        IAutoDependencyConfigurator AddTransients(Func<IEnumerable<Type>> servicesTypesSelector, Func<IEnumerable<Type>> implementationTypesSelector);
    }
}
