using Microsoft.Extensions.DependencyInjection;

namespace BlazorDnd;

/// <summary>Registration helpers for BlazorDnd services.</summary>
public static class BlazorDndServiceCollectionExtensions
{
    /// <summary>
    /// Registers the BlazorDnd interop service. The <see cref="BlazorDndDragDropManager"/> itself is
    /// provided per-subtree by the <see cref="BlazorDndDndProvider"/> component, so it is not registered
    /// here; this only wires up the JS interop dependency.
    /// </summary>
    public static IServiceCollection AddBlazorDnd(this IServiceCollection services)
    {
        services.AddScoped<BlazorDndDndInterop>();
        return services;
    }
}
