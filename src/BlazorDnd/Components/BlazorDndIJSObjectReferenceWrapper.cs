using Microsoft.JSInterop;

namespace BlazorDnd;

/// <summary>Wraps an IJSObjectReference's disposal in an exception-safe ValueTask.</summary>
internal sealed class BlazorDndIJSObjectReferenceWrapper : IAsyncDisposable
{
    private readonly IJSObjectReference _reference;
    public BlazorDndIJSObjectReferenceWrapper(IJSObjectReference reference) => _reference = reference;

    public async ValueTask DisposeAsync()
    {
        try { await _reference.InvokeVoidAsync("dispose"); }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
        finally
        {
            try { await _reference.DisposeAsync(); } catch { /* ignore */ }
        }
    }
}
