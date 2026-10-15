using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Flowery.WinUI.Kanban.Controls.Users;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_KanbanDesignRegression
    {
        [TestMethod]
        public void When_Default_Boards_Are_Constructed_Their_State_Is_Independent()
        {
            var first = new FlowKanban();
            var second = new FlowKanban();
            first.Board.Title = "First";
            first.Board.Columns.Add(new FlowKanbanColumnData { Title = "Only first" });
            Assert.AreNotSame(first.Board, second.Board);
            Assert.AreNotEqual(first.Board.Title, second.Board.Title);
            Assert.AreEqual(0, second.Board.Columns.Count);
        }

        [TestMethod]
        public void When_Filter_Criteria_Are_Replaced_Visible_Controls_Match_Without_Mutating_Criteria()
        {
            var board = new FlowKanban { FilterPriorityLow = true };
            var date = new DateTime(2026, 10, 15);
            var criteria = new FlowKanbanFilterCriteria
            {
                Priorities = [FlowTaskPriority.Urgent],
                ShowOnlyBlocked = true,
                ShowOnlyOverdue = true,
                PlannedEndRange = new FlowKanbanDateRange { From = date, To = date.AddDays(2) }
            };
            board.FilterCriteria = criteria;
            Assert.IsFalse(board.FilterPriorityLow);
            Assert.IsTrue(board.FilterPriorityUrgent);
            Assert.IsTrue(board.FilterShowBlocked);
            Assert.IsTrue(board.FilterShowOverdue);
            Assert.AreEqual(date, board.FilterDateFrom?.DateTime);
            Assert.AreEqual(date.AddDays(2), board.FilterDateTo?.DateTime);
            CollectionAssert.AreEqual(new[] { FlowTaskPriority.Urgent }, criteria.Priorities);
            Assert.AreEqual(date.AddDays(2), criteria.PlannedEndRange.To);
        }

        [TestMethod]
        public void When_EndDate_Changes_Footer_Updates_And_Clears()
        {
            var task = new FlowTask { PlannedEndDate = new DateTime(2026, 10, 15) };
            var card = new FlowTaskCard { Task = task };
            var previous = card.EndDateText;
            task.PlannedEndDate = task.PlannedEndDate.Value.AddDays(2);
            Assert.IsTrue(card.HasEndDate);
            Assert.AreNotEqual(previous, card.EndDateText);
            task.PlannedEndDate = null;
            Assert.IsFalse(card.HasEndDate);
            Assert.AreEqual(string.Empty, card.EndDateText);
        }

        [TestMethod]
        public async Task When_Card_Is_Reloaded_Model_Updates_Are_Observed()
        {
            var task = new FlowTask();
            var card = new FlowTaskCard { Task = task };
            var panel = new Grid();
            panel.Children.Add(card);
            RuntimeTestHelpers.AttachToHost(panel);
            await RuntimeTestHelpers.EnsureLoadedAsync(card);
            var unloaded = new TaskCompletionSource();
            card.Unloaded += (_, _) => unloaded.TrySetResult();
            panel.Children.Remove(card);
            await unloaded.Task;
            panel.Children.Add(card);
            await RuntimeTestHelpers.EnsureLoadedAsync(card);

            task.PlannedEndDate = new DateTime(2026, 10, 15);
            Assert.IsTrue(card.HasEndDate);
        }

        [TestMethod]
        public async Task When_Provider_Fails_Error_Replaces_Empty_State_And_Retry_Clears_It()
        {
            var provider = new TestUserProvider();
            var view = new FlowKanbanUserManagement { UserProvider = provider };
            var refresh = typeof(FlowKanbanUserManagement).GetMethod("RefreshUsersAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await (Task)refresh.Invoke(view, new object[] { true, true })!;
            Assert.IsTrue(view.HasLoadError);
            Assert.AreEqual("Provider unavailable", view.LoadErrorMessage);
            Assert.IsFalse(view.IsLoading);
            Assert.IsFalse(view.IsEmptyStateVisible);

            provider.Fail = false;
            await (Task)refresh.Invoke(view, new object[] { true, true })!;
            Assert.IsFalse(view.HasLoadError);
            Assert.IsTrue(view.IsEmptyStateVisible);
        }

        [TestMethod]
        public void When_Card_Selection_Provider_Is_Used_Model_State_Follows()
        {
            var task = new FlowTask();
            var card = new FlowTaskCard { Task = task };
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(card);
            var provider = (ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem);
            Assert.IsNotNull(peer.GetPattern(PatternInterface.Invoke));
            provider.AddToSelection();
            Assert.IsTrue(task.IsSelected);
            Assert.IsTrue(provider.IsSelected);
            provider.RemoveFromSelection();
            Assert.IsFalse(task.IsSelected);
            provider.Select();
            Assert.IsTrue(task.IsSelected);
        }

        [TestMethod]
        public void When_Selected_Task_Is_Not_Realized_Selection_Provider_Can_Remove_It()
        {
            var task = new FlowTask { Title = "Offscreen", IsSelected = true };
            var board = new FlowKanban();
            board.Board.Columns.Add(new FlowKanbanColumnData { Tasks = { task } });
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(board);
            var selection = (ISelectionProvider)peer.GetPattern(PatternInterface.Selection);
            Assert.IsTrue(selection.CanSelectMultiple);
            var selected = selection.GetSelection();
            Assert.AreEqual(1, selected.Length);
            var itemPeer = new ProviderPeer(new Grid()).FromProvider(selected[0]);
            var item = (ISelectionItemProvider)itemPeer.GetPattern(PatternInterface.SelectionItem);
            Assert.IsTrue(item.IsSelected);
            item.RemoveFromSelection();
            Assert.IsFalse(task.IsSelected);
            Assert.AreEqual(0, selection.GetSelection().Length);
        }

        private sealed partial class ProviderPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
        {
            public AutomationPeer FromProvider(IRawElementProviderSimple provider) => PeerFromProvider(provider);
        }

        private sealed class TestUserProvider : IUserProvider
        {
            public bool Fail { get; set; } = true;
            public string ProviderKey => "test";
            public string DisplayName => "Test";
            public string ImplementationVersion => "1";
            public bool SupportsAvatars => false;
            public bool SupportsPresence => false;
            public bool SupportsRealtime => false;
            public event Action? UsersChanged { add { } remove { } }
            public Task<IEnumerable<IFlowUser>> GetAllUsersAsync(CancellationToken cancellation = default) =>
                Task.FromResult<IEnumerable<IFlowUser>>(Array.Empty<IFlowUser>());
            public Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default) => Task.FromResult<IFlowUser?>(null);
            public Task<IEnumerable<IFlowUser>> SearchUsersAsync(string query, int maxResults = 20, CancellationToken cancellation = default) => GetAllUsersAsync(cancellation);
            public Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default) => Task.FromResult<IFlowUser?>(null);
            public Task RefreshAsync(CancellationToken cancellation = default) => Fail
                ? Task.FromException(new InvalidOperationException("Provider unavailable")) : Task.CompletedTask;
        }
    }
}
