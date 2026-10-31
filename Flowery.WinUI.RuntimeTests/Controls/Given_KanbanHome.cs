using System.Linq;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // The home view wires up every board tile when its ItemsRepeater prepares it: a tap on the tile opens the board, and
    // the tile menu has its texts and commands, also after the board is attached again. Flowery.NET looked a tile up
    // before its item template had built it.
    [TestClass]
    public class Given_KanbanHome
    {
        private static readonly string[] MenuKeys =
            ["Kanban_Home_Open", "Kanban_Home_Rename", "Kanban_Home_Duplicate", "Kanban_Home_Export", "Kanban_Home_Delete"];

        [TestMethod]
        public Task When_ABoardTileIsTapped_TheBoardOpensAndItsMenuHasEveryEntry() => WithHomeAsync(async (kanban, boardId) =>
        {
            foreach (var attachedAgain in new[] { false, true })
            {
                await RuntimeTestHelpers.RunCaseAsync(attachedAgain ? "attached again" : "first", async () =>
                {
                    kanban.CurrentView = FlowKanbanView.Home;
                    if (attachedAgain)
                    {
                        await RuntimeTestHelpers.DetachFromHostAsync(kanban);
                        RuntimeTestHelpers.AttachToHost(kanban);
                        await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
                    }

                    var tile = await TileAsync(kanban, boardId);
                    var menuButton = tile.FindName("PART_BoardMenuButton") as DaisyButton;
                    Assert.IsNotNull(menuButton, "the tile has no menu button");
                    var flyout = menuButton.Flyout as MenuFlyout;
                    Assert.IsNotNull(flyout, "the menu button has no menu");
                    var items = flyout.Items.OfType<DaisyMenuFlyoutItem>().ToList();
                    CollectionAssert.AreEqual(MenuKeys, items.Select(static item => item.LocalizationKey).ToList());
                    foreach (var item in items)
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(item.Text), $"{item.LocalizationKey} has no text");
                        Assert.IsNotNull(item.Command, $"{item.LocalizationKey} has no command");
                        Assert.AreEqual(boardId, (item.CommandParameter as FlowBoardMetadata)?.Id, $"{item.LocalizationKey} has no board");
                    }

                    // The view handles taps on this tile's open surface, and a tap there opens the board.
                    var surface = tile.FindName("PART_BoardOpenSurface") as FrameworkElement;
                    Assert.IsNotNull(surface, "the tile has no open surface");
                    var home = RuntimeTestHelpers.Descendants<FlowKanbanHome>(kanban).Single();
                    var before = kanban.Board;
                    Assert.IsTrue(home.TapBoardSurface(surface), "the home view handles no tap on the tile");
                    await RuntimeTestHelpers.WaitUntilAsync(
                        () => kanban.CurrentView == FlowKanbanView.Board && !ReferenceEquals(kanban.Board, before) && kanban.Board.Id == boardId,
                        "the tapped tile opens its board");
                });
            }
        });

        // A template applied again brings a new repeater and new tiles; the view lets the old tiles go.
        [TestMethod]
        public Task When_TheHomeTemplateIsAppliedAgain_OnlyTheNewTilesTakeTaps() => WithHomeAsync(async (kanban, boardId) =>
        {
            kanban.CurrentView = FlowKanbanView.Home;
            var oldSurface = (FrameworkElement)(await TileAsync(kanban, boardId)).FindName("PART_BoardOpenSurface");
            var home = RuntimeTestHelpers.Descendants<FlowKanbanHome>(kanban).Single();

            var template = home.Template;
            home.Template = null;
            home.UpdateLayout();
            home.Template = template;
            home.UpdateLayout();

            var newSurface = (FrameworkElement)(await TileAsync(kanban, boardId)).FindName("PART_BoardOpenSurface");
            Assert.AreNotSame(oldSurface, newSurface, "the template built no new tile");
            Assert.IsFalse(home.TapBoardSurface(oldSurface), "the view still handles taps on a tile of the old template");
            Assert.IsTrue(home.TapBoardSurface(newSurface), "the view handles no tap on the new tile");
            await RuntimeTestHelpers.WaitUntilAsync(
                () => kanban.CurrentView == FlowKanbanView.Board && kanban.Board.Id == boardId, "the tapped tile opens its board");
        });

        // Runs the body on a loaded Kanban with one saved board, in a state storage of its own, and detaches the Kanban after.
        private static Task WithHomeAsync(Func<FlowKanban, string, Task> body) => RuntimeTestHelpers.WithStateStorageAsync(async () =>
        {
            var store = new FlowKanbanBoardStore(StateStorageProvider.Instance);
            var saved = new FlowKanbanData { Title = "Saved", Columns = { new FlowKanbanColumnData { Title = "Todo" } } };
            Assert.IsTrue(store.TrySaveBoard(saved, out var saveError), saveError?.Message);
            // A fixed size keeps the tiles inside the runner window.
            var kanban = new FlowKanban
            {
                BoardStore = store, Width = 1000, Height = 600,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
            };
            RuntimeTestHelpers.AttachToHost(kanban);
            try
            {
                await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
                await body(kanban, saved.Id);
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(kanban);
            }
        });

        // The home view lays its tiles out over several passes after it is shown, so a tile counts only once the same tile
        // stays in place for a pass.
        private static async Task<FrameworkElement> TileAsync(FlowKanban kanban, string boardId)
        {
            FrameworkElement? previous = null;
            Windows.Foundation.Point? previousPosition = null;
            for (var pass = 0; pass < 50; pass++)
            {
                await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
                var tile = RuntimeTestHelpers.Descendants<FrameworkElement>(kanban).FirstOrDefault(element =>
                    element.DataContext is FlowBoardMetadata board && board.Id == boardId
                    && element.FindName("PART_BoardOpenSurface") is FrameworkElement { ActualWidth: > 0 });
                var position = tile?.TransformToVisual(kanban).TransformPoint(default);
                if (tile is not null && ReferenceEquals(tile, previous) && position == previousPosition)
                    return tile;
                previous = tile;
                previousPosition = position;
            }

            Assert.Fail("The home view does not show the board tile in place.");
            return null!;
        }
    }
}
