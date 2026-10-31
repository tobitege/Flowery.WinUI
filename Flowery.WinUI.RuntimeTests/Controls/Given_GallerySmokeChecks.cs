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
            var localized = Bound("Localized", "[Gallery_Tour_WelcomeTitle]", GalleryLocalization.Instance);
            var noIndexer = Bound("NoIndexer", "[Gallery_Tour_WelcomeTitle]", new object());
            var noProperty = Bound("NoProperty", "NoSuchProperty", new object());
            var page = new StackPanel { Children = { localized, noIndexer, noProperty } };
            RuntimeTestHelpers.AttachToHost(page);
            try
            {
                await RuntimeTestHelpers.CompleteLayoutAsync(page);
                List<string> emptyTexts = [];
                GallerySmokeTest.CollectEmptyTexts(page, emptyTexts);
                Assert.IsFalse(string.IsNullOrEmpty(localized.Text), "the Gallery localization gives no text");
                CollectionAssert.AreEqual(new[] { "TextBlock NoIndexer.Text {Binding [Gallery_Tour_WelcomeTitle]}" }, emptyTexts);
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(page);
            }
        }

        private static TextBlock Bound(string name, string path, object source)
        {
            var text = new TextBlock { Name = name };
            text.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(path), Source = source });
            return text;
        }
    }
}
