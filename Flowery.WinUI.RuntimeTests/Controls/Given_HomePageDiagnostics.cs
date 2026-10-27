using System.Threading.Tasks;
using Flowery.WinUI.Gallery.Examples;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_HomePageDiagnostics
    {
        [TestMethod]
        public async Task When_DiagnosticsChange_VisibilityFollowsTheText()
        {
            var page = new HomePage();
            var diagnostics = (TextBox)page.FindName("DiagnosticsTextBox");
            Assert.AreEqual(string.Empty, diagnostics.Text);
            Assert.AreEqual(Visibility.Collapsed, diagnostics.Visibility);

            try
            {
                RuntimeTestHelpers.AttachToHost(page);
                await RuntimeTestHelpers.EnsureLoadedAsync(page);
                Assert.AreEqual(Visibility.Collapsed, diagnostics.Visibility,
                    "Loading the page must not reveal an empty diagnostics box.");

                page.AppendDiagnostics(string.Empty);
                Assert.AreEqual(string.Empty, diagnostics.Text);
                Assert.AreEqual(Visibility.Collapsed, diagnostics.Visibility);

                page.AppendDiagnostics("First message");
                Assert.AreEqual("First message", diagnostics.Text);
                Assert.AreEqual(Visibility.Visible, diagnostics.Visibility);

                page.AppendDiagnostics("Second message");
                Assert.AreEqual("First message\n" + new string('#', 50) +
                    "\nSecond message", diagnostics.Text.ReplaceLineEndings("\n"));
                Assert.AreEqual(Visibility.Visible, diagnostics.Visibility);

                page.SetDiagnosticsText("Restored diagnostics");
                Assert.AreEqual("Restored diagnostics", diagnostics.Text);
                Assert.AreEqual(Visibility.Visible, diagnostics.Visibility);

                page.SetDiagnosticsText("Replacement");
                Assert.AreEqual("Replacement", diagnostics.Text);
                Assert.AreEqual(Visibility.Visible, diagnostics.Visibility);

                page.SetDiagnosticsText(null);
                Assert.AreEqual(string.Empty, diagnostics.Text);
                Assert.AreEqual(Visibility.Collapsed, diagnostics.Visibility);

                page.AppendDiagnostics("After reset");
                Assert.AreEqual("After reset", diagnostics.Text,
                    "Appending after a reset must not retain a separator or an earlier message.");
                Assert.AreEqual(Visibility.Visible, diagnostics.Visibility);

                page.SetDiagnosticsText(string.Empty);
                Assert.AreEqual(string.Empty, diagnostics.Text);
                Assert.AreEqual(Visibility.Collapsed, diagnostics.Visibility);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }
    }
}
