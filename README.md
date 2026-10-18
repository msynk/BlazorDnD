# BlazorDnd

A declarative drag-and-drop library for Blazor, inspired by [React DnD](https://react-dnd.github.io/react-dnd/). Instead of hooks, BlazorDnd gives you composable components (`BlazorDndDraggable`, `BlazorDndDropTarget`, `BlazorDndDragLayer`, `BlazorDndDragHandle`) backed by the HTML5 drag-and-drop API.

It supports in-app drags between typed sources and targets, custom drag previews that follow the pointer, drag handles, and native OS drags (files, text, URLs, HTML).

## Features

- Declarative `useDrag` / `useDrop` style components with strongly-typed item payloads
- Type matching between draggables and drop targets via `Accept` lists
- Custom drag layers for fully styled, pointer-following previews
- Drag handles to restrict where a drag can start
- Native OS drag support (dropped files, selected text, links, HTML)
- Isolated drag contexts through nestable providers
- A shared, read-only monitor for inspecting live drag state

## Requirements

- .NET 10.0
- A Blazor project (Server or WebAssembly)

## Installation

Add a reference to the `BlazorDnd` project (or package), then register the interop service in your `Program.cs`:

```csharp
using BlazorDnd;

builder.Services.AddBlazorDnd();
```

`AddBlazorDnd()` wires up the JS interop dependency. The drag-and-drop manager itself is provided per-subtree by the `BlazorDndDndProvider` component, so it is not registered in DI.

## Getting started

### 1. Wrap your drag-and-drop region in a provider

`BlazorDndDndProvider` is the root of a drag-and-drop context (the equivalent of React DnD's `<DndProvider>`). It creates a manager and cascades it to all descendant components. Nest providers when you need isolated contexts.

```razor
@using BlazorDnd

<BlazorDndDndProvider>
    @* draggables and drop targets go here *@
</BlazorDndDndProvider>
```

### 2. Make something draggable

`BlazorDndDraggable<TItem>` registers its host element as a drag source. Declare a `Type` (used to match drop targets) and an `Item` payload. The child content receives a `BlazorDndDragState` with collected props such as `IsDragging`.

```razor
<BlazorDndDraggable TItem="CardModel" Type="card" Item="card">
    <div class="card @(context.IsDragging ? "dragging" : "")">
        @card.Title
    </div>
</BlazorDndDraggable>
```

### 3. Add a drop target

`BlazorDndDropTarget<TItem>` registers its host element as a drop target. List the item types it accepts and handle `OnDrop`.

```razor
<BlazorDndDropTarget TItem="CardModel" Accept="card" OnDrop="HandleDrop">
    <div class="dropzone @(context.IsOver && context.CanDrop ? "active" : "")">
        Drop cards here
    </div>
</BlazorDndDropTarget>

@code {
    private void HandleDrop(BlazorDndDropEventArgs<CardModel> args)
    {
        var dropped = args.Item;
        // ... move the item, update state, etc.
    }
}
```

## Components

### `BlazorDndDndProvider`

Root of a drag-and-drop context. Creates and cascades a `BlazorDndDragDropManager`.

| Parameter | Type | Description |
| --- | --- | --- |
| `Manager` | `BlazorDndDragDropManager?` | Optional externally-owned manager. When omitted, one is created for the subtree. |
| `ChildContent` | `RenderFragment?` | The drag-and-drop content. |

### `BlazorDndDraggable<TItem>`

Wires its host element into the DnD system as a drag source. Child content receives a `BlazorDndDragState`.

| Parameter | Type | Description |
| --- | --- | --- |
| `Type` | `string` | **Required.** Item type used to match drop-target `Accept` lists. |
| `Item` | `TItem?` | The item payload. Ignored when `ItemFactory` is supplied. |
| `ItemFactory` | `Func<TItem?>?` | Produces the item lazily at drag start. Return `default` to cancel the drag (in Blazor Server the browser still shows the drag, but no target accepts it). |
| `CanDrag` | `bool` | Whether dragging is allowed. Defaults to `true`. |
| `CanDragPredicate` | `Func<bool>?` | Optional predicate overriding `CanDrag`, evaluated at render time and again at drag start. |
| `IsDraggingOverride` | `Func<BlazorDndIDndMonitor, bool>?` | Custom `isDragging` implementation (e.g. id-based for re-parented items). |
| `UseCustomDragLayer` | `bool` | Hide the native drag image (pair with a `BlazorDndDragLayer`). |
| `UseHandle` | `bool` | Require a `BlazorDndDragHandle` child to initiate the drag. |
| `DropEffect` | `string` | The effects the source allows, assigned to `dataTransfer.effectAllowed` (drives the cursor): `move` (default), `copy`, `link`, `copyMove`, `copyLink`, `linkMove` or `all`. |
| `OnBegin` | `EventCallback<TItem?>` | Fired when the drag begins, before any hover, drop or end callback of that drag. |
| `OnEnd` | `EventCallback<BlazorDndDragEndContext<TItem>>` | Fired when the drag ends (drop or cancel), even if the draggable was removed during the drag. |
| `Tag` | `string` | Host element tag name. Defaults to `div`. |

### `BlazorDndDropTarget<TItem>`

Wires its host element into the DnD system as a drop target. Child content receives a `BlazorDndDropState<TItem>` (`IsOver`, `IsOverShallow` for the innermost hovered target, `CanDrop`, `ItemType`, `Item`).

While a drag is over a target that cannot accept it (wrong type, or `CanDropPredicate` returns `false`), the browser shows the "no drop" cursor and no drop happens; an enclosing target that can accept the item still receives it. Native drags over a text input, textarea or editable element inside a drop target are left to the browser, so text can still be dropped into them.

| Parameter | Type | Description |
| --- | --- | --- |
| `Accept` | `string?` | A single accepted item type. |
| `AcceptTypes` | `IEnumerable<string>?` | Multiple accepted item types (combined with `Accept`). |
| `OnDrop` | `EventCallback<BlazorDndDropEventArgs<TItem>>` | Called when a compatible in-app item is dropped. Set `args.Result` to publish a result. Nested targets are called innermost first; `args.Monitor.DidDrop()` tells whether an inner target already handled the drop. |
| `OnHover` | `EventCallback<BlazorDndDropEventArgs<TItem>>` | Called repeatedly while a compatible in-app item is hovered. |
| `OnNativeDrop` | `EventCallback<BlazorDndNativeDropEventArgs>` | Called when a native OS drag is dropped. |
| `CanDropPredicate` | `Func<TItem?, BlazorDndIDndMonitor, bool>?` | Gates whether this target can accept the current item. |
| `Tag` | `string` | Host element tag name. Defaults to `div`. |

### `BlazorDndDragLayer<TItem>`

Renders a custom drag preview that follows the pointer (the equivalent of React DnD's `useDragLayer`). Pair with `BlazorDndDraggable UseCustomDragLayer="true"` to hide the native preview. Child content receives a `BlazorDndDragLayerState<TItem>` with the live offsets (`ClientOffset`, `SourceClientOffset`, `DifferenceFromInitialOffset`, etc.).

```razor
@using System.Globalization

<BlazorDndDragLayer TItem="CardModel">
    @if (context.IsDragging && context.ClientOffset is { } offset)
    {
        @* format with the invariant culture: a decimal comma would be invalid CSS *@
        <div style="position: fixed; pointer-events: none;
                     left: @(offset.X.ToString(CultureInfo.InvariantCulture))px;
                     top: @(offset.Y.ToString(CultureInfo.InvariantCulture))px;">
            @context.Item?.Title
        </div>
    }
</BlazorDndDragLayer>
```

### `BlazorDndDragHandle`

Designates a sub-region of a `BlazorDndDraggable` (configured with `UseHandle="true"`) as the grab handle. The handle is the native draggable element, so only drags that start on it move the item; the whole draggable is still used as the drag image, and the rest of it stays interactive.

```razor
<BlazorDndDraggable TItem="CardModel" Type="card" Item="card" UseHandle="true">
    <div class="card">
        <BlazorDndDragHandle>
            <span class="grip">⠿</span>
        </BlazorDndDragHandle>
        @card.Title
    </div>
</BlazorDndDraggable>
```

## Native OS drags

A drop target can accept drags that originate outside the application by listing one of the `BlazorDndNativeTypes` in its `Accept`/`AcceptTypes`:

| Constant | Source |
| --- | --- |
| `BlazorDndNativeTypes.File` | Dropped files |
| `BlazorDndNativeTypes.Url` | Dragged links |
| `BlazorDndNativeTypes.Text` | Selected text |
| `BlazorDndNativeTypes.Html` | Dragged HTML |

```razor
<BlazorDndDropTarget TItem="object" Accept="@BlazorDndNativeTypes.File" OnNativeDrop="HandleFiles">
    <div class="dropzone">Drop files here</div>
</BlazorDndDropTarget>

@code {
    private void HandleFiles(BlazorDndNativeDropEventArgs args)
    {
        foreach (var file in args.Files)
        {
            // inspect dropped file metadata
        }
    }
}
```

## The monitor

`BlazorDndIDndMonitor` is a read-only view over the drag-and-drop state, shared through the manager and passed into collecting and lifecycle callbacks. It mirrors the union of React DnD's `DragSourceMonitor`, `DropTargetMonitor`, and `DragLayerMonitor`, exposing methods like `IsDragging()`, `GetItem<T>()`, `GetItemType()`, `CanDrop(handlerId)`, `IsOver(handlerId)`, and the various offset accessors used to build custom drag layers.

## License

Licensed under the [MIT License](LICENSE).
