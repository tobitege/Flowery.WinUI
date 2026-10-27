using System.Linq;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_TableCells
    {
        [TestMethod]
        public async Task When_CellsHaveStringOrElementContent_TextIsRendered()
        {
            var cells = new StackPanel { Orientation = Orientation.Horizontal };
            cells.Children.Add(new DaisyTableCell { Content = "String cell" });
            cells.Children.Add(new DaisyTableCell { Content = new TextBlock { Text = "Element cell" } });
            cells.Children.Add(new DaisyTableCell { Content = "Fixed cell", ColumnWidth = 120 });
            var row = new DaisyTableRow { Content = cells };
            var panel = new StackPanel { Width = 600 };
            panel.Children.Add(row);

            RuntimeTestHelpers.AttachToHost(panel);
            await RuntimeTestHelpers.EnsureLoadedAsync(row);
            await Task.Delay(200);
            panel.UpdateLayout();

            foreach (var text in new[] { "String cell", "Element cell", "Fixed cell" })
            {
                var block = Descendants<TextBlock>(row).FirstOrDefault(t => t.Text == text);
                Assert.IsNotNull(block, $"'{text}' is not in the visual tree.");
                Assert.IsTrue(block.ActualWidth > 0, $"'{text}' has no width.");
            }
        }

        [TestMethod]
        public async Task When_TableDefinesRowBorderBrush_RowsUseItAndRowLocalBrushWins()
        {
            const string key = "DaisyTableRowBorderBrush";
            var tableBrush = new SolidColorBrush(Colors.Red);
            var rowBrush = new SolidColorBrush(Colors.Blue);
            var inheritingRow = CreateRow("Inherits");
            var localRow = CreateRow("Local");
            localRow.Resources[key] = rowBrush;
            var table = new DaisyTable { Content = new StackPanel { Children = { inheritingRow, localRow } } };
            table.Resources[key] = tableBrush;

            RuntimeTestHelpers.AttachToHost(table);
            await RuntimeTestHelpers.EnsureLoadedAsync(table);
            await Task.Delay(200); // the table moves the rows into its own panel after Loaded; they load again there
            table.UpdateLayout();

            // After load a row's content is its root border, whose BorderBrush is the row separator.
            Assert.AreSame(tableBrush, (inheritingRow.Content as Border)?.BorderBrush, "Row without its own brush uses the table resource");
            Assert.AreSame(rowBrush, (localRow.Content as Border)?.BorderBrush, "Row resource wins over the table resource");
        }

        [TestMethod]
        public async Task When_GlobalNeumorphicIsOn_OnlyTheTableGetsASurface()
        {
            // Rows and cells opt out, so a table reads as one raised surface instead of one box per row and cell (#12).
            // The helper injects a Grid tagged DaisyNeumorphicContainer next to its host: one for the table, none inside a row.
            var previousMode = DaisyBaseContentControl.GlobalNeumorphicMode;
            var previousEnabled = DaisyBaseContentControl.GlobalNeumorphicEnabled;
            DaisyBaseContentControl.GlobalNeumorphicEnabled = true;
            try
            {
                // The probes are not attached: WinUI cannot apply the cell and row styles to subclasses without XAML metadata.
                Assert.IsTrue(new ProbeTable().IsEffectivelyNeumorphicEnabled(), "Table");
                Assert.IsFalse(new ProbeRow().IsEffectivelyNeumorphicEnabled(), "Row");
                Assert.IsFalse(new ProbeCell().IsEffectivelyNeumorphicEnabled(), "Cell");

                var rows = new[] { CreateRow("First"), CreateRow("Second") };
                var table = new DaisyTable { Content = new StackPanel { Children = { rows[0], rows[1] } } };

                RuntimeTestHelpers.AttachToHost(table);
                await RuntimeTestHelpers.EnsureLoadedAsync(table);
                await Task.Delay(200); // the table moves the rows into its own panel after Loaded; they load again there
                table.UpdateLayout();

                // Refresh now instead of waiting for the queued refresh timer.
                table.RefreshNeumorphicEffect();
                foreach (var row in rows)
                {
                    row.RefreshNeumorphicEffect();
                    foreach (var cell in Descendants<DaisyTableCell>(row))
                        cell.RefreshNeumorphicEffect();
                }
                table.UpdateLayout();

                Assert.AreEqual(1, Descendants<Grid>(table).Count(g => g.Tag is "DaisyNeumorphicContainer"), "The table has one surface");
                foreach (var row in rows)
                    Assert.IsFalse(Descendants<Grid>(row).Any(g => g.Tag is "DaisyNeumorphicContainer"), "A row or cell drew its own neumorphic surface");
            }
            finally
            {
                DaisyBaseContentControl.GlobalNeumorphicMode = previousMode;
                DaisyBaseContentControl.GlobalNeumorphicEnabled = previousEnabled;
            }
        }

        private sealed class ProbeTable : DaisyTable
        {
            public bool IsEffectivelyNeumorphicEnabled() => IsNeumorphicEffectivelyEnabled();
        }

        private sealed class ProbeRow : DaisyTableRow
        {
            public bool IsEffectivelyNeumorphicEnabled() => IsNeumorphicEffectivelyEnabled();
        }

        private sealed class ProbeCell : DaisyTableCell
        {
            public bool IsEffectivelyNeumorphicEnabled() => IsNeumorphicEffectivelyEnabled();
        }

        // Same shape as XAML content: a horizontal StackPanel of cells.
        private static DaisyTableRow CreateRow(string text)
        {
            var cells = new StackPanel { Orientation = Orientation.Horizontal };
            cells.Children.Add(new DaisyTableCell { Content = text });
            cells.Children.Add(new DaisyTableCell { Content = "Value" });
            return new DaisyTableRow { Content = cells };
        }

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
    }
}
