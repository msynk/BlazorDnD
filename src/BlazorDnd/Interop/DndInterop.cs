using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorDnd;

/// <summary>
/// Thin wrapper around the <c>blazor-dnd.js</c> module. Registered as a scoped
/// service by <c>AddBlazorDnd()</c> and used by the drag/drop components.
/// </summary>
public sealed class DndInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public DndInterop(IJSRuntime js)
    {
        _module = new(() => js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorDnd/blazor-dnd.js").AsTask());
    }

    public async ValueTask<DomRect?> GetBoundingClientRectAsync(ElementReference element)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<DomRect?>("getBoundingClientRect", element);
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

/// <summary>A DOMRect projected from JS.</summary>
public sealed class DomRect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
