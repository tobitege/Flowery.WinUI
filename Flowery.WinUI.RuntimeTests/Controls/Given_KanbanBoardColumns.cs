using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // The status bar shows ColumnCount, which follows the columns and a new column collection. A new collection also
    // reaches the board, which rebuilds its columns on the Columns change.
    [TestClass]
    public class Given_KanbanBoardColumns
    {
        [TestMethod]
        public void When_ColumnsChangeOrAreReplaced_ColumnCountFollows()
        {
            var board = new FlowKanbanData();
            List<string?> changes = [];
            board.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

            board.Columns.Add(new FlowKanbanColumnData { Title = "Todo" });
            Assert.AreEqual(1, board.ColumnCount);
            CollectionAssert.AreEqual(new[] { nameof(FlowKanbanData.ColumnCount) }, changes, "an added column");

            var old = board.Columns;
            changes.Clear();
            board.Columns = [new FlowKanbanColumnData { Title = "A" }, new FlowKanbanColumnData { Title = "B" }];
            Assert.AreEqual(2, board.ColumnCount);
            CollectionAssert.AreEqual(new[] { nameof(FlowKanbanData.Columns), nameof(FlowKanbanData.ColumnCount) }, changes, "a new collection");

            changes.Clear();
            old.Add(new FlowKanbanColumnData { Title = "Gone" });
            Assert.IsEmpty(changes, "the board still follows the replaced collection");
        }

        [TestMethod]
        public Task When_TheBoardGetsANewColumnCollection_TheBoardShowsIt() => RuntimeTestHelpers.WithStateStorageAsync(async () =>
        {
            // An empty state storage of its own and the board view keep the saved boards of other runs out of the test; a
            // fixed width keeps the standard layout, which the board picks only after a layout pass.
            var board = new FlowKanbanData { Columns = { new FlowKanbanColumnData { Title = "Todo" } } };
            var kanban = new FlowKanban
            {
                AutoSaveAfterEdits = false, Board = board, Width = 1000, Height = 600,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
            };
            RuntimeTestHelpers.AttachToHost(kanban);
            try
            {
                await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
                kanban.CurrentView = FlowKanbanView.Board;
                await RuntimeTestHelpers.WaitUntilAsync(() => ReferenceEquals(kanban.StandardColumnsSource, board.Columns), "the board shows its columns");

                var columns = new ObservableCollection<FlowKanbanColumnData> { new() { Title = "A" }, new() { Title = "B" } };
                board.Columns = columns;
                Assert.AreSame(columns, kanban.StandardColumnsSource, "the board still shows the replaced columns");
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(kanban);
            }
        });
    }
}
