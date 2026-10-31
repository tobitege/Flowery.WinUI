using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
            var column = RuntimeTestHelpers.Descendants<FlowKanbanColumn>(kanban).First();
            Assert.IsTrue(kanban.IsFreePlace(column));
            Assert.IsFalse(kanban.IsFreePlace(cards[0]));
            Assert.IsFalse(kanban.IsFreePlace(RuntimeTestHelpers.Descendants<TextBlock>(cards[0]).First()));
            Assert.IsFalse(kanban.IsFreePlace(RuntimeTestHelpers.Descendants<ButtonBase>(column).First()));
            Assert.IsFalse(kanban.IsFreePlace(null));
            // On touch a tap on a column header shows its buttons.
            Assert.IsFalse(kanban.IsFreePlace(RuntimeTestHelpers.Descendants<FrameworkElement>(column).First(static element => element.Name == "PART_ColumnHeaderGrid")));

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
        public async Task When_ACardIsTappedBeforeItsLoadedHandlerRan_ItJoinsTheSelection()
        {
            var selected = new FlowTask { Title = "Selected" };
            var kanban = await CreateBoardAsync(new[] { selected });
            Assert.IsTrue(CardOf(kanban, selected).HoldTask());
            await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
            var edits = new List<FlowTask>();

            // The layout realizes the new card, and WinUI raises its Loaded later. A touch can reach the card in between,
            // such as on a card that a long column realizes while it scrolls.
            var task = new FlowTask { Title = "Task" };
            kanban.Board.Columns[0].Tasks.Insert(0, task);
            kanban.UpdateLayout();
            var card = CardOf(kanban, task);
            Assert.IsNull(card.LoadedBoard, "the card already ran its Loaded handler");
            card.EditCommand = new EditCounter(edits);

            Assert.IsTrue(card.TapTask());

            Assert.IsTrue(task.IsSelected);
            Assert.IsTrue(selected.IsSelected);
            Assert.IsEmpty(edits);
        }

        private static async Task<FlowKanban> CreateBoardAsync(FlowTask[] tasks)
        {
            var column = new FlowKanbanColumnData { Title = "Todo" };
            foreach (var task in tasks)
                column.Tasks.Add(task);
            // A fixed size keeps the board inside the runner window.
            var kanban = new FlowKanban
            {
                Board = new FlowKanbanData { Columns = { column } }, Width = 1000, Height = 600,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
            };
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            // With no saved boards, OnKanbanLoaded switches to the Home view, which hides the columns.
            kanban.CurrentView = FlowKanbanView.Board;
            // The columns realize their cards over several layout passes.
            bool Realized() => tasks.All(task => RuntimeTestHelpers.Descendants<FlowTaskCard>(kanban).Any(card => ReferenceEquals(card.Task, task)));
            for (var pass = 0; pass < 50 && !Realized(); pass++)
                await RuntimeTestHelpers.CompleteLayoutAsync(kanban);
            Assert.IsTrue(Realized(), "realized cards");
            // WinUI can raise a card's Loaded after the card is in the tree. A tap looks the board up itself, but the other
            // work of the Loaded handler has not run yet, so a test taps a card only once it found its board there.
            await RuntimeTestHelpers.WaitUntilAsync(
                () => tasks.All(task => ReferenceEquals(CardOf(kanban, task).LoadedBoard, kanban)), "the cards know their board");
            return kanban;
        }

        private static FlowTaskCard CardOf(FlowKanban kanban, FlowTask task) =>
            RuntimeTestHelpers.Descendants<FlowTaskCard>(kanban).Single(card => ReferenceEquals(card.Task, task));

        private sealed class EditCounter(List<FlowTask> edits) : ICommand
        {
            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object? parameter) => parameter is FlowTask;

            public void Execute(object? parameter) => edits.Add((FlowTask)parameter!);
        }
    }
}
