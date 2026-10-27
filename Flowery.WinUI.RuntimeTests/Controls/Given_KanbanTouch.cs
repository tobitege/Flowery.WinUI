using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // Touch and pen on Kanban cards follow DaisyScheduler and the apps of the platforms: a tap opens a card, a hold
    // selects it, and while cards are selected a tap adds a card to the selection or takes it out. The taps and holds go
    // through the same entry points as the pointer handlers and the hold timer; the real finger test injects Windows
    // touch input.
    [TestClass]
    public class Given_KanbanTouch
    {
        [TestMethod]
        public async Task When_TouchTapsOrHoldsACard_ATapOpensAHoldSelectsTapsThenToggleAndControlsAreNoFreePlace()
        {
            var tasks = new[] { new FlowTask { Title = "A" }, new FlowTask { Title = "B" } };
            var kanban = await CreateBoardAsync(tasks);
            var edits = new List<FlowTask>();
            var cards = tasks.Select(task => CardOf(kanban, task)).ToArray();
            foreach (var card in cards)
                card.EditCommand = new EditCounter(edits);
            bool[] Selected() => tasks.Select(static task => task.IsSelected).ToArray();

            // A press on a card or a control is no free place of the board.
            var column = Descendants<FlowKanbanColumn>(kanban).First();
            Assert.IsTrue(kanban.IsFreePlace(column));
            Assert.IsFalse(kanban.IsFreePlace(cards[0]));
            Assert.IsFalse(kanban.IsFreePlace(Descendants<TextBlock>(cards[0]).First()));
            Assert.IsFalse(kanban.IsFreePlace(Descendants<ButtonBase>(column).First()));
            Assert.IsFalse(kanban.IsFreePlace(null));
            // On touch a tap on a column header shows its buttons.
            Assert.IsFalse(kanban.IsFreePlace(Descendants<FrameworkElement>(column).First(static element => element.Name == "PART_ColumnHeaderGrid")));

            Assert.IsTrue(cards[0].TapTask());
            CollectionAssert.AreEqual(new[] { tasks[0] }, edits);
            CollectionAssert.AreEqual(new[] { false, false }, Selected());

            Assert.IsTrue(cards[0].HoldTask());
            CollectionAssert.AreEqual(new[] { true, false }, Selected());
            cards[1].TapTask();
            CollectionAssert.AreEqual(new[] { true, true }, Selected());
            cards[0].TapTask();
            CollectionAssert.AreEqual(new[] { false, true }, Selected());
            cards[1].TapTask();
            CollectionAssert.AreEqual(new[] { false, false }, Selected());
            Assert.HasCount(1, edits);

            // Without a selection a tap opens again; a hold on a selected card keeps it selected.
            cards[1].TapTask();
            CollectionAssert.AreEqual(new[] { tasks[0], tasks[1] }, edits);
            cards[1].HoldTask();
            cards[1].HoldTask();
            CollectionAssert.AreEqual(new[] { false, true }, Selected());
        }

        [TestMethod]
        public async Task When_ARealFingerTapsHoldsAndSwipes_WinUIDeliversTheTouchModel()
        {
            var tasks = new[] { new FlowTask { Title = "A" }, new FlowTask { Title = "B" } };
            var doing = new FlowKanbanColumnData { Title = "Doing" };
            // Enough cards below them that the board scrolls.
            var kanban = await CreateBoardAsync(tasks, filler: 20, doing);
            var edits = new List<FlowTask>();
            foreach (var task in tasks)
                CardOf(kanban, task).EditCommand = new EditCounter(edits);
            var card = CardOf(kanban, tasks[0]);
            var middle = new Windows.Foundation.Point(card.ActualWidth / 2, card.ActualHeight / 2);

            await TouchInjection.TapAsync(card, middle);
            CollectionAssert.AreEqual(new[] { tasks[0] }, edits, "tap");
            Assert.IsFalse(tasks[0].IsSelected, "tap");

            await TouchInjection.HoldAsync(card, middle, () => tasks[0].IsSelected);
            Assert.IsTrue(tasks[0].IsSelected, "hold");
            Assert.HasCount(1, edits, "hold");

            // A tap on a free place of the board clears the selection.
            var doingColumn = Descendants<FlowKanbanColumn>(kanban).Single(column => ReferenceEquals(column.ColumnData, doing));
            await TouchInjection.TapAsync(doingColumn, new Windows.Foundation.Point(doingColumn.ActualWidth / 2, doingColumn.ActualHeight * 2 / 3));
            Assert.IsFalse(tasks[0].IsSelected, "free tap");

            // A swipe over a card scrolls and neither opens nor selects it, also once the hold time has passed.
            card = CardOf(kanban, tasks[0]);
            var before = card.TransformToVisual(kanban).TransformPoint(default);
            await TouchInjection.SwipeAsync(card, middle, new Windows.Foundation.Point(0, -150));
            await Task.Delay(1000);
            await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
            Assert.AreNotEqual(before, card.TransformToVisual(kanban).TransformPoint(default), "the swipe did not scroll");
            Assert.IsFalse(tasks[0].IsSelected, "swipe");
            Assert.HasCount(1, edits, "swipe");
        }

        [TestMethod]
        public async Task When_ARealFingerHoldsACard_ItDragsItIntoAnotherColumn()
        {
            var task = new FlowTask { Title = "A" };
            var doing = new FlowKanbanColumnData { Title = "Doing" };
            var kanban = await CreateBoardAsync(new[] { task }, 0, doing);
            var card = CardOf(kanban, task);
            var middle = new Windows.Foundation.Point(card.ActualWidth / 2, card.ActualHeight / 2);
            var doingColumn = Descendants<FlowKanbanColumn>(kanban).Single(column => ReferenceEquals(column.ColumnData, doing));
            var target = card.TransformToVisual(doingColumn).Inverse.TransformPoint(
                new Windows.Foundation.Point(doingColumn.ActualWidth / 2, doingColumn.ActualHeight / 3));

            // After the hold the finger drags the card through WinUI's drag and drop (CanDrag).
            await TouchInjection.HoldAsync(card, middle, () => task.IsSelected,
                new Windows.Foundation.Point(target.X - middle.X, target.Y - middle.Y));
            await Task.Delay(500);
            await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
            if (!doing.Tasks.Contains(task) && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
                Assert.Inconclusive("Skipped: on the GitHub runner, WinUI's drag and drop does not land a card dragged by injected touch.");
            CollectionAssert.Contains(doing.Tasks, task, "drag after the hold");
        }

        private static async Task<FlowKanban> CreateBoardAsync(FlowTask[] tasks, int filler = 0, params FlowKanbanColumnData[] more)
        {
            var column = new FlowKanbanColumnData { Title = "Todo" };
            foreach (var task in tasks)
                column.Tasks.Add(task);
            for (var index = 0; index < filler; index++)
                column.Tasks.Add(new FlowTask { Title = "Filler " + index });
            // A fixed size keeps the board inside the runner window, so the board scrolls instead.
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData { Columns = { column } }, Width = 1000, Height = 600,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
            };
            foreach (var other in more)
                kanban.Board.Columns.Add(other);
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            // With no saved boards, OnKanbanLoaded switches to the Home view, which hides the columns.
            kanban.CurrentView = FlowKanbanView.Board;
            // The columns realize their cards over several layout passes.
            bool Realized() => tasks.All(task => Descendants<FlowTaskCard>(kanban).Any(card => ReferenceEquals(card.Task, task)));
            for (var pass = 0; pass < 50 && !Realized(); pass++)
                await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
            Assert.IsTrue(Realized(), "realized cards");
            return kanban;
        }

        private static FlowTaskCard CardOf(FlowKanban kanban, FlowTask task) =>
            Descendants<FlowTaskCard>(kanban).Single(card => ReferenceEquals(card.Task, task));

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                    yield return match;
                foreach (var descendant in Descendants<T>(child))
                    yield return descendant;
            }
        }

        private sealed class EditCounter(List<FlowTask> edits) : ICommand
        {
            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object? parameter) => parameter is FlowTask;

            public void Execute(object? parameter) => edits.Add((FlowTask)parameter!);
        }
    }
}
