using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorDnd;

/// <summary>Registration helpers for BlazorDnd services.</summary>
public static class BlazorDndServiceCollectionExtensions
{
    /// <summary>
    /// Registers the BlazorDnd interop service. The <see cref="BlazorDndDragDropManager"/> itself is
    /// provided per-subtree by the <see cref="BlazorDndDndProvider"/> component, so it is not registered
    /// here; this only wires up the JS interop dependency. Calling it more than once is harmless.
    /// </summary>
    public static IServiceCollection AddBlazorDnd(this IServiceCollection services)
    {
        services.TryAddScoped<BlazorDndDndInterop>();
        return services;
    }
}
