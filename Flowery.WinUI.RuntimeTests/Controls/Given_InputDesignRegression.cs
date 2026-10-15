using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public partial class Given_InputDesignRegression
{
    [TestMethod]
    public async Task When_PasswordReloads_LabelAndSizeStillUpdate()
    {
        var control = new DaisyPasswordBox { Label = "Original", IsRequired = true, HintText = "Hint", HelperText = "Help" };
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        await DetachAsync(control);
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        control.Label = "Updated";
        control.Size = DaisySize.Large;
        control.Padding = new Thickness(9);
        var input = Descendants(control).OfType<PasswordBox>().Single();
        Assert.AreEqual("Updated", (AutomationProperties.GetLabeledBy(input) as TextBlock)?.Text);
        Assert.IsTrue(AutomationProperties.GetIsRequiredForForm(input));
        Assert.AreEqual("Hint Help", AutomationProperties.GetHelpText(input));
        Assert.AreEqual(new Thickness(9), input.Padding);
    }

    [TestMethod]
    public async Task When_InputReloads_PaddingAndAccessibleLabelStillFollow()
    {
        var control = new DaisyInput { Label = "Field", IsRequired = true };
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        await DetachAsync(control);
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        control.Padding = new Thickness(7);
        var input = Descendants(control).OfType<TextBox>().Single();
        Assert.AreEqual(new Thickness(7), input.Padding);
        Assert.AreEqual("Field", (AutomationProperties.GetLabeledBy(input) as TextBlock)?.Text);
        AutomationProperties.SetName(control, "Explicit field name");
        Assert.AreEqual("Explicit field name", AutomationProperties.GetName(input));
    }

    [TestMethod]
    public async Task When_SelectUsesDisplayMember_VisibleTextTracksTheModel()
    {
        var item = new DisplayItem { Name = "Alpha" };
        var control = new DaisySelect { DisplayMemberPath = nameof(DisplayItem.Name), SelectedItem = item };
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        var trigger = Descendants(control).OfType<Button>().First();
        Assert.AreEqual("Alpha", trigger.Content);
        item.Name = "Beta";
        await EnqueueAsync(control);
        Assert.AreEqual("Beta", trigger.Content);
    }

    [TestMethod]
    public async Task When_NumericInputIsReadOnly_SpinnersCannotMutateIt()
    {
        var control = new DaisyNumericUpDown { Value = 20, IsReadOnly = true };
        await LoadVisualTreeAsync(control);
        var spinners = Descendants(control).OfType<RepeatButton>().ToArray();
        Assert.HasCount(2, spinners);
        Assert.IsTrue(spinners.All(button => !button.IsEnabled));
        foreach (var methodName in new[] { "Increase", "Decrease" })
        {
            var method = typeof(DaisyNumericUpDown).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(control, null);
        }
        Assert.AreEqual(20m, control.Value);
    }

    [TestMethod]
    public async Task When_SwapStateIsBound_AllThreeContentsFollow()
    {
        var control = new DaisySwap { OnContent = "On", OffContent = "Off", IndeterminateContent = "Mixed", IsThreeState = true };
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        control.IsChecked = true;
        Assert.AreEqual("On", VisibleSwapContent(control));
        control.IsChecked = null;
        Assert.AreEqual("Mixed", VisibleSwapContent(control));
        control.IsChecked = false;
        Assert.AreEqual("Off", VisibleSwapContent(control));
    }

    [TestMethod]
    public async Task When_CheckboxBecomesIndeterminate_ItUsesMixedIndicator()
    {
        var control = new DaisyCheckBox { IsThreeState = true, IsChecked = true };
        await LoadVisualTreeAsync(control);
        var indicator = Descendants(control).OfType<Path>().Single();
        control.IsChecked = null;
        Assert.AreEqual(Visibility.Visible, indicator.Visibility);
        Assert.HasCount(1, ((PathGeometry)indicator.Data).Figures[0].Segments);
        control.IsChecked = false;
        Assert.AreEqual(Visibility.Collapsed, indicator.Visibility);
    }

    [TestMethod]
    public async Task When_IconVariantChanges_ItsOwnedForegroundUpdates()
    {
        var control = new DaisyIconText { Text = "Icon", Variant = DaisyBadgeVariant.Primary };
        RuntimeTestHelpers.AttachToHost(control);
        await RuntimeTestHelpers.EnsureLoadedAsync(control);
        var oldBrush = control.Foreground;
        control.Variant = DaisyBadgeVariant.Secondary;
        Assert.AreNotSame(oldBrush, control.Foreground);
        var customBrush = new SolidColorBrush(Microsoft.UI.Colors.Magenta);
        control.Foreground = customBrush;
        control.Variant = DaisyBadgeVariant.Accent;
        Assert.AreSame(customBrush, control.Foreground);
    }

    [TestMethod]
    public async Task When_FabStartsOpen_ItsActionsAreVisible()
    {
        var action = new DaisyButton { Content = "Action" };
        var control = new DaisyFab { IsOpen = true, Content = new StackPanel { Children = { action } } };
        await LoadVisualTreeAsync(control);
        Assert.IsNotNull(action.Parent);
        Assert.AreEqual(Visibility.Visible, ((UIElement)action.Parent).Visibility);
    }

    [TestMethod]
    public async Task When_NavbarShadowIsDisabled_TheNativeShadowCollectionIsEmpty()
    {
        var control = new DaisyNavbar();
        await LoadVisualTreeAsync(control);
        var shadow = Descendants(control).OfType<DaisyShadowContainer>().Single();
        Assert.HasCount(1, shadow.Shadows);
        control.HasShadow = false;
        Assert.HasCount(0, shadow.Shadows);
    }

    [TestMethod]
    public async Task When_TextAreaResizeIsDisabled_TheGripIsHidden()
    {
        var control = new DaisyTextArea();
        await LoadVisualTreeAsync(control);
        var grip = Descendants(control).OfType<Thumb>().Single();
        Assert.AreEqual(Visibility.Visible, ((UIElement)grip.Parent).Visibility);
        control.CanResize = false;
        Assert.AreEqual(Visibility.Collapsed, ((UIElement)grip.Parent).Visibility);
    }

    private static object? VisibleSwapContent(DaisySwap control) =>
        ((Grid)control.Content).Children.OfType<ContentPresenter>().Single(presenter => presenter.Visibility == Visibility.Visible).Content;

    private static async Task LoadVisualTreeAsync(FrameworkElement element)
    {
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element).WaitAsync(TimeSpan.FromSeconds(5));
        // These controls assign Content during Loaded. Its presenter materializes on the following layout pass.
        var ready = new TaskCompletionSource();
        if (!element.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => ready.TrySetResult()))
            throw new InvalidOperationException("The dispatcher rejected visual tree initialization.");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        element.UpdateLayout();
    }

    private static Task EnqueueAsync(FrameworkElement element)
    {
        var tcs = new TaskCompletionSource();
        element.DispatcherQueue.TryEnqueue(() => tcs.TrySetResult());
        return tcs.Task;
    }

    private static async Task DetachAsync(FrameworkElement element)
    {
        var tcs = new TaskCompletionSource();
        RoutedEventHandler? handler = null;
        handler = (_, _) => { element.Unloaded -= handler; tcs.TrySetResult(); };
        element.Unloaded += handler;
        RuntimeTestHelpers.GetHostPanel().Children.Remove(element);
        await tcs.Task;
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [WinRT.GeneratedBindableCustomProperty]
    public sealed partial class DisplayItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
