using Flowery.Controls;
using Flowery.Enums;
using Flowery.WinUI.Kanban.Controls;

namespace Flowery.WinUI.Gallery.Examples
{
    public sealed partial class FlowKanbanExample : ScrollableExamplePage
    {
        private static readonly object ResourceInitLock = new();
        private static bool _kanbanResourcesLoaded;
        private const string KanbanDictionaryUri = "ms-appx:///Flowery.WinUI.Kanban/Themes/Generic.xaml";
        private readonly FlowKanbanAssigneeAdapter _assigneeAdapter;
        private FlowKanbanManager? _kanbanManager;

        public FlowKanbanExample()
        {
            InitializeComponent();
            _assigneeAdapter = new FlowKanbanAssigneeAdapter(CreateAssignees());
            InitializeKanbanDemo();
            Unloaded += OnFlowKanbanUnloaded;
        }

        protected override ScrollViewer? ScrollViewer => MainScrollViewer;

        private void OnFlowKanbanUnloaded(object sender, RoutedEventArgs e)
        {
            _kanbanManager?.Shutdown();
        }

        private void InitializeKanbanDemo()
        {
            if (DemoKanban == null)
                return;

            EnsureKanbanResourcesLoaded();
            DemoKanban.AssigneeAdapter = _assigneeAdapter;
            DemoKanban.AssigneeAdapterFailed += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Kanban assignee {args.Operation} failed: {args.Exception.Message}");
            };

            DemoKanban.EditCardCommand = new RelayCommand<FlowTask>(async task =>
            {
                if (task == null || XamlRoot == null)
                    return;

                var assignees = await GetAssigneeOptionsAsync(_assigneeAdapter);
                await FlowTaskEditorDialog.ShowAsyncWithAssignees(
                    task,
                    XamlRoot,
                    DemoKanban.Board.Tags,
                    assignees,
                    DemoKanban.AutoExpandCardDetails);
            });

            _kanbanManager = new FlowKanbanManager(DemoKanban, autoAttach: false);
            _kanbanManager.PersistenceFailed += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Kanban persistence {args.Operation} failed: {args.Exception.Message}");
            };

            var loaded = _kanbanManager.Initialize();
            if (!loaded)
            {
                DemoKanban.Board = CreateSampleKanbanBoard();
            }
        }

        // The demo supplies its assignees directly. A real host app loads them from its own user store.
        private static IReadOnlyList<FlowKanbanAssignee> CreateAssignees()
        {
            string[] names = ["Sam", "Dario", "Max", "Demis", "Adam", "Lucy", "Anita", "Sue", "Eric", "Forrest"];
            return names.Select((name, index) =>
            {
                string[] roles = index switch
                {
                    0 => ["Administrator"],
                    1 => ["Product owner"],
                    _ => ["Contributor"]
                };
                return new FlowKanbanAssignee(
                    $"demo-{name.ToLowerInvariant()}",
                    name,
                    roles: roles);
            }).ToArray();
        }

        private static void EnsureKanbanResourcesLoaded()
        {
            if (_kanbanResourcesLoaded)
            {
                return;
            }

            lock (ResourceInitLock)
            {
                if (_kanbanResourcesLoaded)
                {
                    return;
                }

                var appResources = Application.Current?.Resources;
                if (appResources == null)
                {
                    return;
                }

                if (ContainsDictionarySource(appResources, KanbanDictionaryUri))
                {
                    _kanbanResourcesLoaded = true;
                    return;
                }

                try
                {
                    appResources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri(KanbanDictionaryUri)
                    });
                    _kanbanResourcesLoaded = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"FlowKanbanExample Kanban resources init failed: {ex.GetType().Name} - {ex.Message}");
                }
            }
        }

        private static bool ContainsDictionarySource(ResourceDictionary dictionary, string source)
        {
            if (dictionary.Source is Uri uri && string.Equals(uri.ToString(), source, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var merged in dictionary.MergedDictionaries)
            {
                if (merged != null && ContainsDictionarySource(merged, source))
                {
                    return true;
                }
            }

            return false;
        }

        private static FlowKanbanData CreateSampleKanbanBoard()
        {
            return new FlowKanbanData
            {
                Columns =
                [
                    new FlowKanbanColumnData
                    {
                        Title = "Todo",
                        Tasks =
                        [
                            new FlowTask
                            {
                                Title = "Make content sections more interactive",
                                Description = "Add extra visual elements to remove big walls of text\n\nhigh priority",
                                Palette = DaisyColor.Primary,
                                Subtasks =
                                {
                                    new FlowSubtask { Title = "Add cards explaining Kanban concept", IsCompleted = true },
                                    new FlowSubtask { Title = "Add visual roadmap" },
                                    new FlowSubtask { Title = "Add images" }
                                }
                            },
                            new FlowTask
                            {
                                Title = "Some other todo",
                                Description = "Additional task details here",
                                Palette = DaisyColor.Default
                            },
                            new FlowTask
                            {
                                Title = "Another todo",
                                Description = "Important pending item",
                                Palette = DaisyColor.Secondary
                            },
                            new FlowTask
                            {
                                Title = "One more",
                                Description = "Final todo item",
                                Palette = DaisyColor.Default
                            }
                        ]
                    },
                    new FlowKanbanColumnData
                    {
                        Title = "In Progress",
                        Tasks =
                        [
                            new FlowTask
                            {
                                Title = "Make content sections more interactive",
                                Description = "Subtask: 1/3 complete",
                                Palette = DaisyColor.Error
                            }
                        ]
                    },
                    new FlowKanbanColumnData
                    {
                        Title = "Done",
                        Tasks =
                        [
                            new FlowTask
                            {
                                Title = "Buy more coffee",
                                Description = "Completed: 1/3",
                                Palette = DaisyColor.Default,
                                Subtasks =
                                [
                                    new FlowSubtask { Title = "Order beans online", IsCompleted = true },
                                    new FlowSubtask { Title = "Pick up from store", IsCompleted = false },
                                    new FlowSubtask { Title = "Try new roast", IsCompleted = false }
                                ]
                            },
                            new FlowTask
                            {
                                Title = "Published repo",
                                Description = "Successfully published",
                                Palette = DaisyColor.Success
                            }
                        ]
                    }
                ]
            };
        }

        private static async Task<IReadOnlyList<FlowTaskAssigneeOption>> GetAssigneeOptionsAsync(
            IFlowKanbanAssigneeAdapter adapter)
        {
            var assignees = await adapter.GetAssigneesAsync();
            return assignees
                .Select(assignee => new FlowTaskAssigneeOption(assignee.Id, assignee.DisplayName))
                .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
