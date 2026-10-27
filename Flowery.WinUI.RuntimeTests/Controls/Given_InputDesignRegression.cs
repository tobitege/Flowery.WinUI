using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Localization;
using Flowery.Theming;
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
        await ReloadAsync(control);
        control.Label = "Updated";
        control.Size = DaisySize.Large;
        control.Padding = new Thickness(9);
        var input = Descendants(control).OfType<PasswordBox>().Single();
        Assert.AreEqual("Updated", (AutomationProperties.GetLabeledBy(input) as TextBlock)?.Text, "Password label.");
        Assert.IsTrue(AutomationProperties.GetIsRequiredForForm(input), "Password required state.");
        Assert.AreEqual("Hint Help", AutomationProperties.GetHelpText(input), "Password help text.");
        Assert.AreEqual(new Thickness(9), input.Padding, "Password padding.");
        Assert.AreEqual(DaisyResourceLookup.GetSizeDouble(Application.Current.Resources, "DaisySize", DaisySize.Large, "FontSize",
            DaisyResourceLookup.GetDefaultFontSize(DaisySize.Large)), input.FontSize, "Password size.");
    }

    [TestMethod]
    public async Task When_PasswordCultureChanges_RevealActionAndOptionalLabelFollowAfterReload()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        var control = new DaisyPasswordBox { Label = "Password", IsOptional = true, Password = "secret" };
        try
        {
            FloweryLocalization.SetCulture("en");
            await LoadVisualTreeAsync(control);
            var input = Descendants(control).OfType<PasswordBox>().Single();
            var reveal = Descendants(control).OfType<Button>().Single(button => button.Content is Path);
            var optional = Descendants(control).OfType<TextBlock>().Single(text => text.Text == "Optional");
            var toggle = typeof(DaisyPasswordBox).GetMethod("OnRevealButtonClick", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(toggle);
            Assert.AreEqual("Show password", AutomationProperties.GetName(reveal));

            FloweryLocalization.SetCulture("de");
            await EnqueueAsync(control);
            Assert.AreEqual("Passwort anzeigen", AutomationProperties.GetName(reveal));
            toggle.Invoke(control, new object[] { reveal, new RoutedEventArgs() });
            Assert.AreEqual(PasswordRevealMode.Visible, input.PasswordRevealMode);
            Assert.AreEqual("Passwort ausblenden", AutomationProperties.GetName(reveal));

            await Task.Run(static () => FloweryLocalization.SetCulture("fr"));
            await EnqueueAsync(control);
            Assert.AreEqual("Masquer le mot de passe", AutomationProperties.GetName(reveal));
            Assert.AreEqual("Facultatif", optional.Text);
            toggle.Invoke(control, new object[] { reveal, new RoutedEventArgs() });
            Assert.AreEqual(PasswordRevealMode.Hidden, input.PasswordRevealMode);
            Assert.AreEqual("Afficher le mot de passe", AutomationProperties.GetName(reveal));

            await DetachAsync(control);
            FloweryLocalization.SetCulture("en");
            await EnqueueAsync(control);
            Assert.AreEqual("Afficher le mot de passe", AutomationProperties.GetName(reveal));
            Assert.AreEqual("Facultatif", optional.Text);

            await LoadVisualTreeAsync(control);
            Assert.AreEqual("Show password", AutomationProperties.GetName(reveal));
            Assert.AreEqual("Optional", optional.Text);
            FloweryLocalization.SetCulture("de");
            await EnqueueAsync(control);
            Assert.AreEqual("Passwort anzeigen", AutomationProperties.GetName(reveal));
            Assert.AreEqual("secret", control.Password);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(control);
            await EnqueueAsync(control);
            FloweryLocalization.SetCulture(previousCulture);
        }
    }

    [TestMethod]
    public async Task When_OtpCultureChanges_ExistingSlotNamesFollowAfterReloadWithoutChangingTheCode()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        var control = new DaisyOtpInput { Length = 3, Value = "123" };
        try
        {
            FloweryLocalization.SetCulture("en");
            await LoadVisualTreeAsync(control);
            var slots = Descendants(control).OfType<TextBox>().ToArray();
            Assert.HasCount(3, slots);
            AssertSlotNames(slots, "Code digit {0} of {1}");

            FloweryLocalization.SetCulture("de");
            await EnqueueAsync(control);
            AssertSlotNames(slots, "Codeziffer {0} von {1}");
            await Task.Run(static () => FloweryLocalization.SetCulture("fr"));
            await EnqueueAsync(control);
            AssertSlotNames(slots, "Chiffre du code {0} sur {1}");
            CollectionAssert.AreEqual(slots, Descendants(control).OfType<TextBox>().ToArray());
            Assert.AreEqual("123", string.Concat(slots.Select(slot => slot.Text)));

            await DetachAsync(control);
            FloweryLocalization.SetCulture("en");
            await EnqueueAsync(control);
            AssertSlotNames(slots, "Chiffre du code {0} sur {1}");
            await LoadVisualTreeAsync(control);
            AssertSlotNames(slots, "Code digit {0} of {1}");
            CollectionAssert.AreEqual(slots, Descendants(control).OfType<TextBox>().ToArray());
            FloweryLocalization.SetCulture("de");
            await EnqueueAsync(control);
            AssertSlotNames(slots, "Codeziffer {0} von {1}");
            Assert.AreEqual("123", control.Value);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(control);
            await EnqueueAsync(control);
            FloweryLocalization.SetCulture(previousCulture);
        }
    }

    [TestMethod]
    public async Task When_SizeDropdownCultureChanges_SidebarInitializationKeepsOneSubscriptionAndPreservesSelection()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        var previousSize = FlowerySizeManager.CurrentSize;
        var control = new DaisySizeDropdown();
        try
        {
            FloweryLocalization.SetCulture("en");
            FlowerySizeManager.ApplySize(DaisySize.Large);
            control.EnsureInitializedForSidebar("Runtime test before loading");
            AssertSizeDropdownSubscriptions(control, 0);

            await LoadVisualTreeAsync(control);
            control.EnsureInitializedForSidebar("Runtime test first initialization");
            control.EnsureInitializedForSidebar("Runtime test repeated initialization");
            await EnqueueAsync(control);
            AssertSizeDropdownSubscriptions(control, 1);
            var trigger = Descendants(control).OfType<Button>().First();
            AssertSizeSelection(control, trigger, "Large");

            await Task.Run(static () => FloweryLocalization.SetCulture("fr"));
            await EnqueueAsync(control);
            AssertSizeSelection(control, trigger, "Grand");
            var frenchOptions = (SizePreviewInfo[])control.ItemsSource!;
            CollectionAssert.AreEqual(new[] { "Très petit", "Petit", "Moyen", "Grand", "Très grand" },
                frenchOptions.Select(option => option.DisplayName).ToArray());
            AssertSizeDropdownSubscriptions(control, 1);

            await DetachAsync(control);
            AssertSizeDropdownSubscriptions(control, 0);
            await Task.Run(static () => FloweryLocalization.SetCulture("en"));
            await EnqueueAsync(control);
            Assert.AreSame(frenchOptions, control.ItemsSource);

            await LoadVisualTreeAsync(control);
            AssertSizeDropdownSubscriptions(control, 1);
            Assert.AreNotSame(frenchOptions, control.ItemsSource);
            AssertSizeSelection(control, trigger, "Large");
            await Task.Run(static () => FloweryLocalization.SetCulture("fr"));
            await EnqueueAsync(control);
            AssertSizeSelection(control, trigger, "Grand");
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(control);
            await EnqueueAsync(control);
            try
            {
                FloweryLocalization.SetCulture(previousCulture);
            }
            finally
            {
                FlowerySizeManager.ApplySize(previousSize);
            }
        }
    }

    [TestMethod]
    public async Task When_InputReloads_PaddingAndAccessibleLabelStillFollow()
    {
        var control = new DaisyInput { Label = "Field", IsRequired = true };
        await ReloadAsync(control);
        control.Padding = new Thickness(7);
        var input = Descendants(control).OfType<TextBox>().Single();
        Assert.AreEqual(new Thickness(7), input.Padding, "Input padding.");
        Assert.AreEqual("Field", (AutomationProperties.GetLabeledBy(input) as TextBlock)?.Text, "Input label.");
        AutomationProperties.SetName(control, "Explicit field name");
        Assert.AreEqual("Explicit field name", AutomationProperties.GetName(input), "Input explicit automation name.");
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

    private static void AssertSlotNames(TextBox[] slots, string format)
    {
        for (var index = 0; index < slots.Length; index++)
            Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, format, index + 1, slots.Length),
                AutomationProperties.GetName(slots[index]));
    }

    private static void AssertSizeSelection(DaisySizeDropdown control, Button trigger, string text)
    {
        var selected = control.SelectedItem as SizePreviewInfo;
        Assert.IsNotNull(selected);
        Assert.AreEqual(DaisySize.Large, selected.Size);
        Assert.AreEqual(DaisySize.Large, control.SelectedSize);
        Assert.AreEqual(DaisySize.Large, FlowerySizeManager.CurrentSize);
        Assert.AreEqual(text, trigger.Content);
        Assert.AreEqual(text, AutomationProperties.GetName(trigger));
    }

    private static void AssertSizeDropdownSubscriptions(DaisySizeDropdown control, int expected)
    {
        Assert.AreEqual(expected, CountStaticSubscriptions(typeof(FloweryLocalization), nameof(FloweryLocalization.CultureChanged), control));
        Assert.AreEqual(expected, CountStaticSubscriptions(typeof(FlowerySizeManager), nameof(FlowerySizeManager.SizeChanged), control));
    }

    private static int CountStaticSubscriptions(Type owner, string eventName, object target)
    {
        var eventField = owner.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);
        var count = 0;
        if (eventField.GetValue(null) is Delegate handlers)
        {
            foreach (var handler in handlers.GetInvocationList())
            {
                if (ReferenceEquals(handler.Target, target))
                    count++;
            }
        }
        return count;
    }

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

    private static async Task ReloadAsync(FrameworkElement element)
    {
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element);
        await DetachAsync(element);
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element);
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
