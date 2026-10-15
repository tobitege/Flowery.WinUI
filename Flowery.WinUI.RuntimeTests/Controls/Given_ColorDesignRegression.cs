using System;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_ColorDesignRegression
{
    [TestMethod]
    public void When_PaletteSelectionChanges_OneEventUpdatesDialog()
    {
        var dialog = new DaisyColorPickerDialog();
        var grid = Find<DaisyColorGrid>(dialog);
        Assert.IsNotNull(grid);
        grid.Palette = new ColorCollection(new[] { Colors.Red, Colors.Blue });
        grid.ShowCustomColors = false;
        var count = 0;
        grid.ColorChanged += (_, _) => count++;

        grid.ColorIndex = 1;

        Assert.AreEqual(Colors.Blue, grid.Color);
        Assert.AreEqual(Colors.Blue, dialog.Color);
        Assert.AreEqual(1, count);
        grid.ColorIndex = -1;
        Assert.AreEqual(Colors.Blue, grid.Color);
    }

    [TestMethod]
    public void When_HslIsAssigned_AllEditorComponentsAndEventFollow()
    {
        var editor = new DaisyColorEditor();
        var expected = Color.FromArgb(128, 0, 255, 0);
        var count = 0;
        editor.ColorChanged += (_, _) => count++;
        editor.HslColor = new HslColor(expected);
        Assert.AreEqual(expected, editor.Color);
        Assert.AreEqual((byte)128, editor.Alpha);
        Assert.AreEqual((byte)255, editor.Green);
        Assert.AreEqual("#8000FF00", editor.HexValue);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task When_WheelPointerChangesColor_AlphaIsPreserved()
    {
        var wheel = new DaisyColorWheel { Width = 180, Height = 180, Color = Color.FromArgb(80, 255, 0, 0) };
        RuntimeTestHelpers.AttachToHost(wheel);
        await RuntimeTestHelpers.EnsureLoadedAsync(wheel);
        wheel.UpdateLayout();
        var method = typeof(DaisyColorWheel).GetMethod("UpdateColorFromPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(wheel, new object[] { new Point(90, 90) });
        Assert.AreEqual((byte)80, wheel.Color.A);
    }

    [TestMethod]
    public void When_EditingModeChanges_TheRequestedColorControlsAreShown()
    {
        var editor = new DaisyColorEditor();
        var root = (StackPanel)editor.Content;
        var rgb = (StackPanel)root.Children[1];
        var hsl = (StackPanel)root.Children[2];
        Assert.AreEqual(Visibility.Visible, rgb.Visibility);
        Assert.AreEqual(Visibility.Collapsed, hsl.Visibility);
        editor.EditingMode = ColorEditingMode.Hsl;
        Assert.AreEqual(Visibility.Collapsed, rgb.Visibility);
        Assert.AreEqual(Visibility.Visible, hsl.Visibility);
    }

    [TestMethod]
    public void When_AutomationSetsSelectorValues_ControlsUpdateAndRespectReadOnly()
    {
        var range = new DaisyRange { Minimum = 0, Maximum = 10 };
        var rangePeer = FrameworkElementAutomationPeer.CreatePeerForElement(range);
        var rangeProvider = rangePeer.GetPattern(PatternInterface.RangeValue) as IRangeValueProvider;
        Assert.IsNotNull(rangeProvider);
        rangeProvider.SetValue(7);
        Assert.AreEqual(7d, range.Value);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => rangeProvider.SetValue(11));

        var rating = new DaisyRating { IsReadOnly = true };
        var ratingProvider = FrameworkElementAutomationPeer.CreatePeerForElement(rating)
            .GetPattern(PatternInterface.RangeValue) as IRangeValueProvider;
        Assert.IsNotNull(ratingProvider);
        Assert.IsTrue(ratingProvider.IsReadOnly);
        Assert.ThrowsExactly<InvalidOperationException>(() => ratingProvider.SetValue(2));

        var slider = new DaisyColorSlider { Channel = ColorSliderChannel.Blue };
        var sliderProvider = FrameworkElementAutomationPeer.CreatePeerForElement(slider)
            .GetPattern(PatternInterface.RangeValue) as IRangeValueProvider;
        Assert.IsNotNull(sliderProvider);
        sliderProvider.SetValue(120);
        Assert.AreEqual((byte)120, slider.Color.B);

        var grid = new DaisyColorGrid();
        var gridProvider = FrameworkElementAutomationPeer.CreatePeerForElement(grid)
            .GetPattern(PatternInterface.Value) as IValueProvider;
        Assert.IsNotNull(gridProvider);
        gridProvider.SetValue("#80010203");
        Assert.AreEqual(Color.FromArgb(128, 1, 2, 3), grid.Color);
    }

    private static T? Find<T>(object? root) where T : DependencyObject
    {
        if (root is T match) return match;
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
                if (Find<T>(child) is { } result) return result;
        }
        if (root is Border border) return Find<T>(border.Child);
        if (root is ScrollViewer scroller) return Find<T>(scroller.Content);
        if (root is ContentPresenter presenter) return Find<T>(presenter.Content);
        if (root is ContentControl control) return Find<T>(control.Content);
        return null;
    }
}
