using System.Text.Json;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Controls.Scheduler.Utilities;
using Flowery.Localization;
using Flowery.Theming;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls;

/// <summary>Ports of the Flowery.NET scheduler tests. Pointer and key input goes through the
/// control's internal gesture entry points, which the native event handlers also call.</summary>
[TestClass]
public sealed partial class Given_DaisyScheduler
{
    private static readonly DateTime Monday = new(2026, 10, 12);
    private static readonly SchedulerView[] ListViews =
        [SchedulerView.Day, SchedulerView.Week, SchedulerView.Month, SchedulerView.Agenda, SchedulerView.Planner, SchedulerView.Timeline];

    [TestMethod]
    public async Task When_EveryViewRendersHostAppointments_SelectionWorksWithoutMovingTheFrame()
    {
        foreach (var view in ListViews)
            await RuntimeTestHelpers.RunCaseAsync(view.ToString(), () => VerifySelectionKeepsTheFrameAsync(view));
    }

    private static async Task VerifySelectionKeepsTheFrameAsync(SchedulerView view)
    {
        var scope = await SchedulerScope.CreateAsync(view);
        var block = scope.Frame(scope.Appointment);
        Assert.IsTrue(block.ActualWidth > 0);
        Assert.IsTrue(block.ActualHeight > 0);
        var content = (FrameworkElement)((Panel)block.Child).Children[0];
        var size = new Size(block.ActualWidth, block.ActualHeight);
        var position = content.TransformToVisual(scope.Scheduler).TransformPoint(default);
        await scope.ClickAsync(block);
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.HasCount(1, scope.Scheduler.SelectedAppointments);
        // The selection ring lies over the frame, so selecting neither rebuilds nor moves the appointment.
        Assert.AreSame(block, scope.Frame(scope.Appointment));
        Assert.AreEqual(size, new Size(block.ActualWidth, block.ActualHeight));
        Assert.AreEqual(position, content.TransformToVisual(scope.Scheduler).TransformPoint(default));
    }

    [TestMethod]
    public async Task When_DragMoving_ReportsSnappedProposalWithoutMutatingHostData()
    {
        // A later occurrence of a daily series moves the whole series by the same delta.
        foreach (var (label, daily, end, dayOffset, newStart, newEnd) in new[]
                 {
                     ("single", false, Monday.AddHours(10), 0, Monday.AddHours(10), Monday.AddHours(11)),
                     ("later daily occurrence", true, Monday.AddDays(2).AddHours(10), 2, Monday.AddHours(10), Monday.AddDays(2).AddHours(11))
                 })
            await RuntimeTestHelpers.RunCaseAsync(label, () => VerifyDragMoveAsync(daily, end, dayOffset, newStart, newEnd));
    }

    private static async Task VerifyDragMoveAsync(bool daily, DateTime end, int dayOffset, DateTime newStart, DateTime newEnd)
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Day);
        if (daily)
        {
            scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
            scope.Appointment.End = end;
            scope.Scheduler.SelectedDate = new DateTimeOffset(Monday.AddDays(dayOffset));
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            await scope.LayoutAsync();
        }
        AppointmentMoveArgs? request = null;
        scope.Scheduler.AppointmentMoveRequested += (_, args) => request = args;
        var block = scope.Frame(scope.Appointment);
        var origin = scope.Point(block, block.ActualWidth / 2, block.ActualHeight / 2);
        await scope.DragAsync(block, origin, Offset(origin, 0, 48));
        Assert.IsNotNull(request);
        Assert.AreSame(scope.Appointment, request.Appointment);
        Assert.AreEqual(newStart, request.NewStart);
        Assert.AreEqual(newEnd, request.NewEnd);
        Assert.AreEqual(Monday.AddHours(9), scope.Appointment.Start);
        Assert.AreEqual(end, scope.Appointment.End);
    }

    [TestMethod]
    public async Task When_ResizingTheSelectedEdge_ReportsDurationWithoutMutatingHostData()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Day);
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        await scope.LayoutAsync();
        AppointmentResizeArgs? request = null;
        scope.Scheduler.AppointmentResizeRequested += (_, args) => request = args;
        var block = scope.Frame(scope.Appointment);
        var origin = scope.Point(block, block.ActualWidth / 2, block.ActualHeight - 2);
        await scope.DragAsync(block, origin, Offset(origin, 0, 24));
        Assert.IsNotNull(request);
        Assert.AreEqual(Monday.AddHours(9), request.NewStart);
        Assert.AreEqual(Monday.AddHours(10.5), request.NewEnd);
        Assert.AreEqual(Monday.AddHours(10), scope.Appointment.End);
    }

    [TestMethod]
    public async Task When_DraggingAnEmptyTimeRange_RequestsCreation()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Day);
        AppointmentCreateArgs? request = null;
        scope.Scheduler.AppointmentCreateRequested += (_, args) => request = args;
        var surface = Descendants<Canvas>(scope.Scheduler).Single(canvas => canvas.Height == 1152 && canvas.Width > 100);
        var origin = scope.Point(surface, surface.Width / 2, 11 * 60 * 0.8);
        await scope.DragLaneAsync(surface, origin, Offset(origin, 0, 48));
        Assert.IsNotNull(request);
        Assert.AreEqual(Monday.AddHours(11), request.Start);
        Assert.AreEqual(Monday.AddHours(12.5), request.End);
        Assert.IsFalse(request.IsAllDay);
        Assert.HasCount(1, scope.Scheduler.Appointments!);
    }

    [TestMethod]
    public async Task When_ReadOnly_MoveResizeCreateAndDeleteAreBlockedButSelectionRemains()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Day);
        scope.Scheduler.IsReadOnly = true;
        await scope.LayoutAsync();
        var writes = 0;
        scope.Scheduler.AppointmentMoveRequested += (_, _) => writes++;
        scope.Scheduler.AppointmentResizeRequested += (_, _) => writes++;
        scope.Scheduler.AppointmentCreateRequested += (_, _) => writes++;
        scope.Scheduler.AppointmentsDeleteRequested += (_, _) => writes++;
        var block = scope.Frame(scope.Appointment);
        var origin = scope.Point(block, block.ActualWidth / 2, block.ActualHeight / 2);
        await scope.DragAsync(block, origin, Offset(origin, 0, 48));
        scope.Scheduler.DeleteSelected();
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.AreEqual(0, writes);
    }

    [TestMethod]
    public async Task When_AppointmentIsLocked_SelectionAndDeleteAreBlockedButContextIsRequested()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        scope.Appointment.IsLocked = true;
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        await scope.LayoutAsync();
        var block = scope.Frame(scope.Appointment);
        await scope.ClickAsync(block);
        Assert.IsNull(scope.Scheduler.SelectedAppointment);
        AppointmentContextRequestedArgs? context = null;
        scope.Scheduler.AppointmentContextRequested += (_, args) => context = args;
        await scope.ClickAsync(block, rightButton: true);
        Assert.IsNotNull(context);
        Assert.AreSame(scope.Appointment, context.Appointment);
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        Assert.IsEmpty(scope.Scheduler.SelectedAppointments);
    }

    [TestMethod]
    public async Task When_DeleteKeyIsPressed_SelectionIsReportedAndTheInputListStaysIntact()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        AppointmentsDeleteArgs? request = null;
        scope.Scheduler.AppointmentsDeleteRequested += (_, args) => request = args;
        await scope.ClickAsync(scope.Frame(scope.Appointment));
        Assert.IsTrue(scope.Scheduler.HandleKey(VirtualKey.Delete, false));
        Assert.IsNotNull(request);
        Assert.AreSame(scope.Appointment, request.Appointments.Single());
        Assert.HasCount(1, scope.Scheduler.Appointments!);
        Assert.IsEmpty(scope.Scheduler.SelectedAppointments);
    }

    [TestMethod]
    public async Task When_HostRefreshes_ScrollIsKeptAndSelectionMatchesTheReplacedInstance()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Day);
        var body = scope.BodyScroll();
        body.ChangeView(null, 480, null, true);
        await scope.LayoutAsync();
        // ChangeView takes effect with a later layout pass, which a busy runner can delay past LayoutAsync.
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(body.VerticalOffset - 480) <= 1, "the body scrolled to 480");
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        var updated = scope.Appointment.Clone();
        updated.Subject = "Updated";
        scope.Scheduler.Appointments = new[] { updated };
        await scope.LayoutAsync();
        var refreshed = scope.BodyScroll();
        Assert.AreNotSame(body, refreshed);
        // The new body restores the offset in its Loaded handler, which WinUI raises asynchronously.
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(refreshed.VerticalOffset - 480) <= 1,
            $"the refreshed body kept the offset 480 (it shows {refreshed.VerticalOffset})");
        Assert.AreSame(updated, scope.Scheduler.SelectedAppointment);
        Assert.IsTrue(Descendants<TextBlock>(scope.Scheduler).Any(text => text.Text == "Updated"));
    }

    [TestMethod]
    public async Task When_SizeChanges_FontsAndCornerRadiusFollowAndHostValuesAreKept()
    {
        var previous = FlowerySizeManager.CurrentSize;
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        try
        {
            var medium = scope.Scheduler.CornerRadius;
            scope.Scheduler.ClearValue(DaisyScheduler.SizeProperty);
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            await scope.LayoutAsync();
            Assert.AreEqual(DaisySize.Auto, scope.Scheduler.Size);
            Assert.AreEqual(DaisySize.ExtraLarge, scope.Scheduler.ResolvedSize);
            Assert.AreEqual(20, scope.Scheduler.FontSize);
            Assert.AreEqual(DaisyResourceLookup.GetCornerRadius("DaisySizeExtraLargeCornerRadius"), scope.Scheduler.CornerRadius);
            Assert.AreNotEqual(medium, scope.Scheduler.CornerRadius);
            foreach (var button in Descendants<DaisyButton>(scope.Scheduler))
                Assert.AreEqual(DaisySize.ExtraLarge, button.ResolvedSize);
            // An explicit size and a corner radius the host set win over the global size.
            scope.Scheduler.CornerRadius = new CornerRadius(3);
            scope.Scheduler.Size = DaisySize.Small;
            FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
            await scope.LayoutAsync();
            Assert.AreEqual(DaisySize.Small, scope.Scheduler.ResolvedSize);
            Assert.AreEqual(12, scope.Scheduler.FontSize);
            Assert.AreEqual(new CornerRadius(3), scope.Scheduler.CornerRadius);
        }
        finally { FlowerySizeManager.ApplySize(previous); }
    }

    [TestMethod]
    public async Task When_ThemeChanges_AppointmentColorsRefreshAndCustomFillsKeepReadableText()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        try
        {
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Light"));
            await scope.LayoutAsync();
            // Theme changes mutate the shared brush. Capture the color before switching.
            var light = ((SolidColorBrush)Context(scope.Frame(scope.Appointment)).BackBrush).Color;
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Dark"));
            await scope.LayoutAsync();
            Assert.AreNotEqual(light, ((SolidColorBrush)Context(scope.Frame(scope.Appointment)).BackBrush).Color);
            scope.Appointment.BackColor = Colors.Black;
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            await scope.LayoutAsync();
            Assert.AreEqual(Colors.White, ((SolidColorBrush)Context(scope.Frame(scope.Appointment)).Foreground).Color);
        }
        finally { DaisyThemeManager.ApplyTheme(previous ?? "Light"); }
    }

    [TestMethod]
    public async Task When_TagsHaveTheirOwnFill_TheirTextKeepsReadableContrast()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            foreach (var (theme, darkAppointment) in new[] { ("Coffee", false), ("Light", false), ("Dark", false), ("Corporate", false), ("Light", true) })
                await RuntimeTestHelpers.RunCaseAsync($"{theme}, dark appointment={darkAppointment}",
                    () => VerifyTagContrastAsync(theme, darkAppointment));
        }
        finally { DaisyThemeManager.ApplyTheme(previous ?? "Light"); }
    }

    private static async Task VerifyTagContrastAsync(string theme, bool darkAppointment)
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        Assert.IsTrue(DaisyThemeManager.ApplyTheme(theme));
        if (darkAppointment)
            scope.Appointment.BackColor = Colors.Black;
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Alex" });
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Dark tag", BackColor = Colors.Black });
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Light tag", BackColor = Colors.White });
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Translucent tag", BackColor = Color.FromArgb(128, 255, 255, 255) });
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Explicit tag", BackColor = Colors.Black, ForeColor = Colors.Orange });
        scope.Appointment.Adornments.Add(new AppointmentDecoration { Text = "Overlay" });
        scope.Scheduler.ShowTiles = true;
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        await scope.LayoutAsync();

        var frame = scope.Frame(scope.Appointment);
        var labels = Descendants<TextBlock>(frame).ToArray();
        var context = Context(frame);
        var backdrop = ((SolidColorBrush)context.BackBrush).Color;
        foreach (var name in new[] { "Alex", "Dark tag", "Light tag", "Translucent tag" })
        {
            var label = labels.Single(text => text.Text == name);
            var badge = (Border)label.Parent;
            var fill = ((SolidColorBrush)badge.Background).Color;
            var textColor = ((SolidColorBrush)label.Foreground).Color;
            var opacity = fill.A / 255d;
            var rendered = Color.FromArgb(255,
                (byte)Math.Round(fill.R * opacity + backdrop.R * (1 - opacity)),
                (byte)Math.Round(fill.G * opacity + backdrop.G * (1 - opacity)),
                (byte)Math.Round(fill.B * opacity + backdrop.B * (1 - opacity)));
            var foregroundLuminance = Luminance(textColor);
            var backgroundLuminance = Luminance(rendered);
            var ratio = (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05) /
                (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);
            Assert.IsTrue(ratio >= 4.5, $"{name}: contrast {ratio:F2}; text {textColor}, fill {fill}, appointment {backdrop}");
        }
        Assert.AreEqual(Colors.Orange, ((SolidColorBrush)labels.Single(text => text.Text == "Explicit tag").Foreground).Color);
        Assert.AreEqual(((SolidColorBrush)context.Foreground).Color,
            ((SolidColorBrush)labels.Single(text => text.Text == "Overlay").Foreground).Color);

        static double Luminance(Color color)
        {
            static double Linear(byte channel) => channel <= 10 ? channel / 3294.6 : Math.Pow((channel / 255d + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }
    }

    [TestMethod]
    public async Task When_PlannerConvertsDisplayedTimes_SelectionReturnsTheOriginalAppointment()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, Name = "New York", TimeZoneId = "America/New_York" } };
        scope.Appointment.Start = Monday.AddHours(13);
        scope.Appointment.End = Monday.AddHours(14);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        await scope.LayoutAsync();
        var frame = scope.Frame(scope.Appointment);
        Assert.AreEqual(Monday.AddHours(9), Context(frame).Start);
        await scope.ClickAsync(frame);
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.AreEqual(Monday.AddHours(13), scope.Appointment.Start);
    }

    [TestMethod]
    public async Task When_TooltipIsShown_ItsTextInheritsTheTooltipPaletteAndAHostStyleIsKept()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            foreach (var theme in new[] { "Corporate", "Light", "Dark", "Coffee" })
                await RuntimeTestHelpers.RunCaseAsync(theme, () => VerifyTooltipPaletteAsync(theme));

            // A tooltip the host supplies keeps its own style.
            var hostStyle = new Style(typeof(ToolTip));
            var hosted = await SchedulerScope.CreateAsync(SchedulerView.Planner);
            hosted.Scheduler.AppointmentTooltipRenderer = _ => new ToolTip { Style = hostStyle, Content = "Host" };
            await hosted.LayoutAsync();
            Assert.AreSame(hostStyle, ((ToolTip)ToolTipService.GetToolTip(hosted.Frame(hosted.Appointment))).Style);
        }
        finally { DaisyThemeManager.ApplyTheme(previous ?? "Light"); }
    }

    private static async Task VerifyTooltipPaletteAsync(string theme)
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        Assert.IsTrue(DaisyThemeManager.ApplyTheme(theme));
        scope.Appointment.Description = "Appointment note";
        scope.Appointment.Memo = "Internal memo";
        scope.Appointment.Details.Add(new AppointmentDetail("Resource", "Alex"));
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        await scope.LayoutAsync();
        var tooltip = (ToolTip)ToolTipService.GetToolTip(scope.Frame(scope.Appointment));
        var content = (StackPanel)tooltip.Content;
        var background = ((SolidColorBrush)tooltip.Background).Color;
        var foreground = ((SolidColorBrush)tooltip.Foreground).Color;
        Assert.AreEqual(((SolidColorBrush)DaisyResourceLookup.GetBrush("DaisyBase200Brush")).Color, background);
        Assert.AreNotEqual(background, ((SolidColorBrush)tooltip.BorderBrush).Color);
        Assert.IsTrue(tooltip.BorderThickness.Left > 0);
        Assert.AreNotEqual(background, foreground);
        foreach (var label in content.Children.OfType<TextBlock>())
            Assert.AreEqual(DependencyProperty.UnsetValue, label.ReadLocalValue(TextBlock.ForegroundProperty), label.Text);
    }

    [TestMethod]
    public async Task When_MonthStartsOnSunday_TheMidnightEndIsExclusive()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        scope.Appointment.IsAllDay = true;
        scope.Appointment.Start = Monday;
        scope.Appointment.End = Monday.AddDays(1);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Scheduler.FirstDayOfWeek = DayOfWeek.Sunday;
        await scope.LayoutAsync();
        Assert.HasCount(1, scope.Frames(scope.Appointment));
        var grid = Descendants<Grid>(scope.Scheduler).Single(control => control.RowDefinitions.Count == 7 && control.ColumnDefinitions.Count == 7);
        var header = (Border)grid.Children[0];
        Assert.AreEqual(new DateTime(2026, 9, 27).ToString("ddd", scope.Scheduler.FormattingCulture), HeadingText(header).Text);
    }

    [TestMethod]
    public async Task When_WeekendsAreHidden_MonthAndWeekShowFiveWeekdays()
    {
        var month = await SchedulerScope.CreateAsync(SchedulerView.Month);
        var saturday = new AppointmentModel { Id = 2, Subject = "Weekend", Start = Monday.AddDays(5).AddHours(9), End = Monday.AddDays(5).AddHours(10) };
        month.Scheduler.Appointments = new[] { month.Appointment, saturday };
        month.Scheduler.ShowWeekends = false;
        await month.LayoutAsync();
        var grid = Descendants<Grid>(month.Scheduler).Single(control => control.RowDefinitions.Count == 7);
        Assert.HasCount(5, grid.ColumnDefinitions);
        Assert.AreEqual(Monday.ToString("ddd", month.Scheduler.FormattingCulture), HeadingText((Border)grid.Children[0]).Text);
        Assert.HasCount(1, month.Frames(month.Appointment));
        Assert.IsEmpty(month.Frames(saturday));
        month.Scheduler.ShowWeekends = true;
        await month.LayoutAsync();
        Assert.HasCount(7, Descendants<Grid>(month.Scheduler).Single(control => control.RowDefinitions.Count == 7).ColumnDefinitions);
        Assert.HasCount(1, month.Frames(saturday));

        var week = await SchedulerScope.CreateAsync(SchedulerView.Week);
        week.Scheduler.Appointments = new[] { week.Appointment, saturday };
        week.Scheduler.ShowWeekends = false;
        await week.LayoutAsync();
        Assert.HasCount(5, Descendants<Canvas>(week.Scheduler).Where(canvas => canvas.AllowDrop));
        Assert.HasCount(1, week.Frames(week.Appointment));
        Assert.IsEmpty(week.Frames(saturday));

        var planner = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        saturday.ResourceIds.Add(1);
        var friday = new AppointmentModel { Id = 3, Subject = "Friday", Start = Monday.AddDays(4).AddHours(9), End = Monday.AddDays(4).AddHours(10), ResourceIds = { 1 } };
        var overWeekend = new AppointmentModel { Id = 4, Subject = "Over the weekend", Start = Monday.AddDays(4).AddHours(12), End = Monday.AddDays(7).AddHours(12), ResourceIds = { 1 } };
        planner.Scheduler.Appointments = new[] { planner.Appointment, saturday, friday, overWeekend };
        planner.Scheduler.ShowWeekends = false;
        planner.Scheduler.SingleClickCreates = true;
        await planner.LayoutAsync();
        var lane = Descendants<Canvas>(planner.Scheduler).Single(canvas => canvas.AllowDrop);
        var day = lane.Width / 5;
        Assert.IsEmpty(planner.Frames(saturday));
        Assert.AreEqual(4 * day + 9 * day / 24, Canvas.GetLeft(planner.Frame(friday)), 0.5);
        // The hidden weekend takes no space: the bar from Friday noon runs to the end of the lane.
        Assert.AreEqual(4 * day + 12 * day / 24, Canvas.GetLeft(planner.Frame(overWeekend)), 0.5);
        Assert.AreEqual(lane.Width, Canvas.GetLeft(planner.Frame(overWeekend)) + planner.Frame(overWeekend).Width + 2, 0.5);
        AppointmentCreateArgs? request = null;
        planner.Scheduler.AppointmentCreateRequested += (_, args) => request = args;
        await planner.ClickLaneAsync(lane, planner.Point(lane, 4 * day + 9.6 * day / 24, lane.Height / 2));
        Assert.IsNotNull(request);
        Assert.AreEqual(Monday.AddDays(4).AddHours(9.5), request.Start);
    }

    [TestMethod]
    public async Task When_HostResizes_MonthFitsTheViewport()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        scope.Host.Width = 760;
        await scope.LayoutAsync();
        var scroll = Descendants<ScrollViewer>(scope.Scheduler).Single();
        Assert.IsTrue(scroll.ExtentWidth <= scroll.ViewportWidth + 1, $"extent {scroll.ExtentWidth}, viewport {scroll.ViewportWidth}");
        Assert.IsTrue(scope.Frame(scope.Appointment).ActualWidth > 0);
    }

    [TestMethod]
    public async Task When_LanguageChanges_EveryLanguageHasSchedulerTextsAndAttachedControlsUpdate()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        var previousResolver = FloweryLocalization.CustomResolver;
        var assembly = typeof(DaisyScheduler).Assembly;
        using var englishStream = assembly.GetManifestResourceStream("Flowery.WinUI.Localization.en.json")!;
        using var english = JsonDocument.Parse(englishStream);
        var requiredKeys = SchedulerKeys(english);
        Assert.IsNotEmpty(requiredKeys);
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        try
        {
            // App keys must not replace the library's view names or resource headings.
            FloweryLocalization.CustomResolver = static _ => "Host override";
            foreach (var language in FloweryLocalization.SupportedLanguages)
            {
                using var stream = assembly.GetManifestResourceStream($"Flowery.WinUI.Localization.{language}.json")!;
                using var document = JsonDocument.Parse(stream);
                var keys = SchedulerKeys(document);
                CollectionAssert.AreEqual(requiredKeys, keys, language);
                foreach (var key in keys)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(document.RootElement.GetProperty(key).GetString()), language + ": " + key);

                FloweryLocalization.SetCulture(language);
                await scope.LayoutAsync();
                foreach (var button in Descendants<DaisyButton>(scope.Scheduler))
                {
                    if (button.Tag is SchedulerView view)
                        Assert.AreEqual(document.RootElement.GetProperty("Scheduler_" + view).GetString(), button.Content, language);
                    else if (button.Tag is string action)
                    {
                        if (action is "Today" or "Settings")
                            Assert.AreEqual(document.RootElement.GetProperty("Scheduler_" + action).GetString(), button.Content, language);
                        var key = action == "Date" ? "ChooseDate" : action;
                        Assert.AreEqual(document.RootElement.GetProperty("Scheduler_" + key).GetString(), AutomationProperties.GetName(button), language);
                    }
                }
                var resourceHeading = document.RootElement.GetProperty("Scheduler_Resources").GetString();
                Assert.IsTrue(Descendants<TextBlock>(scope.Scheduler).Any(text => text.Text == resourceHeading), language);
                Assert.AreEqual("Install", scope.Appointment.Subject);
                Assert.AreEqual(Monday.AddHours(9), scope.Appointment.Start);
            }
        }
        finally
        {
            FloweryLocalization.CustomResolver = previousResolver;
            FloweryLocalization.SetCulture(previousCulture);
        }

        static string[] SchedulerKeys(JsonDocument document) => document.RootElement.EnumerateObject()
            .Where(static property => property.Name.StartsWith("Scheduler_", StringComparison.Ordinal))
            .Select(static property => property.Name).OrderBy(static key => key, StringComparer.Ordinal).ToArray();
    }

    [TestMethod]
    public void When_ExternalDragDataIsSet_TheTypedItemIsReturnedAndSurvivesACollection()
    {
        var item = new Droppable();
        var package = new DataPackage();
        ExternalDropHandler.SetData(package, item);
        Assert.AreSame(item, ExternalDropHandler.TryGetItem(package.GetView()));
        Assert.IsNull(ExternalDropHandler.TryGetItem(new DataPackage().GetView()));

        // When only the drag package references the item, it survives a collection and the host operation is kept.
        var temporary = new DataPackage { RequestedOperation = DataPackageOperation.Move };
        SetTemporaryItem(temporary);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.AreEqual("Temporary", ExternalDropHandler.TryGetItem(temporary.GetView())?.Subject);
        Assert.AreEqual(DataPackageOperation.Move | DataPackageOperation.Copy, temporary.RequestedOperation);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void SetTemporaryItem(DataPackage package) => ExternalDropHandler.SetData(package, new Droppable("Temporary"));

    private sealed class Droppable(string subject = "External") : ISchedulerDroppable
    {
        public string Subject => subject;
        public TimeSpan DefaultDuration => TimeSpan.FromHours(1);
        public object? Payload => null;
    }

    private static AppointmentContext Context(FrameworkElement frame) => (AppointmentContext)frame.DataContext;

    private static Point Offset(Point point, double x, double y) => new(point.X + x, point.Y + y);

    private static void Invoke(Button button) =>
        ((IInvokeProvider)FrameworkElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    // A heading cell holds its text beside the grip on its right edge.
    private static TextBlock HeadingText(Border heading) => ((Grid)heading.Child).Children.OfType<TextBlock>().Single();

    private sealed partial class SchedulerScope
    {
        private SchedulerScope(SchedulerView view)
        {
            Scheduler = new DaisyScheduler
            {
                CurrentView = view, SelectedDate = new DateTimeOffset(Monday), Size = DaisySize.Medium,
                Appointments = new[] { Appointment },
                SchedulerResources = view is SchedulerView.Planner or SchedulerView.Timeline ? new[] { new ResourceModel { Id = 1, Name = "Alex" } } : Array.Empty<ResourceModel>()
            };
            // A fixed host keeps the geometry independent of the runner window.
            Host = new Grid { Width = 1100, Height = 720, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            Host.Children.Add(Scheduler);
        }

        public AppointmentModel Appointment { get; } = new() { Id = 1, Subject = "Install", Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { 1 } };
        public DaisyScheduler Scheduler { get; }
        public Grid Host { get; }

        public static async Task<SchedulerScope> CreateAsync(SchedulerView view)
        {
            var scope = new SchedulerScope(view);
            RuntimeTestHelpers.AttachToHost(scope.Host);
            await scope.LayoutAsync();
            // The view scrolls to the start of the day once it is loaded, and WinUI applies that scroll later.
            if (view is SchedulerView.Day or SchedulerView.Week or SchedulerView.Planner or SchedulerView.Timeline)
            {
                var scroll = scope.BodyScroll();
                bool Scrolled() => view is SchedulerView.Day or SchedulerView.Week
                    ? scroll.VerticalOffset > 0 || scroll.ScrollableHeight == 0
                    : scroll.HorizontalOffset > 0 || scroll.ScrollableWidth == 0;
                for (var pass = 0; pass < 100 && !Scrolled(); pass++)
                {
                    await Task.Delay(20);
                    await scope.LayoutAsync();
                }
                Assert.IsTrue(Scrolled(), "The view did not scroll to the start of the day.");
            }
            return scope;
        }

        /// <summary>Lets queued renders, scroll restores and layout passes finish.</summary>
        public async Task LayoutAsync()
        {
            for (var pass = 0; pass < 3 || Scheduler.IsRenderPending && pass < 50; pass++)
                await RuntimeTestHelpers.CompleteLayoutAsync(Host);
            Assert.IsFalse(Scheduler.IsRenderPending, "The scheduler render did not run.");
        }

        public IEnumerable<Border> Frames(AppointmentModel appointment) => Descendants<Border>(Scheduler)
            .Where(border => border.IsTabStop && border.DataContext is AppointmentContext context && Equals(context.Appointment.Id, appointment.Id));

        public Border Frame(AppointmentModel appointment) => Frames(appointment).First();

        public ScrollViewer BodyScroll() => Descendants<ScrollViewer>(Scheduler)
            .Single(scroll => scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);

        /// <summary>A point inside <paramref name="element"/>, relative to the scheduler.</summary>
        public Point Point(UIElement element, double x, double y) => element.TransformToVisual(Scheduler).TransformPoint(new Point(x, y));

        public async Task ClickAsync(Border frame, bool rightButton = false, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
        {
            var point = Point(frame, frame.ActualWidth / 2, frame.ActualHeight / 2);
            Scheduler.PressAppointment(frame, new DaisyScheduler.SchedulerPointer(1, point, modifiers, 1, rightButton));
            Scheduler.ReleaseGesture(point, modifiers);
            await LayoutAsync();
        }

        public async Task DragAsync(Border frame, Point origin, Point destination, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
        {
            Scheduler.PressAppointment(frame, new DaisyScheduler.SchedulerPointer(1, origin, modifiers));
            Scheduler.MoveGesture(destination);
            Scheduler.ReleaseGesture(destination, modifiers);
            await LayoutAsync();
        }

        public async Task DragLaneAsync(Canvas lane, Point origin, Point destination)
        {
            Scheduler.PressLane(lane, new DaisyScheduler.SchedulerPointer(1, origin, VirtualKeyModifiers.None));
            Scheduler.MoveGesture(destination);
            Scheduler.ReleaseGesture(destination, VirtualKeyModifiers.None);
            await LayoutAsync();
        }

        public async Task ClickLaneAsync(Canvas lane, Point point)
        {
            Scheduler.PressLane(lane, new DaisyScheduler.SchedulerPointer(1, point, VirtualKeyModifiers.None));
            Scheduler.ReleaseGesture(point, VirtualKeyModifiers.None);
            await LayoutAsync();
        }
    }
}
