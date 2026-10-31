using System.Threading.Tasks;

namespace Flowery.WinUI.Gallery
{
    /// <summary>
    /// The Gallery's smoke run (--smoke-test &lt;report.xml&gt;): opens every category, scrolls through it and reports per
    /// category an exception, a page that does not load, a binding whose path does not resolve, or a localized text
    /// ({Binding [Key]}) that stays empty. A NativeAOT build reports missing marshalling and binding metadata only at
    /// run time, so this run is how such a build is checked.
    /// </summary>
    public sealed class GallerySmokeTest
    {
        private const int MaxScrollSteps = 60;
        private const int MaxListedTexts = 20;
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
            for (var attempt = 0; _view.ActiveCategoryContent is null; attempt++)
            {
                if (attempt > 600) throw new TimeoutException("The Gallery showed no first page within 30 seconds.");
                await Task.Delay(50);
            }

            var application = Application.Current;
            application.DebugSettings.IsBindingTracingEnabled = true;
            application.DebugSettings.BindingFailed += OnBindingFailed;
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
                application.DebugSettings.BindingFailed -= OnBindingFailed;
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

            var problems = _errors.Distinct().Take(MaxListedTexts).ToList();
            if (emptyTexts.Count > 0)
            {
                problems.Add($"{emptyTexts.Count} localized texts are empty: " + string.Join("; ", emptyTexts.Take(MaxListedTexts))
                    + (emptyTexts.Count > MaxListedTexts ? "; ..." : string.Empty));
            }
            _report.Add("Category." + category, problems.Count == 0,
                problems.Count == 0 ? "Opened and scrolled through; every binding resolved." : string.Join(Environment.NewLine, problems));
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            args.Handled = true;
            _errors.Add("Unhandled: " + args.Exception);
        }

        private void OnBindingFailed(object? sender, BindingFailedEventArgs args) =>
            _errors.Add("Binding failed: " + args.Message);

        private void OnCategoryFailed(string category, Exception exception) =>
            _errors.Add($"Creating {category} failed: {exception}");

        private static async Task WaitUntilLoadedAsync(FrameworkElement element, string description)
        {
            for (var attempt = 0; !element.IsLoaded; attempt++)
            {
                if (attempt > 400) throw new TimeoutException($"{description} did not load within 20 seconds.");
                await Task.Delay(50);
            }
        }

        // Two rendered frames and a short pause let layout, deferred loading and bindings complete.
        private static async Task SettleAsync()
        {
            await NextFrameAsync();
            await NextFrameAsync();
            await Task.Delay(100);
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
        private static void CollectEmptyTexts(DependencyObject root, List<string> emptyTexts)
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
