using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorDnd;

/// <summary>
/// Thin wrapper around the <c>blazor-dnd.js</c> module. Registered as a scoped
/// service by <c>AddBlazorDnd()</c> and used by the drag/drop components.
/// </summary>
public sealed class BlazorDndDndInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public BlazorDndDndInterop(IJSRuntime js)
    {
        _module = new(() => js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorDnd/blazor-dnd.js").AsTask());
    }

    public async ValueTask<BlazorDndDomRect?> GetBoundingClientRectAsync(ElementReference element)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<BlazorDndDomRect?>("getBoundingClientRect", element);
    }

    public async ValueTask<IJSObjectReference> UseEmptyDragPreviewAsync(ElementReference element)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<IJSObjectReference>("useEmptyDragPreview", element);
    }

    public async ValueTask<IJSObjectReference> ConfigureDragSourceAsync(
        ElementReference element, string effectAllowed)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<IJSObjectReference>(
            "configureDragSource", element, effectAllowed);
    }

    public async ValueTask<IJSObjectReference> RegisterNativeDropAsync(
        ElementReference element, object dotNetRef)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<IJSObjectReference>(
            "registerNativeDrop", element, dotNetRef);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try
            {
                var module = await _module.Value;
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            catch (OperationCanceledException) { }
        }
    }
}
