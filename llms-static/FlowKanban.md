<!-- Supplementary documentation for FlowKanban -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

`FlowKanban` is a high-level container component for building interactive Kanban boards. It manages a collection of columns and the tasks within them, providing a modern, industrial interface with specialized layout zones. It features a horizontally scrollable main board area and a vertical utility sidebar for board-level actions (home, settings, undo/redo, zoom, save/load, metrics/help).

## File Composition

The Kanban system spans multiple files for modularity:

| File | Description | Dependencies / Relationships |
| --- | --- | --- |
| `FlowKanban.cs` | Main control shell: core layout, persistence, and state management. | Dependency: `FlowKanbanManager`, `FlowKanbanHome` |
| `FlowKanban.Dialogs.cs` | Partial class: Encapsulates all modal dialog invocation logic. | Dependency: `FloweryDialogBase`, `FlowBoardEditorDialog` |
| `FlowKanban.Drop.cs` | Partial class: Handles Drag & Drop interactions for columns and cards. | Dependency: `FlowTask`, `FlowKanbanColumnData` |
| `FlowKanban.Home.cs` | Partial class: Orchestrates board navigation and Home screen integration. | Dependency: `FlowKanbanHome`, `FlowBoardMetadata` |
| `FlowKanban.UI.cs` | Partial class: Manages UI state, visual sizing tiers, and scrolling logic. | Dependency: `DaisySize` tokens |
| `FlowKanbanHome.cs` | **NEW** - Home screen control for multi-board management and search. | Dependency: `FlowBoardMetadata`, `FlowKanbanManager` |
| `FlowBoardMetadata.cs` | **NEW** - Lightweight model for board listings on the Home screen. | - |
| `FlowKanbanColumn.cs` | UI control: Renders a vertical column with a header and task items. | Dependency: `FlowKanbanColumnData`, `FlowTaskCard` |
| `FlowTaskCard.cs` | UI control: Visual representation of an individual Kanban task (card). | Dependency: `FlowTask` model |
| `FlowKanbanAddCard.cs` | UI control: Inline "Add Task" button/placeholder within a column. | Dependency: `FlowKanbanColumnData` |
| `FlowKanbanModels.cs` | Core data models: `FlowTask`, `FlowKanbanData`, `FlowKanbanColumnData`. | - |
| `FlowKanbanTaskView.cs` | **NEW** - View-only wrapper for lane-aware task rendering in swimlanes. | Dependency: `FlowTask`, `FlowKanbanLane` |
| `FlowKanbanManager.cs` | Central Engine: Full programmatic logic for board/task manipulation. | Dependency: `FlowKanbanData`, `FlowKanbanCommandHistory` |
| `FlowKanbanMigration.cs` | Migration Engine: Versions schema and upgrades legacy board JSON files. | Dependency: `FlowKanbanData` |
| `IBoardStore.cs` | Persistence abstraction for board data (swap JSON/DB/cloud). | Used by `FlowKanban`, `FlowKanbanBoardStore` |
| `FlowKanbanBoardStore.cs` | Default JSON board store with atomic save and key management. | Dependency: `IStateStorage`, `FlowKanbanBoardSanitizer` |
| `FlowKanbanBoardSanitizer.cs` | Load-time validation and sanitization (limits, control chars, invalid IDs). | Dependency: `FlowKanbanMigration` |
| `FlowKanbanCommands.cs` | Command implementations: Undo/redo transactional operations. | Dependency: `FlowKanbanManager` |
| `FlowKanbanCommandHistory.cs` | Transactional History: Manages the undo/redo stack. | Dependency: `FlowKanbanCommands` |
| `FlowKanbanJsonContext.cs` | Serialization: Source-generated JSON context for Native AOT support. | Dependency: `FlowKanbanModels` |
| `FlowTaskEditorDialog.cs` | Modal: Comprehensive editor for task details, dates, and effort. | Dependency: `FlowTask`, `FloweryDialogBase` |
| `FlowBoardEditorDialog.cs` | Modal: Edits board metadata and manages swimlane definitions. | Dependency: `FlowKanbanData`, `FloweryDialogBase` |
| `FlowKanbanSettingsDialog.cs` | Modal: Configures control behavior and UI preferences. | Dependency: `FlowKanban`, `FloweryDialogBase` |

## Key Features

- **Column Management**: Add or remove board sections dynamically.
- **Task Management**: Create and delete tasks within individual columns.
- **Add Card Placement**: Inline add card controls can appear at the top, bottom, or both (user preference).
- **WIP Limits**: Set per-column limits with automatic enforcement and visual warnings.
- **Swimlanes**: Group tasks vertically using lanes for matrix-style organization.
- **Unassigned Lane**: Legacy lane IDs (`0`, null, or unknown) surface in an auto-created "Unassigned" swimlane row.
- **Lane Management**: Create/reorder/delete lanes in board properties, with fallback lane selection on delete.
- **Lane-Aware Drag & Drop**: Dropping into a lane cell updates the task's `LaneId`.
- **Card Title UX**: Title font size is slightly reduced; wraps up to two lines with tooltips only when truncated.
- **Card Footer Badges**: Priority (non-normal) and progress (>0%) badges render in a responsive, wrapping footer row.
- **Blocked State**: Mark tasks as blocked with reasons and duration tracking (blocked tasks cannot move forward to later columns or be archived, and progress is capped at 99% until unblocked).
- **Undo/Redo**: Command history for card operations (move/add/delete/edit/palette/block/archive), bounded to 50 entries; new actions clear the redo stack.
- **Multi-Select**: Bulk operations (move, archive, delete) on selected tasks.
- **Multi-Board Management**: Navigate between multiple boards using the Home screen.
- **Search & Sort**: Filter boards by name and sort them by modification date or title.
- **Board Actions**: Rename, duplicate, delete, and export boards directly from the Home screen.
- **Drag & Drop Ready**: Columns and tasks are hosted in reorder-capable containers.
- **JSON Persistence**: Atomic save strategy (temp-file rename), schema migration support, and safe-load validation.
- **Source Gen Support**: Source-generated JSON metadata through `FlowKanbanJsonContext`.
- **Industrial Sidebar**: A dedicated space for board-level controls (home, settings, undo/redo, zoom, save/load, metrics/help).
- **Persisted Preferences**: Confirmation prompts, auto-save, add card placement, welcome message, undo/redo and status bar settings are stored in the application settings.
- **Host-Managed Assignees**: The host application supplies assignee IDs, names, avatars and roles through `AssigneeAdapter`. The package does no sign-in and connects to no account service.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Board` | `FlowKanbanData` | The root data model containing the columns and tasks. |
| `BoardStore` | `IBoardStore` | Persistence backend for boards (default JSON store). |
| `BoardSize` | `DaisySize` | Current size tier for board controls. |
| `ConfirmColumnRemovals` | `bool` | Require confirmation before deleting a column. |
| `ConfirmCardRemovals` | `bool` | Require confirmation before deleting a card. |
| `AutoSaveAfterEdits` | `bool` | Auto-save board after edits. |
| `AddCardPlacement` | `FlowKanbanAddCardPlacement` | User preference for inline add card placement (Bottom/Top/Both). |
| `EnableUndoRedo` | `bool` | Enables undo/redo command history. |
| `CommandHistory` | `FlowKanbanCommandHistory` | Undo/redo stack manager (counts + next descriptions). |
| `UndoCommand` | `ICommand` | Command to undo the last operation. |
| `RedoCommand` | `ICommand` | Command to redo the last undone operation. |
| `IsStatusBarVisible` | `bool` | Controls visibility of the status bar. |
| `LastBoardId` | `string?` | Last board ID loaded or saved via persistence. |
| `IsSwimlaneLayoutEnabled` | `bool` | True when `Board.GroupBy` is set to `Lane` and lanes exist. |
| `IsStandardLayoutEnabled` | `bool` | True when swimlanes are disabled or there are no lanes. |
| `LaneRows` | `ObservableCollection<FlowKanbanLaneRowView>` | Lane row view models used by the swimlane grid template. |
| `AssigneeAdapter` | `IFlowKanbanAssigneeAdapter?` | Supplies assignee IDs, names, avatars and roles. `null` leaves the saved assignments as they are. |
| `AssigneeFilterOptions` | `ObservableCollection<FlowTaskAssigneeOption>` | The assignees of the filter bar: the adapter's list plus unresolved IDs from saved tasks. |
| `GetAssigneeOptions()` | `IReadOnlyList<FlowTaskAssigneeOption>` | The adapter's assignees sorted by name, as the choices for the task editor. |

## User API: Assignees Without External Account Integration

An assignee is a person who can own a task.
The Kanban package does not sign users in and does not connect to GitHub, Microsoft, Slack or another account service.
The host app owns all external connections, login steps, permissions, privacy rules and access tokens.

The host app gives the board only the data that it needs for display:

- A stable ID for each assignee.
- A display name.
- An optional avatar image (`ImageSource`).
- Optional descriptive roles, such as `Designer` or `Reviewer`.

Roles are display text only. The Kanban package never uses roles to grant or deny access.

Set `FlowKanban.AssigneeAdapter` to an `IFlowKanbanAssigneeAdapter` implementation.
The included `FlowKanbanAssigneeAdapter` covers direct lists, list callbacks and single-ID callbacks.
If the host app needs another data source, it can implement the interface.
A custom adapter raises `AssigneesChanged` after its available assignee data changes.

### Why the ID Matters

The same assignee must keep the same ID between app sessions.
The board stores this ID with each task.
Names, avatars and roles can change without changing task ownership.

IDs are case-sensitive and are kept exactly as supplied: `person-42` and `Person-42` are different IDs.
An ID cannot be empty.
If a list contains the same ID more than once, the adapter uses the first entry.

When an old task contains an unknown ID, the board keeps that ID and its last known name.
The filter bar still lists such an assignee, so a saved filter keeps working.
With an adapter set, the entry is marked "(Unavailable)"; without an adapter, nothing can be resolved and the names stay plain.
The task receives a new name, avatar and role list after the host app resolves the ID.

### Use a Direct Assignee List

If the host app already has the complete assignee list, use `FlowKanbanAssigneeAdapter`:

```csharp
var assignees = new FlowKanbanAssigneeAdapter(new[]
{
    new FlowKanbanAssignee(
        id: "person-42",
        displayName: "Mina Singh",
        avatarSource: minaAvatar,
        roles: new[] { "Designer", "Reviewer" })
});

kanban.AssigneeAdapter = assignees;
```

When the host data changes, replace the list:

```csharp
assignees.SetAssignees(latestAssignees);
```

`SetAssignees` updates the list and tells every attached board to reload it.

### Use Host Callbacks

The board calls a callback to get assignee data.
If the host app loads assignees from its own service, use callbacks:

```csharp
kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
    getAssigneesAsync: LoadAssigneesAsync,
    resolveAssigneeAsync: FindAssigneeAsync);
```

The methods have these shapes:

```csharp
Task<IReadOnlyList<FlowKanbanAssignee>> LoadAssigneesAsync(
    CancellationToken cancellationToken);

Task<FlowKanbanAssignee?> FindAssigneeAsync(
    string assigneeId,
    CancellationToken cancellationToken);
```

`LoadAssigneesAsync` returns the assignees of the board. The board uses them for the assignee filter, and `GetAssigneeOptions()` hands them to the task editor (see below).
`FindAssigneeAsync` finds one ID that already exists in saved task data.
If the host app does not know the ID, it returns `null`.

If the host app only resolves saved IDs, use the resolver-only adapter:

```csharp
kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(FindAssigneeAsync);
```

This form resolves assignees already used by tasks. It does not add other assignees to the editor or filter choices.

A resolver must return the requested ID without changes.
If a resolver returns a different ID, the board reports an error.

When callback data changes, call `NotifyAssigneesChanged()`:

```csharp
assigneeAdapter.NotifyAssigneesChanged();
```

The host app can also request a reload:

```csharp
await kanban.RefreshAssigneesAsync(cancellationToken);
```

### Task Editor

The host opens the task editor through `EditCardCommand`.
`GetAssigneeOptions()` returns the adapter's assignees, sorted by name, ready for `FlowTaskEditorDialog.ShowAsyncWithAssignees`:

```csharp
kanban.EditCardCommand = new RelayCommand<FlowTask>(async task =>
{
    if (task is null || kanban.XamlRoot is null)
        return;

    await FlowTaskEditorDialog.ShowAsyncWithAssignees(
        task,
        kanban.XamlRoot,
        kanban.Board.Tags,
        kanban.GetAssigneeOptions(),
        kanban.AutoExpandCardDetails);
});
```

### Avatars and Roles

`AvatarSource` accepts a WinUI `ImageSource`.
The host app owns the image and keeps it available while the board displays it.
If an ID cannot be resolved, the board clears the current avatar.

The board shows the avatar on the task card and the roles as its tooltip.
The role list is not saved because the host app remains the source of that data.

### Changes, Delays and Errors

The board reloads the assignees when the control opens, when `Board` or `AssigneeAdapter` changes, and when the adapter raises `AssigneesChanged`, also from another thread.
Added, moved and reassigned tasks do not reload the list: they take the last loaded assignees, and an ID the adapter has not been asked about is resolved once through `ResolveAssigneeAsync`.
A newer reload replaces an older unfinished reload.
An unfinished reload cannot change the board after the control is unloaded.
Names that the adapter updates are display data: they raise no `CardEdited` and start no auto-save.

Callback errors do not rewrite the assignee IDs or names in task data.
The `AssigneeAdapterFailed` event reports the operation and the original error.
If the assignee ID is available, the event also reports it.

```csharp
kanban.AssigneeAdapterFailed += (_, error) =>
{
    logger.LogError(
        error.Exception,
        "Assignee operation {Operation} failed for {AssigneeId}.",
        error.Operation,
        error.AssigneeId);
};
```

The host app decides how to show or record this error.
The Kanban package does not store external service credentials or retry external service requests.

## Commands

`FlowKanban` provides a suite of pre-wired commands for easy integration with your UI or ViewModels:

| Command | Parameter | Action |
| --- | --- | --- |
| `AddColumnCommand` | None | Appends a new column to the board. |
| `RemoveColumnCommand` | `FlowKanbanColumnData` | Removes the specified column. |
| `EditColumnCommand` | `FlowKanbanColumnData` | Opens the column editor dialog for the specified column. |
| `AddCardCommand` | `FlowKanbanColumnData` | Adds a new task to the specified column. |
| `RemoveCardCommand` | `FlowTask` | Removes the specific task from its parent column. |
| `EditCardCommand` | `FlowTask` | Opens the task editor dialog. |
| `UndoCommand` | None | Undoes the last command (when enabled). |
| `RedoCommand` | None | Redoes the last undone command (when enabled). |
| `SaveCommand` | None | Serializes the current board to the app state storage (per-board ID). |
| `LoadCommand` | None | Deserializes the most recently saved board from app state storage. |
| `SettingsCommand` | None | Opens Kanban settings dialog. |
| `EditBoardCommand` | None | Opens board properties dialog. |
| `RenameBoardCommand` | None | Opens rename dialog for the board title. |
| `ZoomInCommand` | None | Increases board size tier. |
| `ZoomOutCommand` | None | Decreases board size tier. |
| `ToggleStatusBarCommand` | None | Toggles the status bar visibility. |

## Undo/Redo Behavior

- **History size**: 50 commands (`FlowKanbanCommandHistory.MaxHistorySize`).
- **Recorded operations**: move card, add card, delete card, edit title/description/palette, block/unblock, archive/unarchive (including bulk flows, which enqueue one command per task).
- **Not recorded**: priority/tags/due date edits, column operations, board settings, zoom, selection, and search/sort changes.
- **Redo invalidation**: executing a new command clears the redo stack.
- **Toggle**: when `EnableUndoRedo` is false, commands execute immediately without history.

## Dialogs

### FlowKanban.Dialogs.cs (Partial Class)

Contains all dialog-related methods, separated for maintainability. All dialogs use themed `DaisyButton` controls.

| Method | Purpose |
| --- | --- |
| `ExecuteAddColumn()` | Shows input dialog to add a new column. |
| `ExecuteRemoveColumn(column)` | Confirmation dialog before column deletion. |
| `ExecuteRenameColumn(column)` | Input dialog to rename a column. |
| `ExecuteEditBoard()` | Opens `FlowBoardEditorDialog` for board properties. |
| `ExecuteRenameBoard()` | Quick input dialog for renaming the board. |
| `ExecuteRemoveCard(task)` | Confirmation dialog before card deletion. |
| `ExecuteOpenSettings()` | Opens `FlowKanbanSettingsDialog`. |
| `ShowInputDialogAsync(...)` | Reusable themed input dialog. |
| `ShowConfirmDialogAsync(...)` | Reusable themed confirmation dialog. |

### FlowTaskEditorDialog

A full-featured modal for editing `FlowTask` objects. Uses `DaisyCollapse` sections for organized editing:

- **Details**: Title, description, palette color, subtasks.
- **Scheduling**: Due date, start date, reminder.
- **Effort**: Story points, estimated hours, priority, progress.
- **People**: Assignee field.

Based on `FloweryDialogBase` for smart sizing and automatic theming.

```csharp
var saved = await FlowTaskEditorDialog.ShowAsync(task, control.XamlRoot);
```

### FlowBoardEditorDialog

Edits board-level properties: title, description, created by, and displays read-only metadata (created date, board ID).
Includes swimlane enablement and full lane management (add, reorder, delete with fallback reassignment).

```csharp
var saved = await FlowBoardEditorDialog.ShowAsync(board, control.XamlRoot);
```

### FlowKanbanSettingsDialog

Configures Kanban instance settings. Preferences are persisted in the application settings (see Persistence); board-level toggles update the current board. The undo/redo toggle applies immediately.

| Setting | Description |
| --- | --- |
| Confirm column removals | Require confirmation before deleting a column. |
| Confirm card removals | Require confirmation before deleting a card. |
| Auto-Save after edits | Auto-save board after any edit (uses the per-board storage key). |
| Auto-expand card details | Expand card details in the editor by default. |
| Enable undo/redo | Toggles command history and undo/redo UI (stored with the settings). |
| Display Add Card | Preference for inline add placement (Bottom/Top/Both). |
| Show archive column | Shows or hides the archive column for the current board. |
| Show welcome message | Toggle the Home welcome panel. |
| Welcome title | Home screen welcome title text. |
| Welcome message | Home screen welcome subtitle text. |
| Status bar visibility | Persisted in settings, not exposed in the dialog. |

```csharp
await FlowKanbanSettingsDialog.ShowAsync(kanban, control.XamlRoot);
```

## Layout Zones

### 1. Main Board Area (Left/Center)

The core workspace. It uses a `ScrollViewer` to allow horizontal navigation between columns. Each column is a vertical stack of `FlowTaskCard` controls.

### 1a. Swimlane Grid (When Enabled)

The swimlane layout renders a fixed header row for columns and a row per lane. Each lane row contains a column cell with lane filtering applied, enabling true matrix-style boards. The `FlowKanbanTaskView` wrapper is used to provide lane context to individual cards.

### 2. Utility Sidebar (Right)

A space-efficient vertical bar (`56px` wide) containing square `DaisyButton` controls for board-level management (home, settings, undo/redo, zoom, save/load, metrics/help).

### 3. Home Screen (Dashboard)

A high-level view for managing board collections. It features a responsive grid of boards with thumbnails, a search bar, sorting options, and a Create Board action in the toolbar. Users can navigate from the sidebar to the Home screen to manage their library of boards.

## Persistence (JSON)

Persistence is handled via `StateStorageProvider` and stores data in the same application settings location as other `.state` files.

- **Settings**: one entry `kanban.settings` (includes confirmations, auto-save, add card placement, welcome message, undo/redo, column width, last board and view, and status bar visibility).
- **Board files**: one entry per board ID using `kanban.board.<id>`.
- **Board store**: `IBoardStore` allows swapping JSON for DB/cloud without changing the control.
- **Atomic Save**: Uses temporary file and rename strategy for power-loss safety.
- **Migration**: Automatic upgrade of older board files via `FlowKanbanMigration`.
- **Load hardening**: input length/control characters are validated and invalid IDs are pruned on load.
- **Board ID**: `FlowKanbanData.Id` controls the storage key; if empty, a new GUID is generated on save.
- **Save/Load**: `SaveCommand` writes the current board, `LoadCommand` loads the last saved board.
- **Status bar**: `IsStatusBarVisible` is persisted with settings.

### Persistence API (safe wrappers)

| Method | Description |
| --- | --- |
| `TryLoadSettings(forceReload, out error)` | Loads persisted settings; returns false if an exception occurs. |
| `TrySaveSettings(out error)` | Saves current settings to storage. |
| `TrySaveBoard(out error)` | Saves the current board to the per-board storage key. |
| `TryLoadLastBoard(out error)` | Loads the last saved board (by `LastBoardId`). |

Board data is serialized using `System.Text.Json`. The data structure includes IDs, titles, descriptions, and palette settings for every task and column, ensuring a round-trip save/load preserves the entire board state.

```json
{
  "columns": [
    {
      "id": "...",
      "title": "Backlog",
      "tasks": [
        {
          "id": "...",
          "title": "Fix Auth",
          "description": "...",
          "palette": "Error"
        }
      ]
    }
  ]
}
```

## Usage Examples

### Minimal Kanban Setup

Simply dropping the control into a page provides a fully functional board.

```xml
<kanban:FlowKanban x:Name="ProjectBoard" />
```

### Programmatic Board Setup

You can also bind the `Board` property to an existing data structure in your ViewModel.

```xml
<kanban:FlowKanban Board="{x:Bind ViewModel.MyGlobalBoard}" />
```

## Interaction Tips

- **Reordering**: Drag columns to rearrange your workflow phases. Drag tasks within a column to prioritize them.
- **Swimlanes**: Toggle in board properties. Unassigned cards (legacy lane IDs) appear in the "Unassigned" lane until reassigned.
- **Side Actions**: Use the sidebar to save your progress frequently if auto-save is disabled.
- **Empty States**: If no columns exist, the board will show the background (`DaisyBase100Brush`) and is ready to add the first section via dialogs or keyboard shortcuts.

## FlowKanbanManager

`FlowKanbanManager` provides a **full programmatic API** for managing a `FlowKanban` board. It wraps the control and exposes fluent methods for columns, tasks, subtasks, zooming, serialization, and persistence lifecycle helpers.

### Initialization

```csharp
var manager = new FlowKanbanManager(myKanbanControl);
```

### Manager Properties

| Property | Type | Description |
| --- | --- | --- |
| `Kanban` | `FlowKanban` | The managed control. |
| `Board` | `FlowKanbanData` | The root data model. |
| `GroupBy` | `FlowKanbanGroupBy` | View-only grouping mode (None, Lane). |
| `CommandHistory` | `FlowKanbanCommandHistory` | The undo/redo manager instance. |
| `EnableUndoRedo` | `bool` | Enables transactional command history. |
| `Columns` | `IReadOnlyList<FlowKanbanColumnData>` | All columns. |
| `Lanes` | `IReadOnlyList<FlowKanbanLane>` | All swimlanes. |
| `ColumnCount` | `int` | Number of columns. |
| `TotalTaskCount` | `int` | Total tasks across all columns. |
| `BoardSize` | `DaisySize` | Current board size tier. |
| `AllTasks` | `IEnumerable<FlowTask>` | All tasks across all columns. |
| `CanZoomIn` | `bool` | Whether zoom in is possible. |
| `CanZoomOut` | `bool` | Whether zoom out is possible. |
| `ConfirmColumnRemovals` | `bool` | Require confirmation before deleting a column. |
| `ConfirmCardRemovals` | `bool` | Require confirmation before deleting a card. |
| `AutoSaveAfterEdits` | `bool` | Auto-save board after edits. |
| `LastBoardId` | `string?` | Last board ID loaded or saved by the control. |

### Column Management

| Method | Description |
| --- | --- |
| `AddColumn(title)` | Adds a new column at the end. |
| `InsertColumn(title, index)` | Inserts a column at a specific position. |
| `RemoveColumn(column)` | Removes a column and its tasks. |
| `RemoveColumnByTitle(title)` | Removes a column by title. |
| `RemoveColumnAt(index)` | Removes the column at an index. |
| `FindColumnByTitle(title)` | Finds a column by title. |
| `GetColumnAt(index)` | Gets the column at an index. |
| `GetColumnIndex(column)` | Gets the index of a column. |
| `MoveColumn(column, newIndex)` | Moves a column to a new position. |
| `RenameColumn(column, newTitle)` | Renames a column. |
| `ClearColumns()` | Removes all columns. |

### Task Management

| Method | Description |
| --- | --- |
| `AddTask(...)` | Adds a task with optional lane assignment. |
| `RemoveTask(task)` | Removes a task via `DeleteCardCommand`. |
| `RemoveTaskById(taskId)` | Removes a task by ID. |
| `FindTaskById(taskId)` | Finds a task by ID. |
| `FindTasks(predicate)` | Finds all tasks matching a predicate. |
| `FindColumnForTask(task)` | Finds the column containing a task. |
| `FindColumnForTaskById(taskId)` | Finds the column by task ID. |
| `MoveTask(task, targetColumn, index?)` | Moves task via `MoveCardCommand`. |
| `TryMoveTask(...)` | Moves task with event raising. |
| `TryMoveTaskWithWipEnforcement(...)` | Moves with automated WIP validation. |
| `ReorderTask(task, newIndex)` | Reorders a task within its column. |
| `UpdateTask(task, ...)` | Updates task via `EditCardCommand`. |
| `ClearAllTasks()` | Clears all tasks from all columns. |
| `ClearColumnTasks(column)` | Clears tasks from a specific column. |

### WIP & Policy Management

| Method | Description |
| --- | --- |
| `SetColumnWipLimit(col, limit)` | Sets max card limit for a column. |
| `IsWipExceeded(col)` | Checks if column is over its WIP limit. |
| `GetOverWipColumns()` | Returns all columns currently over limit. |
| `SetColumnPolicy(col, text)` | Defines DoD/Entry criteria for a column. |

### Blocked & Archive State

| Method | Description |
| --- | --- |
| `SetBlocked(task, reason?)` | Marks a task as blocked. |
| `ClearBlocked(task)` | Removes the blocked status. |
| `GetBlockedTasks()` | Returns all currently blocked items. |
| `ArchiveTask(task)` | Archives a task. |
| `ArchiveCompletedTasks(col)` | Archives all 100% progress tasks in a column. |
| `PurgeArchivedTasks()` | Permanently deletes all archived cards. |

Blocked policy behavior:

- Blocked cards cannot move forward to later columns or into the archive column.
- Blocked cards can still be edited and moved backward or reordered within a column.
- Progress is capped at 99% while blocked; unblocking restores normal progress updates.

### Lane Operations (Swimlanes)

| Method | Description |
| --- | --- |
| `AddLane(title)` | Appends a new lane to the board. |
| `InsertLane(title, index)` | Inserts a lane at a specific position. |
| `RemoveLane(lane, fallback?)` | Deletes lane and reassigns tasks to fallback. |
| `MoveLane(lane, newIndex)` | Rearranges lane order. |
| `GetTasksForLane(lane)` | Returns all tasks assigned to the lane. |
| `SetTaskLane(task, lane)` | Assigns or clears a task's lane. |

### Selection & Bulk Operations

| Method | Description |
| --- | --- |
| `SelectTask(task)` | Adds task to selection. |
| `DeselectAll()` | Clears current selection. |
| `BulkMove(targetColumn)` | Moves all selected tasks (with events). |
| `BulkSetPriority(priority)` | Updates priority for all selected tasks. |
| `BulkSetBlocked(blocked, ...)` | Blocks/Unblocks selected items. |
| `BulkArchive()` | Archives all selected tasks. |
| `BulkDelete()` | Permanently removes selection. |

### Subtask Management

| Method | Description |
| --- | --- |
| `AddSubtask(task, title, isCompleted?)` | Adds a subtask. |
| `RemoveSubtask(task, subtask)` | Removes a subtask. |
| `ToggleSubtask(subtask)` | Toggles subtask completion. |
| `SetAllSubtasksCompleted(task, isCompleted)` | Sets all subtasks' completion status. |
| `ClearSubtasks(task)` | Clears all subtasks from a task. |
| `GetSubtaskProgress(task)` | Returns `(completed, total)` tuple. |

### Zoom & Size

| Method | Description |
| --- | --- |
| `ZoomIn()` | Increases board size tier. |
| `ZoomOut()` | Decreases board size tier. |
| `SetZoom(DaisySize)` | Sets a specific size tier. |

### Board Operations

| Method | Description |
| --- | --- |
| `ClearBoard()` | Clears all columns and tasks. |
| `ResetBoard(columnTitles)` | Resets with specified default columns. |
| `CreateDefaultBoard()` | Creates "To Do", "In Progress", "Done" columns. |
| `GetBoardData()` | Returns the `FlowKanbanData` model. |
| `SetBoardData(data)` | Replaces the board with new data. |
| `ExportToJson()` | Exports board to JSON string. |
| `ImportFromJson(json)` | Imports board from JSON string. |
| `GetStatistics()` | Returns `BoardStatistics` with counts. |

### BoardStatistics Record

| Property | Type | Description |
| --- | --- | --- |
| `ColumnCount` | `int` | Number of columns. |
| `TotalTaskCount` | `int` | Total tasks across all columns. |
| `TasksPerColumn` | `Dictionary<string, int>` | Task count grouped by column title. |
| `EmptyColumns` | `int` | Number of columns with no tasks. |

### Example: Programmatic Board Setup

```csharp
var manager = new FlowKanbanManager(ProjectBoard);

// Create default structure
manager.CreateDefaultBoard();

// Add some tasks
var backlog = manager.FindColumnByTitle("To Do");
manager.AddTask(backlog, "Implement auth", "OAuth2 integration", DaisyPalette.Primary);
manager.AddTask(backlog, "Write tests", null, DaisyPalette.Info);

// Move a task
var inProgress = manager.FindColumnByTitle("In Progress");
var task = manager.FindTaskById("some-id");
manager.MoveTask(task, inProgress);

// Export for backup
var json = manager.ExportToJson();
```

## Accessibility & Keyboard Navigation

The board uses WinUI 3 focus APIs, keyboard events, and UI Automation peers.

### Focus Zones

1. **Board Header**: title actions, tags, search input.
2. **Board Surface**: columns, cards, and the inline "Add Card" control.
3. **Sidebar**: compact action buttons (home, settings, undo/redo, zoom, save/load, metrics/help).
4. **Status Bar**: informational only (not in tab order).

### Tab Navigation

- Tab/Shift+Tab moves through focusable controls in header, board surface, and sidebar.
- Cards and "Add Card" are focusable so screen readers can announce content in context.
- Enter opens the focused card editor. Space toggles the focused card in the task selection.

### Touch and Pen

As in `DaisyScheduler` and the apps of the platforms, a tap opens a card's editor, and holding a card selects it; while cards are selected, a tap adds a card to the selection or takes it out.
Taps are decided on release and only those that did not move count, so swipes over a card scroll the board and never open or select it.
After the hold, the finger drags the card as a mouse does (`CanDrag`).
A tap or click on a free place of the board (not on a card or a control) clears the selection; a swipe or a mouse drag there does not.
The mouse keeps click, Ctrl-click, Shift-click and double click.

### Arrow-Key Navigation (Board Surface)

- **Column header focus**: Left/Right moves to adjacent column header. Down enters the first visible card, or the add-card control if empty.
- **Card focus**: Up/Down moves within the same column; Left/Right moves to the adjacent column at the nearest card index.
- **Add-card focus**: Up moves to the last visible card; Left/Right moves to the add-card control in adjacent columns.
- **Swimlane layout**: Left/Right moves across columns within the same lane row; Up/Down moves within the same column/lane list.

### Keyboard Shortcuts (Board View)

- `Ctrl + B` → Add column (append).
- `Ctrl + D` → Delete last column (confirmation rules apply).
- `Ctrl + T` → Open the active column editor (defaults to the last column).
- `Ctrl + N` → Create card in active column (uses inline add).
- `Ctrl + F` → Focus the search input.
- `Ctrl + Alt + Left/Right` → Switch active column (for rename/add).
- `Ctrl + Shift + Up/Down` → Move the focused card within its column.
- `Ctrl + Shift + Left/Right` → Move the focused card to an adjacent column.
- `Ctrl + +` / `Ctrl + -` → Zoom in/out.
- `Ctrl` + mouse wheel → Zoom in/out, one size tier per notch, also from the parts of a notch that precision wheels send.

The column shortcuts are available to everyone; the host app decides who may use the board.

### Pinch to Zoom

On a touch screen and on a precision touchpad, spreading two fingers zooms the board in and pinching them zooms it out, one size tier per quarter of their distance, within the same tiers as the zoom buttons (`ExtraSmall` to `ExtraLarge`). On a touch screen the board changes its tier while the fingers move. WinUI gives a touchpad pinch only to the board's scroll viewer, so the board scales while the fingers move and takes the nearest tier when they lift. A pinch neither opens, selects nor drags a card, and the board does not scroll with the fingers. One finger works as before.

### Accessibility Metadata

- Columns, cards, and action buttons expose `AutomationProperties.Name`.
- Cards expose `AutomationProperties.AutomationId` via task ID for stable testing hooks.
- Buttons and icon-only controls use their localized tooltip string as the UIA name.

### UI Automation Selection and Invocation

`FlowKanban` exposes the Selection pattern as a list. It supports multiple selected tasks and does not require a selection.
`GetSelection()` returns providers for all selected tasks, including tasks without a realized card visual.
These offscreen providers expose the task title, task ID, selected state, and board selection container.

Realized `FlowTaskCard` controls expose the Invoke and SelectionItem patterns as list items.
Invoke opens the card editor through the existing edit command.
`Select()` clears the other selections and selects the task.
`AddToSelection()` adds the task to the selection. `RemoveFromSelection()` removes it.
The selected state follows `FlowTask.IsSelected`.
Offscreen task providers support the same selection methods without a card visual.

### Native Windows Focus

The control uses WinUI 3 focus APIs and keyboard events.
No backend feature switch is required.
