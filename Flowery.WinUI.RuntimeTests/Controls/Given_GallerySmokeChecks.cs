using System.Threading.Tasks;
using Flowery.WinUI.Gallery;
using Flowery.WinUI.Gallery.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // The Gallery smoke run sees a localized text ({Binding [Key]}) that stays empty because its binding found no
    // indexer, as in a NativeAOT build without the binding metadata, and no other empty text.
    [TestClass]
    public class Given_GallerySmokeChecks
    {
        [TestMethod]
        public async Task When_ALocalizedBindingFindsNoIndexer_TheSmokeRunReportsItsEmptyText()
        {
            const string Key = "[Gallery_Tour_WelcomeTitle]";
            var localized = Bound(new TextBlock { Name = "Localized" }, TextBlock.TextProperty, Key, GalleryLocalization.Instance);
            var noIndexer = Bound(new TextBlock { Name = "NoIndexer" }, TextBlock.TextProperty, Key, new object());
            var noProperty = Bound(new TextBlock { Name = "NoProperty" }, TextBlock.TextProperty, "NoSuchProperty", new object());
            var placeholder = Bound(new TextBox { Name = "Placeholder" }, TextBox.PlaceholderTextProperty, Key, new object());
            var content = Bound(new ContentControl { Name = "Content" }, ContentControl.ContentProperty, Key, new object());
            var page = new StackPanel { Children = { localized, noIndexer, noProperty, placeholder, content } };
            RuntimeTestHelpers.AttachToHost(page);
            try
            {
                await RuntimeTestHelpers.CompleteLayoutAsync(page);
                List<string> emptyTexts = [];
                GallerySmokeTest.CollectEmptyTexts(page, emptyTexts);
                Assert.IsFalse(string.IsNullOrEmpty(localized.Text), "the Gallery localization gives no text");
                CollectionAssert.AreEquivalent(new[]
                {
                    $"TextBlock NoIndexer.Text {{Binding {Key}}}",
                    $"TextBox Placeholder.PlaceholderText {{Binding {Key}}}",
                    $"ContentControl Content.Content {{Binding {Key}}}"
                }, emptyTexts);
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(page);
            }
        }

        private static T Bound<T>(T element, DependencyProperty property, string path, object source) where T : FrameworkElement
        {
            element.SetBinding(property, new Binding { Path = new PropertyPath(path), Source = source });
            return element;
        }
    }
}
