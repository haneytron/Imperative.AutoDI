Install:

- `PM> Install-Package Imperative.AutoDI`
- `nuget install Imperative.AutoDI`

Automatically add dependencies to your `IServiceCollection` by namespaces (with some wildcard support) or as arrays of `Type`s.

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
        // Singletons - with wildcard that will map anything in a namespace starting with "My.Namespace.Two"
        // including namespaces like "My.Namespace.TwoTimes" and "My.Namespace.Two.Three"
        config.AddSingletons("My.Namespace.One", "My.Namespace.Two*");
        // Scopeds - basic namespace usage, will only map the things at that exact namespace
        config.AddScopeds("OtherNamespace");
        // Transients - with wildcard that will map anything in a namespace starting with "Another.Namespace."
        // such as "Another.Namespace.Two" and "Another.Namespace.Testing.Three"
        config.AddTransients("Another.Namespace.*");
    });
});

...

var app = builder.Build();
```
