using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_KanbanBehavior
    {
        [TestMethod]
        public async Task When_SearchText_Changes_TaskMatchesUpdate()
        {
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData
                {
                    Columns =
                    {
                        new FlowKanbanColumnData
                        {
                            Title = "Backlog",
                            Tasks =
                            {
                                new FlowTask { Title = "Fix bug", Description = "urgent issue", Tags = "urgent" },
                                new FlowTask { Title = "Write docs", Description = "guides", Tags = "documentation" }
                            }
                        }
                    }
                }
            };

            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);

            kanban.SearchText = "bug";

            var tasks = kanban.Board.Columns.SelectMany(column => column.Tasks).ToList();
            Assert.AreEqual(2, tasks.Count);
            Assert.IsTrue(tasks[0].IsSearchMatch);
            Assert.IsFalse(tasks[1].IsSearchMatch);
        }

        [TestMethod]
        public async Task When_Grouping_ByLane_UpdatesLaneRows()
        {
            var lane = new FlowKanbanLane { Id = "lane-1", Title = "Lane 1" };
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData
                {
                    GroupBy = FlowKanbanGroupBy.Lane,
                    Lanes = { lane },
                    Columns =
                    {
                        new FlowKanbanColumnData
                        {
                            Title = "Column",
                            Tasks =
                            {
                                new FlowTask { Title = "Assigned", LaneId = lane.Id },
                                new FlowTask { Title = "Unassigned", LaneId = null }
                            }
                        }
                    }
                }
            };

            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);

            Assert.IsTrue(kanban.IsSwimlaneLayoutEnabled);
            Assert.IsFalse(kanban.IsStandardLayoutEnabled);
            Assert.AreEqual(2, kanban.LaneRows.Count);
        }

        [TestMethod]
        public void When_WipLimit_Exceeded_MoveResultReflectsPolicy()
        {
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData
                {
                    Columns =
                    {
                        new FlowKanbanColumnData
                        {
                            Title = "Todo",
                            Tasks = { new FlowTask { Title = "Task A" } }
                        },
                        new FlowKanbanColumnData
                        {
                            Title = "Doing",
                            WipLimit = 1,
                            Tasks = { new FlowTask { Title = "Task B" } }
                        }
                    }
                }
            };

            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            var source = kanban.Board.Columns[0];
            var target = kanban.Board.Columns[1];
            var task = source.Tasks[0];

            var blocked = manager.TryMoveTaskWithWipEnforcement(task, target, enforceHard: true);
            Assert.AreEqual(MoveResult.BlockedByWip, blocked);
            Assert.AreEqual(1, target.Tasks.Count);

            var warning = manager.TryMoveTaskWithWipEnforcement(task, target, enforceHard: false);
            Assert.AreEqual(MoveResult.AllowedWithWipWarning, warning);
            Assert.AreEqual(2, target.Tasks.Count);
        }

        [TestMethod]
        public void When_Statistics_Handle_DuplicateColumnTitles()
        {
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData
                {
                    Columns =
                    {
                        new FlowKanbanColumnData
                        {
                            Title = "Todo",
                            Tasks = { new FlowTask { Title = "A" } }
                        },
                        new FlowKanbanColumnData
                        {
                            Title = "Todo",
                            Tasks = { new FlowTask { Title = "B" }, new FlowTask { Title = "C" } }
                        }
                    }
                }
            };

            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            var stats = manager.GetStatistics();

            Assert.AreEqual(2, stats.ColumnCount);
            Assert.IsTrue(stats.TasksPerColumn.ContainsKey("Todo"));
            Assert.AreEqual(3, stats.TasksPerColumn["Todo"]);
        }

        [TestMethod]
        public void When_UndoRedo_MoveTask_RestoresState()
        {
            var task = new FlowTask { Title = "Task A" };
            var source = new FlowKanbanColumnData { Title = "Todo", Tasks = { task } };
            var target = new FlowKanbanColumnData { Title = "Doing" };
            var kanban = new FlowKanban
            {
                EnableUndoRedo = true,
                Board = new FlowKanbanData
                {
                    Columns = { source, target }
                }
            };

            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            var result = manager.TryMoveTaskWithWipEnforcement(task, target, 0, enforceHard: false);
            Assert.AreEqual(MoveResult.Success, result);
            Assert.IsFalse(source.Tasks.Contains(task));
            Assert.IsTrue(target.Tasks.Contains(task));

            Assert.IsTrue(kanban.UndoCommand.CanExecute(null));
            kanban.UndoCommand.Execute(null);
            Assert.IsTrue(source.Tasks.Contains(task));
            Assert.IsFalse(target.Tasks.Contains(task));

            Assert.IsTrue(kanban.RedoCommand.CanExecute(null));
            kanban.RedoCommand.Execute(null);
            Assert.IsFalse(source.Tasks.Contains(task));
            Assert.IsTrue(target.Tasks.Contains(task));
        }

        [TestMethod]
        public void When_ArchiveAndUnarchive_Task_ReturnsToSource()
        {
            var task = new FlowTask { Title = "Archived Task" };
            var source = new FlowKanbanColumnData { Title = "Doing", Tasks = { task } };
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData
                {
                    Columns = { source }
                }
            };

            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            manager.ArchiveTask(task);

            var archiveColumn = manager.GetArchiveColumn();
            Assert.IsNotNull(archiveColumn);
            Assert.IsTrue(task.IsArchived);
            Assert.IsTrue(archiveColumn!.Tasks.Contains(task));

            manager.UnarchiveTask(task);
            Assert.IsFalse(task.IsArchived);
            Assert.IsTrue(source.Tasks.Contains(task));
        }

        [TestMethod]
        public void When_MovingTask_WithTargetLane_LaneIdUpdates()
        {
            var lane = new FlowKanbanLane { Id = "lane-1", Title = "Lane 1" };
            var task = new FlowTask { Title = "Task A" };
            var source = new FlowKanbanColumnData { Title = "Todo", Tasks = { task } };
            var target = new FlowKanbanColumnData { Title = "Doing" };
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData
                {
                    GroupBy = FlowKanbanGroupBy.Lane,
                    Lanes = { lane },
                    Columns = { source, target }
                }
            };

            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            var result = manager.TryMoveTaskWithWipEnforcement(task, target, 0, lane.Id, enforceHard: false);

            Assert.AreEqual(MoveResult.Success, result);
            Assert.AreEqual(lane.Id, task.LaneId);
            Assert.IsTrue(target.Tasks.Contains(task));

            var back = manager.TryMoveTaskWithWipEnforcement(task, source, 0, FlowKanban.UnassignedLaneId, enforceHard: false);
            Assert.AreEqual(MoveResult.Success, back);
            Assert.IsNull(task.LaneId);
            Assert.IsTrue(source.Tasks.Contains(task));
        }

        [TestMethod]
        public async Task When_CalculatingDropIndex_PositionsMapToIndices()
        {
            var tasks = new[]
            {
                new FlowTask { Title = "First" },
                new FlowTask { Title = "Second" },
                new FlowTask { Title = "Third" }
            };
            var column = new FlowKanbanColumnData { Title = "Todo" };
            foreach (var task in tasks)
            {
                column.Tasks.Add(task);
            }

            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData { Columns = { column } }
            };

            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);

            // With no saved boards, OnKanbanLoaded switches to the Home view, which hides the columns.
            kanban.CurrentView = FlowKanbanView.Board;

            FlowKanbanColumn? columnControl = null;
            Microsoft.UI.Xaml.Controls.ListView? tasksListView = null;
            Microsoft.UI.Xaml.Controls.ListViewItem? lastContainer = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                kanban.UpdateLayout();
                columnControl = FindDescendant<FlowKanbanColumn>(kanban, c => c.ColumnData == column);
                tasksListView = columnControl != null
                    ? FindDescendant<Microsoft.UI.Xaml.Controls.ListView>(columnControl, lv => lv.Name == "PART_TasksItemsControl")
                    : null;
                lastContainer = tasksListView?.ContainerFromIndex(2) as Microsoft.UI.Xaml.Controls.ListViewItem;

                if (lastContainer is { ActualHeight: > 0 })
                {
                    break;
                }

                await Task.Delay(100);
            }

            Assert.IsNotNull(columnControl, "Column control was not realized.");
            Assert.IsNotNull(tasksListView, "Tasks list was not realized.");
            Assert.IsNotNull(lastContainer, "Task containers were not realized.");

            // Above the first card: insert at the top.
            Assert.AreEqual(0, columnControl!.CalculateInsertIndex(0, out _));

            // Below the midpoint of the last card: insert at the end.
            var lastTop = lastContainer!
                .TransformToVisual(tasksListView)
                .TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            var belowLastMidpoint = lastTop + lastContainer.ActualHeight;
            Assert.AreEqual(3, columnControl.CalculateInsertIndex(belowLastMidpoint, out _));

            // Far beyond all realized cards: clamped to the task count.
            Assert.AreEqual(3, columnControl.CalculateInsertIndex(100000, out _));
        }

        private static T? FindDescendant<T>(Microsoft.UI.Xaml.DependencyObject root, Func<T, bool>? predicate = null)
            where T : Microsoft.UI.Xaml.DependencyObject
        {
            var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T match && (predicate == null || predicate(match)))
                    return match;

                var found = FindDescendant(child, predicate);
                if (found != null)
                    return found;
            }

            return default;
        }

        [TestMethod]
        public void When_PersistingOrMigratingABoard_ItLoadsBack()
        {
            foreach (var (name, load) in new (string, Func<FlowKanbanData, string, FlowKanbanData?>)[]
                     {
                         ("store", SaveAndLoad),
                         ("v0 json", MigrateWithoutSchemaVersion)
                     })
            {
                var board = new FlowKanbanData
                {
                    Id = "board-1",
                    Title = "Round Trip",
                    Columns =
                    {
                        new FlowKanbanColumnData
                        {
                            Title = "Todo",
                            Tasks = { new FlowTask { Title = "Saved Task" } }
                        }
                    }
                };

                var loaded = load(board, name);

                Assert.IsNotNull(loaded, name);
                Assert.AreEqual(board.Id, loaded!.Id, name);
                Assert.AreEqual(board.Title, loaded.Title, name);
                Assert.AreEqual(1, loaded.Columns.Count, name);
                Assert.AreEqual("Todo", loaded.Columns[0].Title, name);
                Assert.AreEqual(1, loaded.Columns[0].Tasks.Count, name);
            }
        }

        private static FlowKanbanData? SaveAndLoad(FlowKanbanData board, string name)
        {
            var store = new FlowKanbanBoardStore(new InMemoryStateStorage());
            Assert.IsTrue(store.TrySaveBoard(board, out var saveError), name);
            Assert.IsNull(saveError, name);
            Assert.IsTrue(store.TryLoadBoard(board.Id, out var loaded, out var loadError), name);
            Assert.IsNull(loadError, name);
            return loaded;
        }

        // A board saved before schema versions has no schemaVersion property.
        private static FlowKanbanData? MigrateWithoutSchemaVersion(FlowKanbanData board, string name)
        {
            var json = JsonSerializer.Serialize(board, FlowKanbanJsonContext.Default.FlowKanbanData);
            var root = JsonNode.Parse(json) as JsonObject;
            Assert.IsNotNull(root, name);
            root.Remove("schemaVersion");
            return FlowKanbanMigration.MigrateFromJson(root.ToJsonString());
        }
    }
}
