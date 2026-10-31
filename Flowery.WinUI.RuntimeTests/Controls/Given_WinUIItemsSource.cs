using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // DaisySelect and DaisyBreadcrumbBar hand a plain list of the app's own items to their WinUI list as a List<object>,
    // whose marshalling code a NativeAOT app always has; the list of the item type itself has none there and fails.
    // The NativeAOT Gallery smoke run checks the marshalling; this test checks what reaches WinUI.
    [TestClass]
    public class Given_WinUIItemsSource
    {
        private sealed record Item(string Label);

        private sealed partial class InnerSelect : DaisySelect
        {
            public ListView? Inner => ListView;
        }

        [TestMethod]
        public async Task When_AControlGetsAPlainList_WinUIGetsItsItemsAsAListOfObjects()
        {
            Item[] items = [new("a"), new("b")];
            var select = new InnerSelect { ItemsSource = items };
            var bar = new DaisyBreadcrumbBar { ItemsSource = items };
            var page = new StackPanel { Children = { select, bar } };
            RuntimeTestHelpers.AttachToHost(page);
            try
            {
                await RuntimeTestHelpers.CompleteLayoutAsync(page);
                AssertSameItems(items, select.Inner?.ItemsSource, "the select");
                AssertSameItems(items, RuntimeTestHelpers.Descendants<BreadcrumbBar>(bar).Single().ItemsSource, "the breadcrumb bar");
                Assert.AreSame(items, select.ItemsSource, "the select keeps the list it was given");

                select.SelectedIndex = 1;
                Assert.AreSame(items[1], select.SelectedItem, "the select picks the item of the given list");

                Item[] next = [new("c")];
                select.ItemsSource = next;
                AssertSameItems(next, select.Inner?.ItemsSource, "the select after a new list");
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(page);
            }
        }

        [TestMethod]
        public async Task When_TheListReportsItsChanges_WinUIGetsTheSameCollection()
        {
            var items = new ObservableCollection<Item> { new("a") };
            var select = new InnerSelect { ItemsSource = items };
            var bar = new DaisyBreadcrumbBar { ItemsSource = items };
            var page = new StackPanel { Children = { select, bar } };
            RuntimeTestHelpers.AttachToHost(page);
            try
            {
                await RuntimeTestHelpers.CompleteLayoutAsync(page);
                Assert.AreSame(items, select.Inner?.ItemsSource, "the select hands on its observable collection");
                Assert.AreSame(items, RuntimeTestHelpers.Descendants<BreadcrumbBar>(bar).Single().ItemsSource,
                    "the breadcrumb bar hands on its observable collection");

                select.ItemsSource = null;
                Assert.AreSame(select.Items, select.Inner?.ItemsSource, "without a list the select shows its own items");
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(page);
            }
        }

        private static void AssertSameItems(IReadOnlyList<Item> expected, object? actual, string owner)
        {
            Assert.IsInstanceOfType<List<object>>(actual, $"{owner} hands WinUI no List<object>");
            var list = (List<object>)actual;
            Assert.HasCount(expected.Count, list, $"{owner} hands WinUI another number of items");
            for (var index = 0; index < expected.Count; index++)
                Assert.AreSame(expected[index], list[index], $"{owner} hands WinUI another item at {index}");
        }
    }
}
