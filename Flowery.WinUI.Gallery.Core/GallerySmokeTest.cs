using System.Diagnostics;
using System.Threading.Tasks;

namespace Flowery.WinUI.Gallery
{
    /// <summary>
    /// The Gallery's smoke run (--smoke-test &lt;report.xml&gt;): opens every category, scrolls through it and reports per
    /// category an exception, a page that does not load, or a localized text ({Binding [Key]}) that stays empty. A
    /// NativeAOT build reports missing marshalling and binding metadata only at run time, so this run is how such a build
    /// is checked. WinUI raises DebugSettings.BindingFailed only with a debugger attached, so other failed bindings go
    /// unseen.
    /// </summary>
    public sealed class GallerySmokeTest
    {
        private const int MaxScrollSteps = 60;
        private const int MaxListedProblems = 20;
        // The first page builds the shell as well, so it may take longer than a category.
        private static readonly TimeSpan FirstPageTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(100);
        private readonly MainView _view;
        private readonly SmokeReport _report;
        private readonly List<string> _errors = [];

        private GallerySmokeTest(MainView view, SmokeReport report)
        {
            _view = view;
            _report = report;
        }

        public static async Task<SmokeReport> RunAsync(Window window, string reportPath)
        {
            var view = (window.Content as Panel)?.Children.OfType<MainView>().FirstOrDefault()
                ?? throw new InvalidOperationException("The Gallery window shows no MainView.");
            var report = new SmokeReport(reportPath);
            await new GallerySmokeTest(view, report).RunAsync();
            report.Save();
            return report;
        }

        private async Task RunAsync()
        {
            await WaitUntilLoadedAsync(_view, "the Gallery");
            await WaitUntilAsync(_view, static view => view.ActiveCategoryContent is not null, FirstPageTimeout,
                "The Gallery showed no first page");

            var application = Application.Current;
            application.UnhandledException += OnUnhandledException;
            _view.CategoryFailed += OnCategoryFailed;
            try
            {
                foreach (var category in _view.CategoryNames.ToArray())
                    await CheckCategoryAsync(category);
            }
            finally
            {
                _view.CategoryFailed -= OnCategoryFailed;
                application.UnhandledException -= OnUnhandledException;
            }
        }

        private async Task CheckCategoryAsync(string category)
        {
            _errors.Clear();
            List<string> emptyTexts = [];
            try
            {
                _view.ShowCategory(category);
                var content = _view.ActiveCategoryContent ?? throw new InvalidOperationException("The category shows no content.");
                await WaitUntilLoadedAsync(content, category);
                await SettleAsync();
                await ScrollThroughAsync(content);
                CollectEmptyTexts(content, emptyTexts);
            }
            catch (Exception exception)
            {
                _errors.Add(exception.ToString());
            }

            var problems = _errors.Distinct().Take(MaxListedProblems).ToList();
            if (emptyTexts.Count > 0)
            {
                problems.Add($"{emptyTexts.Count} localized texts are empty: " + string.Join("; ", emptyTexts.Take(MaxListedProblems))
                    + (emptyTexts.Count > MaxListedProblems ? "; ..." : string.Empty));
            }
            _report.Add("Category." + category, problems.Count == 0,
                problems.Count == 0 ? "Opened and scrolled through; every localized text is set." : string.Join(Environment.NewLine, problems));
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            args.Handled = true;
            _errors.Add("Unhandled: " + args.Exception);
        }

        private void OnCategoryFailed(string category, Exception exception) =>
            _errors.Add($"Creating {category} failed: {exception}");

        private static Task WaitUntilLoadedAsync(FrameworkElement element, string description) =>
            WaitUntilAsync(element, static loading => loading.IsLoaded, LoadTimeout, $"{description} did not load");

        private static async Task WaitUntilAsync<T>(T state, Func<T, bool> condition, TimeSpan timeout, string failure)
        {
            var watch = Stopwatch.StartNew();
            while (!condition(state))
            {
                if (watch.Elapsed > timeout) throw new TimeoutException($"{failure} within {timeout.TotalSeconds:0} seconds.");
                await Task.Delay(PollInterval);
            }
        }

        // Two rendered frames and a short pause let layout, deferred loading and bindings complete.
        private static async Task SettleAsync()
        {
            await NextFrameAsync();
            await NextFrameAsync();
            await Task.Delay(SettleDelay);
        }

        private static Task NextFrameAsync() => new RenderedFrame().Task;

        private sealed class RenderedFrame
        {
            private readonly TaskCompletionSource _frame = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public RenderedFrame() => CompositionTarget.Rendering += OnRendering;

            public Task Task => _frame.Task;

            private void OnRendering(object? sender, object args)
            {
                CompositionTarget.Rendering -= OnRendering;
                _frame.TrySetResult();
            }
        }

        // Scrolling realizes the sections further down, which load their content on demand.
        private static async Task ScrollThroughAsync(FrameworkElement content)
        {
            if (FindFirst<ScrollViewer>(content) is not { } viewer)
                return;

            for (var step = 0; step < MaxScrollSteps && viewer.VerticalOffset < viewer.ScrollableHeight - 1; step++)
            {
                viewer.ChangeView(null, viewer.VerticalOffset + Math.Max(1, viewer.ViewportHeight), null, true);
                await SettleAsync();
            }
            viewer.ChangeView(null, 0, null, true);
            await SettleAsync();
        }

        private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
        {
            Queue<DependencyObject> pending = new([root]);
            while (pending.Count > 0)
            {
                var element = pending.Dequeue();
                if (element is T match) return match;
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                    pending.Enqueue(VisualTreeHelper.GetChild(element, index));
            }
            return null;
        }

        // A localization indexer returns at least its key, so an empty {Binding [Key]} target means the binding found no
        // indexer, as in a NativeAOT build without the generated property provider.
        internal static void CollectEmptyTexts(DependencyObject root, List<string> emptyTexts)
        {
            Stack<DependencyObject> pending = new([root]);
            while (pending.Count > 0)
            {
                var element = pending.Pop();
                switch (element)
                {
                    case TextBlock text when string.IsNullOrEmpty(text.Text):
                        AddIfBound(text, TextBlock.TextProperty, nameof(TextBlock.Text), emptyTexts);
                        break;
                    case TextBox box when string.IsNullOrEmpty(box.PlaceholderText):
                        AddIfBound(box, TextBox.PlaceholderTextProperty, nameof(TextBox.PlaceholderText), emptyTexts);
                        break;
                    case ContentControl control when control.Content is null or "":
                        AddIfBound(control, ContentControl.ContentProperty, nameof(ContentControl.Content), emptyTexts);
                        break;
                }
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                    pending.Push(VisualTreeHelper.GetChild(element, index));
            }
        }

        private static void AddIfBound(FrameworkElement element, DependencyProperty property, string propertyName, List<string> emptyTexts)
        {
            var path = element.GetBindingExpression(property)?.ParentBinding.Path?.Path;
            if (path is null || !path.StartsWith('['))
                return;
            var name = string.IsNullOrEmpty(element.Name) ? element.GetType().Name : $"{element.GetType().Name} {element.Name}";
            emptyTexts.Add($"{name}.{propertyName} {{Binding {path}}}");
        }
    }
}
