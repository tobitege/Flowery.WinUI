using System;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Flowery.Helpers;
using Flowery.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Windows.UI;

namespace Flowery.Services;

internal static class DaisySelectorAutomationPeers
{
    internal static void RaiseRangeValueChanged(FrameworkElement owner, double oldValue, double newValue)
    {
        if (oldValue != newValue)
            FrameworkElementAutomationPeer.FromElement(owner)?.RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty, oldValue, newValue);
    }

    internal static void RaiseColorValueChanged(FrameworkElement owner, Color oldValue, Color newValue)
    {
        if (oldValue != newValue)
            FrameworkElementAutomationPeer.FromElement(owner)?.RaisePropertyChangedEvent(
                ValuePatternIdentifiers.ValueProperty,
                FloweryColorHelpers.ColorToHex(oldValue, true), FloweryColorHelpers.ColorToHex(newValue, true));
    }
}

internal abstract partial class DaisyRangeValueAutomationPeer(FrameworkElement owner)
    : FrameworkElementAutomationPeer(owner), IRangeValueProvider
{
    public abstract double Minimum { get; }
    public abstract double Maximum { get; }
    public abstract double Value { get; }
    public abstract double SmallChange { get; }
    public abstract double LargeChange { get; }
    public virtual bool IsReadOnly => Owner is Control { IsEnabled: false };
    protected abstract void SetOwnerValue(double value);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override object GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.RangeValue ? this : base.GetPatternCore(patternInterface);

    public void SetValue(double value)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The control is read-only or disabled.");
        if (!double.IsFinite(value) || value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        SetOwnerValue(value);
    }
}

internal sealed partial class DaisyRangeAutomationPeer(DaisyRange owner) : DaisyRangeValueAutomationPeer(owner)
{
    private DaisyRange Range => (DaisyRange)Owner;
    public override double Minimum => Range.Minimum;
    public override double Maximum => Range.Maximum;
    public override double Value => Range.Value;
    public override double SmallChange => Range.SmallChange;
    public override double LargeChange => Range.LargeChange;
    protected override void SetOwnerValue(double value) => Range.Value = value;
}

internal sealed partial class DaisyRatingAutomationPeer(DaisyRating owner) : DaisyRangeValueAutomationPeer(owner)
{
    private DaisyRating Rating => (DaisyRating)Owner;
    public override double Minimum => 0;
    public override double Maximum => Rating.Maximum;
    public override double Value => Rating.Value;
    public override double SmallChange => Rating.Precision switch
    {
        RatingPrecision.Half => 0.5,
        RatingPrecision.Precise => 0.1,
        _ => 1
    };
    public override double LargeChange => SmallChange * 5;
    public override bool IsReadOnly => base.IsReadOnly || Rating.IsReadOnly;
    protected override void SetOwnerValue(double value) => Rating.Value = value;
}

internal sealed partial class DaisyColorSliderAutomationPeer(DaisyColorSlider owner) : DaisyRangeValueAutomationPeer(owner)
{
    private DaisyColorSlider Slider => (DaisyColorSlider)Owner;
    public override double Minimum => Slider.Minimum;
    public override double Maximum => Slider.Maximum;
    public override double Value => Slider.Value;
    public override double SmallChange => 1;
    public override double LargeChange => 10;
    protected override void SetOwnerValue(double value) => Slider.Value = value;
}

internal sealed partial class DaisyColorValueAutomationPeer(FrameworkElement owner)
    : FrameworkElementAutomationPeer(owner), IValueProvider
{
    private Color Color => Owner switch
    {
        DaisyColorWheel wheel => wheel.Color,
        DaisyColorGrid grid => grid.Color,
        _ => throw new InvalidOperationException("Unsupported color selector.")
    };
    public bool IsReadOnly => Owner is Control { IsEnabled: false };
    public string Value => FloweryColorHelpers.ColorToHex(Color, true);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override object GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPatternCore(patternInterface);

    public void SetValue(string value)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The control is disabled.");
        if (!FloweryColorHelpers.TryParseColor(value, out var color))
            throw new ArgumentException("Use a valid color value.", nameof(value));
        switch (Owner)
        {
            case DaisyColorWheel wheel: wheel.Color = color; break;
            case DaisyColorGrid grid: grid.Color = color; break;
        }
    }
}
