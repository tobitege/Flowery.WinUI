using System;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.System;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    /// <summary>Two fingers, a touchpad pinch and Ctrl with the wheel zoom the board through its sizes.</summary>
    [TestClass]
    public class Given_KanbanPinch
    {
        [TestMethod]
        public async Task When_TwoFingersSpread_TheBoardZoomsInOneSizePerStep_UpToTheLargest()
        {
            var kanban = await CreateAsync();

            // One step is a quarter more distance; 1.2 is not yet a step.
            Pinch(kanban, from: 50, to: 60);
            Assert.AreEqual(DaisySize.Medium, kanban.BoardSize);
            Pinch(kanban, from: 50, to: 63);
            Assert.AreEqual(DaisySize.Large, kanban.BoardSize);
            // Far beyond two steps, the board stops at its largest size.
            Pinch(kanban, from: 50, to: 400);
            Assert.AreEqual(DaisySize.ExtraLarge, kanban.BoardSize);
        }

        [TestMethod]
        public async Task When_TwoFingersPinch_TheBoardZoomsOut_DownToTheSmallest()
        {
            var kanban = await CreateAsync();

            Pinch(kanban, from: 100, to: 79);
            Assert.AreEqual(DaisySize.Small, kanban.BoardSize);
            Pinch(kanban, from: 400, to: 20);
            Assert.AreEqual(DaisySize.ExtraSmall, kanban.BoardSize);
        }

        [TestMethod]
        public async Task When_APinchStartsOnACard_TheCardIsNeitherOpenedSelectedNorDragged()
        {
            var kanban = await CreateAsync();
            var task = kanban.Board.Columns[0].Tasks[0];
            var card = RuntimeTestHelpers.Descendants<FlowTaskCard>(kanban).Single(card => ReferenceEquals(card.Task, task));

            kanban.Pinch.Press(1, new Point(100, 200));
            Assert.IsFalse(kanban.IsPinchTouch, "One finger may still tap, hold or drag the card.");
            kanban.Pinch.Press(2, new Point(180, 200));
            Assert.IsTrue(kanban.IsPinchTouch, "A card must not start a drag while fingers pinch.");

            // The hold timer and the lift of the first finger come while or after the fingers pinch.
            Assert.IsFalse(card.HoldTask(), "The pinch selected the card under its first finger.");
            kanban.Pinch.Lift(2);
            kanban.Pinch.Lift(1);
            Assert.IsFalse(card.TapTask(), "The lift after the pinch opened the card.");
            Assert.IsFalse(task.IsSelected);

            // The next touch taps and holds as before.
            kanban.Pinch.Press(3, new Point(100, 200));
            Assert.IsFalse(kanban.IsPinchTouch, "The next touch may drag a card again.");
            Assert.IsTrue(card.HoldTask());
            Assert.IsTrue(task.IsSelected);
        }

        [TestMethod]
        public async Task When_CtrlAndTheWheelTurn_TheBoardChangesOneSizePerNotch_AlsoFromParts()
        {
            var kanban = await CreateAsync();

            Assert.IsFalse(kanban.ZoomByWheel(VirtualKeyModifiers.None, 120), "The wheel alone scrolls.");
            Assert.AreEqual(DaisySize.Medium, kanban.BoardSize);
            Assert.IsTrue(kanban.ZoomByWheel(VirtualKeyModifiers.Control, 120));
            Assert.AreEqual(DaisySize.Large, kanban.BoardSize);
            Assert.IsTrue(kanban.ZoomByWheel(VirtualKeyModifiers.Control, -120));
            Assert.IsTrue(kanban.ZoomByWheel(VirtualKeyModifiers.Control, -120));
            Assert.AreEqual(DaisySize.Small, kanban.BoardSize);

            // A precision wheel sends parts of a notch; together they make one size, not one size each.
            for (var part = 0; part < 3; part++)
                Assert.IsTrue(kanban.ZoomByWheel(VirtualKeyModifiers.Control, 40));
            Assert.AreEqual(DaisySize.Medium, kanban.BoardSize);
            // Turning back starts a new notch.
            Assert.IsTrue(kanban.ZoomByWheel(VirtualKeyModifiers.Control, 80));
            Assert.IsTrue(kanban.ZoomByWheel(VirtualKeyModifiers.Control, -80));
            Assert.AreEqual(DaisySize.Medium, kanban.BoardSize);
        }

        [TestMethod]
        public async Task When_ATouchpadPinchesTheBoard_TheBoardTakesTheSizesAndTheOpticalZoomGoesBack()
        {
            var kanban = await CreateAsync();
            var scroll = RuntimeTestHelpers.Descendants<ScrollViewer>(kanban).Single(static scroll => scroll.Name == "PART_BoardScroll");
            Assert.AreEqual(ZoomMode.Enabled, scroll.ZoomMode);

            // WinUI gives a touchpad pinch only to the scroll viewer's zoom; its end is a view change like this one.
            scroll.ChangeView(null, null, 1.6f, true);
            await RuntimeTestHelpers.WaitUntilAsync(() => kanban.BoardSize == DaisySize.ExtraLarge, $"the board took two sizes ({kanban.BoardSize})");
            await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(scroll.ZoomFactor - 1) < 0.001, "the optical zoom went back to 1");
            // From the largest size, the touchpad can only zoom out.
            Assert.IsTrue(scroll.MaxZoomFactor < 1.25f, $"The touchpad can zoom in by {scroll.MaxZoomFactor} beyond the largest size.");
        }

        private static async Task<FlowKanban> CreateAsync()
        {
            var kanban = new FlowKanban
            {
                Width = 900,
                Height = 600,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Board = new FlowKanbanData
                {
                    Columns =
                    {
                        new FlowKanbanColumnData { Title = "Todo", Tasks = { new FlowTask { Title = "First" }, new FlowTask { Title = "Second" } } }
                    }
                }
            };
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            // With no saved boards, OnKanbanLoaded switches to the Home view, which hides the columns.
            kanban.CurrentView = FlowKanbanView.Board;
            bool Realized() => RuntimeTestHelpers.Descendants<FlowTaskCard>(kanban).Any(card => card.LoadedBoard == kanban);
            for (var pass = 0; pass < 50 && !Realized(); pass++)
                await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
            Assert.IsTrue(Realized(), "realized cards");
            Assert.AreEqual(DaisySize.Medium, kanban.BoardSize);
            return kanban;
        }

        // Two fingers on both sides of a point, from and to that distance from it, then both lift.
        private static void Pinch(FlowKanban kanban, double from, double to)
        {
            var middle = new Point(450, 300);
            kanban.Pinch.Press(1, new Point(middle.X - from, middle.Y));
            kanban.Pinch.Press(2, new Point(middle.X + from, middle.Y));
            for (var step = 1; step <= 4; step++)
            {
                var distance = from + (to - from) * step / 4;
                kanban.Pinch.Move(1, new Point(middle.X - distance, middle.Y));
                kanban.Pinch.Move(2, new Point(middle.X + distance, middle.Y));
            }
            kanban.Pinch.Lift(1);
            kanban.Pinch.Lift(2);
        }
    }
}
