using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorDnd;

/// <summary>
/// Thin wrapper around the <c>blazor-dnd.js</c> module. Registered as a scoped
/// service by <c>AddBlazorDnd()</c> and used by the drag/drop components.
/// </summary>
public sealed class BlazorDndDndInterop : IAsyncDisposable
{
    /// <summary>
    /// Prefix of the <c>DataTransfer</c> type that marks drags started by a
    /// <see cref="BlazorDndDraggable{TItem}"/>; the draggable's handler id follows it.
    /// </summary>
    internal const string SourceTypePrefix = "application/x-blazor-dnd-";

    private const string ModulePath = "./_content/BlazorDnd/blazor-dnd.js";

    private readonly IJSRuntime _js;
    private Task<IJSObjectReference>? _module;

    public BlazorDndDndInterop(IJSRuntime js) => _js = js;

    internal static string SourceType(string handlerId) => SourceTypePrefix + handlerId;

    public async ValueTask<BlazorDndDomRect?> GetBoundingClientRectAsync(ElementReference element)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<BlazorDndDomRect?>("getBoundingClientRect", element);
    }

    internal async ValueTask<IJSObjectReference?> RegisterDragSourceAsync(
        ElementReference element, object dotNetRef, BlazorDndDragSourceOptions options)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<IJSObjectReference?>(
            "registerDragSource", element, dotNetRef, options);
    }

    internal async ValueTask<IJSObjectReference?> RegisterDropTargetAsync(
        ElementReference element, object dotNetRef, BlazorDndDropTargetOptions options)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<IJSObjectReference?>(
            "registerDropTarget", element, dotNetRef, options);
    }

    // A failed import is retried by the next call instead of being cached for the whole scope.
    private Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module is null || _module.IsFaulted || _module.IsCanceled)
            _module = _js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
        return _module;
    }

    public async ValueTask DisposeAsync()
    {
        var module = _module;
        _module = null;
        if (module is null) return;
        try
        {
            await (await module).DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
                                       or OperationCanceledException or InvalidOperationException) { }
    }
}
