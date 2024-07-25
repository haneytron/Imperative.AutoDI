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
        /// <param name="types">The service and implementation types to register. You can include interfaces abstract classes, and concrete types in any order.</param>
        /// <returns></returns>
        IAutoDependencyConfigurator AddSingletons(params Type[] types);
        /// <summary>
        /// Adds multiple scoped services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="namespaces">The namespaces to search for service types and their implementations. Namespaces which end with a trailing wildcard character '*' will map all child namespaces.</param>
        /// <seealso cref="ServiceLifetime.Scoped"/>
        IAutoDependencyConfigurator AddScopeds(params string[] namespaces);
        /// <summary>
        /// Adds multiple scoped services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="types">The service and implementation types to register. You can include interfaces abstract classes, and concrete types in any order.</param>        /// <returns></returns>
        IAutoDependencyConfigurator AddScopeds(params Type[] types);
        /// <summary>
        /// Adds multiple transient services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="namespaces">The namespaces to search for service types and their implementations. Namespaces which end with a trailing wildcard character '*' will map all child namespaces.</param>
        /// <seealso cref="ServiceLifetime.Transient"/>
        IAutoDependencyConfigurator AddTransients(params string[] namespaces);
        /// <summary>
        /// Adds multiple transient services to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="types">The service and implementation types to register. You can include interfaces abstract classes, and concrete types in any order.</param>
        /// <returns></returns>
        IAutoDependencyConfigurator AddTransients(params Type[] types);
    }
}
