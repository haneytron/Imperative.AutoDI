using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        /// <param name="loggerFactory">Optional: the logger factory to use for logging. If none is provided, debug logging will be printed to the console.</param>
        public static IServiceCollection AddAutoDependencyInjection(this IServiceCollection serviceCollection, Action<IAutoDependencyConfigurator> config, ILoggerFactory loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(config);

            var autoDependencyConfigurator = new AutoDependencyConfigurator(serviceCollection, loggerFactory);

            config(autoDependencyConfigurator);

            return serviceCollection;
        }
    }
}