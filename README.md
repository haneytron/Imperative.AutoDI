Install:

- `PM> Install-Package Imperative.AutoDI`
- `nuget install Imperative.AutoDI`

Automatically add dependencies to your `IServiceCollection` by namespace or selector functions.

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
        // Singletons - with wildcard that will map anythin in a namespace starting with "My.Namespace.Two" including namespaces like "My.Namespace.TwoTimes"
        config.AddSingletons("My.Namespace.One", "My.Namespace.Two*");
        // Scopeds - basic namespace usage
        config.AddScopeds("OtherNamespace");
        // Transients - with wildcard that will map anything in a namespace starting with "Another.Namespace."
        config.AddTransients("Another.Namespace.*");
    });
});

...

var app = builder.Build();
```
