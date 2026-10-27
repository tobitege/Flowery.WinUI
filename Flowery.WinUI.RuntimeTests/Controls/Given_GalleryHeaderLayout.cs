using System;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.WinUI.Gallery;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_GalleryHeaderLayout
{
    [TestMethod]
    public async Task When_GalleryWidthChanges_HeaderFitsAndCollapsedStateSurvives()
    {
        var previousIntensity = DaisyBaseContentControl.GlobalNeumorphicIntensity;
        var view = new MainView { Width = 900, Height = 480 };
        try
        {
            await Load(view);
            var logo = Find<Image>(view, "HeaderLogo");
            var title = Find<TextBlock>(view, "HeaderTitle");
            var license = Find<TextBlock>(view, "MitLicenseText");
            var credits = Find<Grid>(view, "CreditsGrid");
            var header = Find<Border>(view, "HeaderBorder");
            var categoryTitle = Find<TextBlock>(view, "CategoryTitle");
            categoryTitle.FontSize = 30;

            Assert.AreEqual(48d, logo.ActualWidth);
            Assert.AreEqual(0, Grid.GetRow(license));
            foreach (var width in new[] { 320d, 480d, 519d })
            {
                view.Width = width;
                await Arrange(view);

                Assert.AreEqual(36d, logo.ActualWidth);
                Assert.AreEqual(22d, title.FontSize);
                Assert.AreEqual(1, Grid.GetRow(license));
                Assert.AreEqual(30d, categoryTitle.FontSize, "Header changes must preserve the category's selected size.");
                var licenseBounds = license.TransformToVisual(credits).TransformBounds(new Rect(0, 0, license.ActualWidth, license.ActualHeight));
                Assert.IsTrue(licenseBounds.Y >= credits.RowDefinitions[0].ActualHeight - 0.5, "The license must have its own row.");
                AssertFits(license, credits);
                AssertFits(title, header);
                AssertFits(logo, header);
            }

            foreach (var width in new[] { 520d, 900d })
            {
                view.Width = width;
                await Arrange(view);

                Assert.AreEqual(48d, logo.ActualWidth);
                Assert.AreEqual(28d, title.FontSize);
                Assert.AreEqual(0, Grid.GetRow(license));
                Assert.AreEqual(2, Grid.GetColumn(license));
                Assert.AreEqual(1, Grid.GetColumnSpan(license));
                AssertFits(license, credits);
                AssertFits(title, header);
            }

            // A collapsed header stays collapsed while narrowing and restores the title size of the current width.
            var subtitle = Find<Grid>(view, "SubtitleRow");
            var collapse = typeof(MainView).GetMethod("SetHeaderCollapsed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            collapse.Invoke(view, new object[] { true });
            view.Width = 480;
            await Arrange(view);

            Assert.AreEqual(Visibility.Collapsed, subtitle.Visibility);
            Assert.AreEqual(16d, title.FontSize);

            collapse.Invoke(view, new object[] { false });
            await Arrange(view);
            Assert.AreEqual(Visibility.Visible, subtitle.Visibility);
            Assert.AreEqual(22d, title.FontSize);

            view.Width = 900;
            await Arrange(view);
            Assert.AreEqual(28d, title.FontSize);
        }
        finally
        {
            await Unload(view);
            DaisyBaseContentControl.GlobalNeumorphicIntensity = previousIntensity;
        }
    }

    private static T Find<T>(MainView view, string name) where T : FrameworkElement
    {
        var element = view.FindName(name) as T;
        Assert.IsNotNull(element, name);
        return element;
    }

    private static void AssertFits(FrameworkElement element, FrameworkElement parent)
    {
        var bounds = element.TransformToVisual(parent).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        Assert.IsTrue(bounds.X >= -0.5 && bounds.Right <= parent.ActualWidth + 0.5, $"{element.Name} must fit horizontally.");
        Assert.IsTrue(bounds.Y >= -0.5 && bounds.Bottom <= parent.ActualHeight + 0.5, $"{element.Name} must fit vertically.");
    }

    private static async Task Load(FrameworkElement element)
    {
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element).WaitAsync(TimeSpan.FromSeconds(5));
        await Arrange(element);
    }

    private static async Task Arrange(FrameworkElement element)
    {
        var ready = new TaskCompletionSource();
        // Header changes run in Loaded/SizeChanged; UpdateLayout below settles the measured bounds.
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => ready.SetResult()));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        element.UpdateLayout();
    }

    private static async Task Unload(FrameworkElement element)
    {
        if (!element.IsLoaded) return;
        var unloaded = new TaskCompletionSource();
        void OnUnloaded(object sender, RoutedEventArgs args) => unloaded.TrySetResult();
        element.Unloaded += OnUnloaded;
        try
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(element);
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { element.Unloaded -= OnUnloaded; }
    }
}
