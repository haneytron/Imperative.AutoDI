using Microsoft.Extensions.DependencyInjection;

namespace Imperative.AutoDI
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions used to configure auto dependency injection.
    /// </summary>
    public static class AutoDependencyInjectionExtensions
    {
        /// <summary>
        /// Adds Auto Dependency Injection to the <see cref="IServiceCollection"/>, and allows configuration of how dependencies are automatically registered.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <param name="config">The configuration method.</param>
        public static IServiceCollection AddAutoDependencyInjection(this IServiceCollection serviceCollection, Action<IAutoDependencyConfigurator> config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var autoDependencyConfigurator = new AutoDependencyConfigurator(serviceCollection);

            config(autoDependencyConfigurator);

            return serviceCollection;
        }
    }
}