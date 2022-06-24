Install:

`Install-Package Imperative.AutoDI`
`nuget install Imperative.AutoDI`

Automatically add dependencies to your `IServiceCollection` by namespace. Matches on convention of classes where the class name ends with the interface name minus the leading `I`. Namespaces specified will include all child namespaces (any namespaces which start with the specified namespace string).

Examples (note: classes must implement the relevant interface):

- `InMemoryCacheManager` will match `ICacheManager`
- `CacheManager` will match `ICacheManager`
- `SendGridEmailHandler` will match `IEmailHandler`

Example usage in `Program.cs`:

```
using Imperative.AutoDI;

...

var builder = WebApplication.CreateBuilder(args);
// Configure DI
AutoDependencyInjection.ConfigureDependencies(builder.Services, Scope.Singleton, 
    "MyApp.Data", 
    "MyApp.Handlers");

...

var app = builder.Build();
```
