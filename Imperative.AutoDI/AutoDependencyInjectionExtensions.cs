using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

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
        /// <param name="loggerFactory">Optional: the logger factory to use for logging. If none is provided, nothing will be logged. Useful for debugging.</param>
        /// <param name="includeFrameworkAssemblies">Whether or not to include framework assemblies. The default is to ignore them (because we should not manually wire them into DI). These tend to start with "System." or "Microsoft." etc.</param>
        public static IServiceCollection AddAutoDependencyInjection(this IServiceCollection serviceCollection, Action<IAutoDependencyConfigurator> config, ILoggerFactory loggerFactory = null, bool includeFrameworkAssemblies = false)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var autoDependencyConfigurator = new AutoDependencyConfigurator(serviceCollection, loggerFactory, includeFrameworkAssemblies);

            config(autoDependencyConfigurator);

            return serviceCollection;
        }
    }
}