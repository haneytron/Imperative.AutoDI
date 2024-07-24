Install:

- `PM> Install-Package Imperative.AutoDI`
- `nuget install Imperative.AutoDI`

Automatically add dependencies to your `IServiceCollection` by namespace or selector functions.

Examples (note: classes must implement the relevant interface):

Example usage in `Program.cs`:

```
using Imperative.AutoDI;

...

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureServices((context, services) =>
{
    // Configure Auto DI
    services.AddAutoDependencyInjection(config =>
    {
        // Singletons
        config.AddSingletons("My.Namespace.One", "My.Namespace.Two*");
        // Scoped
        config.AddScopeds("OtherNamespace");
        // Transient
        config.AddTransients("Another.Namespace");
    });
});

...

var app = builder.Build();
```
