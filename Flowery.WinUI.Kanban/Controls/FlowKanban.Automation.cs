using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Flowery.WinUI.Kanban.Controls
{
    public partial class FlowKanban
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new BoardAutomationPeer(this);

        private sealed class BoardAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
        {
            private readonly FlowKanban _owner;
            public BoardAutomationPeer(FlowKanban owner) : base(owner) => _owner = owner;
            private readonly Dictionary<FlowTask, VirtualTaskSelectionPeer> _virtualItems = new();
            protected override string GetClassNameCore() => nameof(FlowKanban);
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
            protected override object GetPatternCore(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.Selection ? this : base.GetPatternCore(patternInterface);
            public bool CanSelectMultiple => true;
            public bool IsSelectionRequired => false;

            public IRawElementProviderSimple[] GetSelection()
            {
                var realized = new Dictionary<FlowTask, FlowTaskCard>();
                CollectCards(_owner, realized);
                var selected = new List<IRawElementProviderSimple>();
                var tasks = _owner.Board.Columns.SelectMany(column => column.Tasks).ToHashSet();
                foreach (var task in tasks.Where(task => task.IsSelected))
                {
                    if (realized.TryGetValue(task, out var card))
                    {
                        selected.Add(ProviderFromPeer(CreatePeerForElement(card)));
                    }
                    else
                    {
                        if (!_virtualItems.TryGetValue(task, out var peer))
                        {
                            peer = new VirtualTaskSelectionPeer(_owner, task, this);
                            _virtualItems.Add(task, peer);
                        }
                        selected.Add(ProviderFromPeer(peer));
                    }
                }
                foreach (var task in _virtualItems.Keys.Where(task => !task.IsSelected || !tasks.Contains(task)).ToArray())
                    _virtualItems.Remove(task);
                return selected.ToArray();
            }

            private static void CollectCards(DependencyObject element, Dictionary<FlowTask, FlowTaskCard> cards)
            {
                if (element is FlowTaskCard { Task: { } task } card)
                    cards[task] = card;
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                    CollectCards(VisualTreeHelper.GetChild(element, i), cards);
            }
        }

        private sealed class VirtualTaskSelectionPeer : FrameworkElementAutomationPeer, ISelectionItemProvider
        {
            private readonly FlowKanban _owner;
            private readonly FlowTask _task;
            private readonly AutomationPeer _container;
            public VirtualTaskSelectionPeer(FlowKanban owner, FlowTask task, AutomationPeer container) : base(owner)
            {
                _owner = owner;
                _task = task;
                _container = container;
            }
            protected override string GetClassNameCore() => nameof(FlowTaskCard);
            protected override string GetNameCore() => _task.Title;
            protected override string GetAutomationIdCore() => _task.Id;
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
            protected override bool IsOffscreenCore() => true;
            protected override bool IsKeyboardFocusableCore() => false;
            protected override Windows.Foundation.Rect GetBoundingRectangleCore() => new(0, 0, 0, 0);
            protected override object GetPatternCore(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.SelectionItem ? this : base.GetPatternCore(patternInterface);
            public bool IsSelected => _task.IsSelected;
            public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(_container);
            public void Select()
            {
                EnsureEnabled();
                _owner.DeselectAllTasks();
                _task.IsSelected = true;
            }
            public void AddToSelection() { EnsureEnabled(); _task.IsSelected = true; }
            public void RemoveFromSelection() { EnsureEnabled(); _task.IsSelected = false; }
            private void EnsureEnabled()
            {
                if (!_owner.IsEnabled) throw new ElementNotEnabledException();
            }
        }
    }
}
