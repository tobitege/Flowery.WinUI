using System.Linq;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // The home view wires up every board tile when its ItemsRepeater prepares it: a tap on the tile opens the board, and
    // the tile menu has its texts and commands. Flowery.NET looked a tile up before its item template had built it.
    [TestClass]
    public class Given_KanbanHome
    {
        private static readonly string[] MenuKeys =
            ["Kanban_Home_Open", "Kanban_Home_Rename", "Kanban_Home_Duplicate", "Kanban_Home_Export", "Kanban_Home_Delete"];

        [TestMethod]
        public async Task When_ABoardTileIsTapped_TheBoardOpensAndItsMenuHasEveryEntry()
        {
            await RuntimeTestHelpers.WithStateStorageAsync(async () =>
            {
                var store = new FlowKanbanBoardStore(StateStorageProvider.Instance);
                var saved = new FlowKanbanData { Title = "Saved", Columns = { new FlowKanbanColumnData { Title = "Todo" } } };
                Assert.IsTrue(store.TrySaveBoard(saved, out var saveError), saveError?.Message);
                // A fixed size keeps the tiles inside the runner window, where the finger can reach them.
                var kanban = new FlowKanban
                {
                    BoardStore = store, Width = 1000, Height = 600,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
                };
                RuntimeTestHelpers.AttachToHost(kanban);
                try
                {
                    await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
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

                            var tile = await TileAsync(kanban, saved.Id);
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
                                Assert.AreEqual(saved.Id, (item.CommandParameter as FlowBoardMetadata)?.Id, $"{item.LocalizationKey} has no board");
                            }

                            // Above the title row, away from the menu button.
                            var surface = tile.FindName("PART_BoardOpenSurface") as FrameworkElement;
                            Assert.IsNotNull(surface, "the tile has no open surface");
                            var before = kanban.Board;
                            await TouchInjection.TapAsync(surface, new Windows.Foundation.Point(surface.ActualWidth / 2, 40));
                            await RuntimeTestHelpers.WaitUntilAsync(
                                () => kanban.CurrentView == FlowKanbanView.Board && !ReferenceEquals(kanban.Board, before) && kanban.Board.Id == saved.Id,
                                "the tapped tile opens its board");
                        });
                    }
                }
                finally
                {
                    await RuntimeTestHelpers.DetachFromHostAsync(kanban);
                }
            });
        }

        // The home view lays its tiles out over several passes after it is shown, so the finger taps a tile only once the
        // same tile stays in place for a pass; otherwise it can lift on another element than it touched.
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
