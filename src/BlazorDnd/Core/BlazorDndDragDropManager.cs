namespace BlazorDnd;

/// <summary>
/// The central, scoped state container for a <see cref="BlazorDndDndProvider"/> subtree.
/// This is the Blazor equivalent of React DnD's internal Redux store plus the
/// drag-drop manager. It owns the authoritative drag state, exposes it through the
/// <see cref="BlazorDndIDndMonitor"/> interface, and notifies subscribers when things change.
/// </summary>
public sealed class BlazorDndDragDropManager : BlazorDndIDndMonitor
{
    private readonly Dictionary<string, BlazorDndDropTargetEntry> _targets = new();
    private readonly List<string> _overOrder = new();
    private int _idSeed;

    // ---- authoritative drag state ------------------------------------------------
    private bool _isDragging;
    private object? _item;
    private string? _itemType;
    private string? _sourceId;
    private object? _dropResult;
    private bool _didDrop;
    private BlazorDndXYCoord? _initialClientOffset;
    private BlazorDndXYCoord? _initialSourceClientOffset;
    private BlazorDndXYCoord? _clientOffset;

    /// <summary>Raised on structural changes (begin/end/enter/leave/drop).</summary>
    public event Action? StateChanged;

    /// <summary>Raised frequently while dragging as the pointer offset changes.</summary>
    public event Action? OffsetChanged;

    /// <summary>The shared monitor instance for this provider subtree.</summary>
    public BlazorDndIDndMonitor Monitor => this;

    public string GenerateHandlerId(string prefix) => $"{prefix}-{++_idSeed}";

    // ---- registration ------------------------------------------------------------

    internal void RegisterTarget(BlazorDndDropTargetEntry entry)
    {
        // Upsert: preserve a live EnterCount across re-renders so an in-flight hover
        // is not lost when the component re-registers with new parameters.
        if (_targets.TryGetValue(entry.Id, out var existing))
        {
            existing.Accept = entry.Accept;
            existing.CanDrop = entry.CanDrop;
        }
        else
        {
            _targets[entry.Id] = entry;
        }
    }

    internal void UnregisterTarget(string id)
    {
        _targets.Remove(id);
        _overOrder.Remove(id);
    }

    internal BlazorDndDropTargetEntry? GetTarget(string id) =>
        _targets.TryGetValue(id, out var t) ? t : null;

    // ---- lifecycle ---------------------------------------------------------------

    internal void BeginDrag(string sourceId, object? item, string itemType,
        BlazorDndXYCoord initialClientOffset, BlazorDndXYCoord initialSourceClientOffset)
    {
        _isDragging = true;
        _sourceId = sourceId;
        _item = item;
        _itemType = itemType;
        _dropResult = null;
        _didDrop = false;
        _initialClientOffset = initialClientOffset;
        _initialSourceClientOffset = initialSourceClientOffset;
        _clientOffset = initialClientOffset;
        _overOrder.Clear();
        NotifyStateChanged();
    }

    internal void SetClientOffset(BlazorDndXYCoord? offset)
    {
        if (!_isDragging) return;
        _clientOffset = offset;
        OffsetChanged?.Invoke();
    }

    /// <summary>
    /// Patches the source element's initial offset shortly after the drag begins (the
    /// bounding rect is fetched asynchronously). Keeps custom drag layers aligned.
    /// </summary>
    internal void SetInitialSourceClientOffset(BlazorDndXYCoord offset)
    {
        if (!_isDragging) return;
        _initialSourceClientOffset = offset;
        OffsetChanged?.Invoke();
    }

    internal void TargetEnter(string id)
    {
        var entry = GetTarget(id);
        if (entry is null) return;
        entry.EnterCount++;
        if (entry.EnterCount == 1)
        {
            _overOrder.Remove(id);
            _overOrder.Add(id);
            NotifyStateChanged();
        }
    }

    internal void TargetLeave(string id)
    {
        var entry = GetTarget(id);
        if (entry is null) return;
        if (entry.EnterCount > 0) entry.EnterCount--;
        if (entry.EnterCount == 0)
        {
            _overOrder.Remove(id);
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Records that a drop occurred. Called by each drop target in the bubbling chain
    /// (innermost first). A non-null result from a later (outer) target overrides the
    /// result set by an inner target, matching React DnD semantics.
    /// </summary>
    internal void RecordDrop(object? result)
    {
        _didDrop = true;
        if (result is not null) _dropResult = result;
    }

    internal void EndDrag()
    {
        _isDragging = false;
        _item = null;
        _itemType = null;
        _sourceId = null;
        _initialClientOffset = null;
        _initialSourceClientOffset = null;
        _clientOffset = null;
        foreach (var t in _targets.Values) t.EnterCount = 0;
        _overOrder.Clear();
        NotifyStateChanged();
    }

    internal void ResetDropState()
    {
        _dropResult = null;
        _didDrop = false;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    // ---- BlazorDndIDndMonitor -------------------------------------------------------------

    public bool IsDragging() => _isDragging;

    public bool IsDragging(string? sourceId)
    {
        if (!_isDragging) return false;
        if (sourceId is null) return true;
        return _sourceId == sourceId;
    }

    public string? GetItemType() => _itemType;

    public object? GetItem() => _item;

    public T? GetItem<T>() => _item is T t ? t : default;

    public object? GetDropResult() => _dropResult;

    public T? GetDropResult<T>() => _dropResult is T t ? t : default;

    public bool DidDrop() => _didDrop;

    public bool CanDrop(string handlerId)
    {
        if (!_isDragging) return false;
        var entry = GetTarget(handlerId);
        if (entry is null) return false;
        if (_itemType is null || !entry.Accept.Contains(_itemType)) return false;
        return entry.CanDrop?.Invoke(_item, this) ?? true;
    }

    public bool IsOver(string handlerId, bool shallow = false)
    {
        if (!_isDragging) return false;
        var entry = GetTarget(handlerId);
        if (entry is null || entry.EnterCount <= 0) return false;
        if (!shallow) return true;
        return _overOrder.Count > 0 && _overOrder[^1] == handlerId;
    }

    public BlazorDndXYCoord? GetInitialClientOffset() => _initialClientOffset;

    public BlazorDndXYCoord? GetInitialSourceClientOffset() => _initialSourceClientOffset;

    public BlazorDndXYCoord? GetClientOffset() => _clientOffset;

    public BlazorDndXYCoord? GetDifferenceFromInitialOffset()
    {
        if (_clientOffset is null || _initialClientOffset is null) return null;
        return _clientOffset.Value - _initialClientOffset.Value;
    }

    public BlazorDndXYCoord? GetSourceClientOffset()
    {
        var diff = GetDifferenceFromInitialOffset();
        if (diff is null || _initialSourceClientOffset is null) return null;
        return _initialSourceClientOffset.Value + diff.Value;
    }
}
