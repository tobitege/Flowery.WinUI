using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Flowery.WinUI.Kanban.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // An edit starts a debounced auto-save. An edit still waiting for it is saved at once when the board unloads, when
    // its window closes, before a board loads and when Board or BoardStore changes, on the board it was made on and into
    // its store. Before, the timer fired later into whatever board, store or state storage was current then.
    [TestClass]
    public class Given_KanbanAutoSave
    {
        // A board whose ID is no GUID gets one when it is saved, which is an edit of its own; it must not start the
        // auto-save again after the pending one was saved.
        private const string ReadableBoardId = "board-1";

        [TestMethod]
        public async Task When_BoardUnloadsWithAPendingAutoSave_TheEditIsSavedAtOnce()
        {
            foreach (var (name, onHomeView, boardId) in new (string, bool, string?)[]
                     {
                         ("board view", false, null),
                         ("home view", true, null),
                         ("home view, board ID without GUID", true, ReadableBoardId)
                     })
            {
                await RuntimeTestHelpers.RunCaseAsync(name, () => RuntimeTestHelpers.WithStateStorageAsync(async () =>
                {
                    var edited = CreateBoard(id: boardId);
                    var store = new CountingBoardStore();
                    var kanban = await ShowAsync(edited, store);
                    try
                    {
                        edited.Title = "Edited";
                        var timer = AutoSaveTimer(kanban);
                        // The user leaves for the home view before the auto-save came, then the board leaves the window.
                        if (onHomeView)
                            kanban.CurrentView = FlowKanbanView.Home;
                        Assert.IsTrue(timer?.IsRunning, "the edit did not start an auto-save");
                        Assert.IsEmpty(store.SavedBoards);
                        Assert.AreEqual(boardId ?? edited.Id, edited.Id, "the board got its ID before the flush");

                        await RuntimeTestHelpers.DetachFromHostAsync(kanban);

                        CollectionAssert.AreEqual(new[] { edited }, store.SavedBoards);
                        Assert.IsFalse(timer!.IsRunning);
                        // The save comes before the home view clears the last board, so the next start opens the home view.
                        Assert.AreEqual(onHomeView ? null : edited.Id, kanban.LastBoardId);
                    }
                    finally
                    {
                        await RuntimeTestHelpers.DetachFromHostAsync(kanban);
                    }
                }));
            }
        }

        [TestMethod]
        public async Task When_TheWindowClosesWithAPendingAutoSave_TheEditIsSaved()
        {
            await RuntimeTestHelpers.WithStateStorageAsync(async () =>
            {
                var edited = CreateBoard();
                var store = new CountingBoardStore();
                var kanban = new FlowKanban { Board = edited, BoardStore = store, Width = 800, Height = 500 };
                var window = new Window { Content = kanban };
                var closed = false;
                try
                {
                    window.Activate();
                    await RuntimeTestHelpers.EnsureLoadedAsync(kanban).WaitAsync(RuntimeTestHelpers.LayoutTimeout);
                    OpenBoardView(kanban, store);

                    edited.Title = "Edited";
                    var timer = AutoSaveTimer(kanban);
                    Assert.IsTrue(timer?.IsRunning, "the edit did not start an auto-save");

                    var unloaded = RuntimeTestHelpers.WaitForUnloadedAsync(kanban);
                    window.Close();
                    closed = true;

                    // Closing the last window ends the app, so the edit is saved while Close runs, not on a later tick.
                    CollectionAssert.AreEqual(new[] { edited }, store.SavedBoards, "the edit was not saved while the window closed");
                    Assert.IsFalse(timer!.IsRunning);
                    await unloaded.WaitAsync(RuntimeTestHelpers.LayoutTimeout);
                }
                finally
                {
                    if (!closed)
                        window.Close();
                }
            });
        }

        [TestMethod]
        public async Task When_BoardOrStoreChangesWithAPendingAutoSave_TheEditGoesToItsBoardAndStore()
        {
            foreach (var (name, replaceStore, boardId) in new (string, bool, string?)[]
                     {
                         ("board", false, null),
                         ("board with an ID without GUID", false, ReadableBoardId),
                         ("store", true, null)
                     })
            {
                await RuntimeTestHelpers.RunCaseAsync(name, () => RuntimeTestHelpers.WithStateStorageAsync(async () =>
                {
                    var edited = CreateBoard(id: boardId);
                    var store = new CountingBoardStore();
                    var kanban = await ShowAsync(edited, store);
                    try
                    {
                        var lastBoardId = kanban.LastBoardId;
                        edited.Title = "Edited";
                        var timer = AutoSaveTimer(kanban);
                        Assert.IsTrue(timer?.IsRunning, "the edit did not start an auto-save");
                        Assert.AreEqual(boardId ?? edited.Id, edited.Id, "the board got its ID before the flush");

                        var otherStore = new CountingBoardStore();
                        if (replaceStore)
                            kanban.BoardStore = otherStore;
                        else
                            kanban.Board = CreateBoard("Other");

                        CollectionAssert.AreEqual(new[] { edited }, store.SavedBoards);
                        Assert.IsEmpty(otherStore.SavedBoards);
                        Assert.IsFalse(timer!.IsRunning);
                        // A replaced board is no longer open, so it does not become the last board; with a new store it
                        // stays open.
                        Assert.AreEqual(replaceStore ? edited.Id : lastBoardId, kanban.LastBoardId);
                    }
                    finally
                    {
                        await RuntimeTestHelpers.DetachFromHostAsync(kanban);
                    }
                }));
            }
        }

        [TestMethod]
        public async Task When_OpenBoardIsDeletedWithAPendingAutoSave_ItIsNotSavedBack()
        {
            await RuntimeTestHelpers.WithStateStorageAsync(async () =>
            {
                var edited = CreateBoard();
                var store = new CountingBoardStore();
                var kanban = await ShowAsync(edited, store);
                try
                {
                    edited.Title = "Edited";
                    var timer = AutoSaveTimer(kanban);
                    Assert.IsTrue(timer?.IsRunning, "the edit did not start an auto-save");

                    Assert.IsTrue(kanban.TryDeleteBoard(edited.Id, out var error), error?.Message);

                    Assert.AreNotSame(edited, kanban.Board);
                    Assert.IsEmpty(store.SavedBoards);
                    Assert.IsFalse(timer!.IsRunning);
                }
                finally
                {
                    await RuntimeTestHelpers.DetachFromHostAsync(kanban);
                }
            });
        }

        [TestMethod]
        public async Task When_OpenBoardIsReopenedWithAPendingAutoSave_TheReopenedBoardHasTheEdit()
        {
            foreach (var throughHome in new[] { false, true })
            {
                await RuntimeTestHelpers.RunCaseAsync(throughHome ? "home view" : "TryLoadBoard", () => RuntimeTestHelpers.WithStateStorageAsync(async () =>
                {
                    var edited = CreateBoard();
                    var kanban = await ShowAsync(edited, new FlowKanbanBoardStore(StateStorageProvider.Instance));
                    try
                    {
                        Assert.IsTrue(kanban.TrySaveBoard(out var saveError), saveError?.Message);
                        edited.Title = "Edited";
                        Assert.IsTrue(AutoSaveTimer(kanban)?.IsRunning, "the edit did not start an auto-save");

                        // Before, the load read the stored copy without the edit, and the edit was saved only afterwards.
                        if (throughHome)
                        {
                            kanban.CurrentView = FlowKanbanView.Home;
                            kanban.OpenBoardCommand.Execute(new FlowBoardMetadata { Id = edited.Id, Title = "Board" });
                            await RuntimeTestHelpers.WaitUntilAsync(() => !ReferenceEquals(kanban.Board, edited), "the board is reopened");
                        }
                        else
                        {
                            Assert.IsTrue(kanban.TryLoadBoard(edited.Id, out var loadError), loadError?.Message);
                        }

                        Assert.AreNotSame(edited, kanban.Board);
                        Assert.AreEqual(edited.Id, kanban.Board.Id);
                        Assert.AreEqual("Edited", kanban.Board.Title);
                    }
                    finally
                    {
                        await RuntimeTestHelpers.DetachFromHostAsync(kanban);
                    }
                }));
            }
        }

        private static FlowKanbanData CreateBoard(string title = "Board", string? id = null)
        {
            var board = new FlowKanbanData { Title = title, Columns = { new FlowKanbanColumnData { Title = "Todo", Tasks = { new FlowTask { Title = "Task" } } } } };
            if (id is not null)
                board.Id = id;
            return board;
        }

        private static async Task<FlowKanban> ShowAsync(FlowKanbanData board, IBoardStore store)
        {
            var kanban = new FlowKanban { Board = board, BoardStore = store };
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            OpenBoardView(kanban, store);
            return kanban;
        }

        private static void OpenBoardView(FlowKanban kanban, IBoardStore store)
        {
            // With no saved boards, OnKanbanLoaded switches to the Home view.
            kanban.CurrentView = FlowKanbanView.Board;
            // A board that an earlier test left in the host can unload only after this test's storage is set and save its
            // settings there, with auto-save off, before this board loads them.
            kanban.AutoSaveAfterEdits = true;
            if (store is CountingBoardStore counting)
                counting.SavedBoards.Clear();
        }

        private static DispatcherQueueTimer? AutoSaveTimer(FlowKanban kanban) =>
            (DispatcherQueueTimer?)typeof(FlowKanban).GetField("_autoSaveTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(kanban);

        private sealed class CountingBoardStore : IBoardStore
        {
            public List<FlowKanbanData> SavedBoards { get; } = [];

            public IReadOnlyList<FlowBoardMetadata> ListBoards() => Array.Empty<FlowBoardMetadata>();

            public bool TryLoadBoard(string boardId, out FlowKanbanData? board, out Exception? error)
            {
                board = null;
                error = null;
                return false;
            }

            public bool TrySaveBoard(FlowKanbanData board, out Exception? error)
            {
                SavedBoards.Add(board);
                error = null;
                return true;
            }

            public bool TryDeleteBoard(string boardId, out Exception? error)
            {
                error = null;
                return true;
            }

            public bool TryExportBoard(string boardId, out string? json, out Exception? error)
            {
                json = null;
                error = null;
                return false;
            }
        }
    }
}
