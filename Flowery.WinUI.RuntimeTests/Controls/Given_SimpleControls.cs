using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Localization;
using Flowery.Services;
using Flowery.Theming;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_SimpleControls
    {
        [TestMethod]
        public async Task When_ControlsLoad_AutoTakesTheGlobalSizeAndAnAuthoredSizeWins()
        {
            // The global size is set to Small here, while the controls fall back to Medium, so ResolvedSize == Small
            // proves the global size is taken. An authored Large then proves each control's own GetControlSize wiring.
            var cases = new (string Name, Func<FrameworkElement> Create)[]
            {
                ("Badge", static () => new DaisyBadge { Content = "New" }),
                ("Alert", static () => new DaisyAlert { Message = "Message" }),
                ("Divider", static () => new DaisyDivider()),
                ("TagPicker", static () => new DaisyTagPicker()),
                ("Avatar", static () => new DaisyAvatar()),
                ("AvatarGroup", static () => new DaisyAvatarGroup { Initials = "AA,BB" }),
                ("Progress", static () => new DaisyProgress()),
                ("Stat", static () => new DaisyStat { Title = "Title", Value = "123" }),
                ("StatusIndicator", static () => new DaisyStatusIndicator()),
                ("Breadcrumbs", static () => new DaisyBreadcrumbs { Content = new StackPanel { Children = { new DaisyBreadcrumbItem { Content = "Home" } } } }),
                ("Card", static () => new DaisyCard()),
                ("ChatBubble", static () => new DaisyChatBubble { Content = new TextBlock { Text = "Hello" } }),
                ("CheckBox", static () => new DaisyCheckBox { Content = "Remember me" }),
                ("Radio", static () => new DaisyRadio { Content = "Option" }),
                ("Toggle", static () => new DaisyToggle()),
                ("Steps", static () => CreateSteps()),
                ("RadialProgress", static () => new DaisyRadialProgress()),
                ("Clock", static () => new DaisyClock()),
                ("Countdown", static () => new DaisyCountdown()),
                ("Table", static () => new DaisyTable
                {
                    Content = new StackPanel { Children = { new DaisyTableRow { Content = new DaisyTableCell { Content = "A" } } } }
                }),
                ("ButtonGroup", static () => new DaisyButtonGroup
                {
                    Content = new StackPanel { Children = { new DaisyButton { Content = "One" }, new DaisyButton { Content = "Two" } } }
                }),
                ("Input", static () => new DaisyInput()),
                ("List", static () => CreateList().List),
                ("Menu", static () => new DaisyMenu { Content = new StackPanel { Children = { new DaisyMenuItem { Content = "Item" } } } }),
                ("Dock", static () => new DaisyDock { Content = new StackPanel { Children = { new Border { Width = 10, Height = 10 } } } }),
                ("FileInput", static () => new DaisyFileInput()),
                ("Mockup", static () => new DaisyMockup()),
                ("SlideToConfirm", static () => new DaisySlideToConfirm()),
                ("Tabs", static () => new DaisyTabs
                {
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new DaisyTabItem { Header = "First", Content = new TextBlock { Text = "One" } },
                            new DaisyTabItem { Header = "Second", Content = new TextBlock { Text = "Two" } }
                        }
                    }
                }),
                ("Collapse", static () => new DaisyCollapse { Header = "Header", ExpanderContent = new TextBlock { Text = "Content" } }),
                ("Dropdown", static () => new DaisyDropdown { ItemsSource = new[] { "Alpha", "Beta" } }),
                ("Rating", static () => new DaisyRating()),
                ("Range", static () => new DaisyRange()),
                ("OtpInput", static () => new DaisyOtpInput())
            };

            var previousSize = FlowerySizeManager.CurrentSize;
            var previousDefault = FlowerySizeManager.UseGlobalSizeByDefault;
            try
            {
                FlowerySizeManager.UseGlobalSizeByDefault = true;
                FlowerySizeManager.ApplySize(DaisySize.Small);
                foreach (var (name, create) in cases)
                    await RuntimeTestHelpers.RunCaseAsync(name, () => AssertGlobalAndAuthoredSizeAsync(create));
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                FlowerySizeManager.ApplySize(previousSize);
                FlowerySizeManager.UseGlobalSizeByDefault = previousDefault;
            }
        }

        private static async Task AssertGlobalAndAuthoredSizeAsync(Func<FrameworkElement> create)
        {
            var control = create();
            RuntimeTestHelpers.AttachToHost(control);
            await RuntimeTestHelpers.EnsureLoadedAsync(control);

            var size = control.GetType().GetProperty(nameof(DaisyBadge.Size), typeof(DaisySize))
                ?? throw new AssertFailedException($"No Size property on {control.GetType().Name}.");
            Assert.AreEqual(DaisySize.Auto, (DaisySize)size.GetValue(control)!, "Size defaults to Auto");
            Assert.AreEqual(DaisySize.Small, ResolvedSizeOf(control), "ResolvedSize follows the global size");

            size.SetValue(control, DaisySize.Large);
            Assert.AreEqual(DaisySize.Large, ResolvedSizeOf(control), "An authored Size wins over the global size");
        }

        [TestMethod]
        public void When_BadgeContentIsSet_ItIsRoutedIntoThePresenter()
        {
            var badge = new DaisyBadge { Content = "New" };

            // DaisyBadge.OnContentChanged moves the authored content into its presenter and keeps its own border.
            Assert.AreEqual("New", badge.BadgeContent, "BadgeContent reads the presenter content.");
            var border = badge.Content as Border;
            Assert.IsNotNull(border, "Content stays the badge's own border.");
            Assert.AreEqual("New", (border.Child as ContentPresenter)?.Content, "The border's presenter shows the authored content.");
        }

        [TestMethod]
        public async Task When_StepsLoad_TheirItemsAreDiscovered()
        {
            var steps = CreateSteps();

            RuntimeTestHelpers.AttachToHost(steps);
            await RuntimeTestHelpers.EnsureLoadedAsync(steps);

            Assert.AreEqual(2, steps.ItemCount);
        }

        [TestMethod]
        public async Task When_ListSizeChanges_ItsRowsTakeIt()
        {
            var (list, row) = CreateList();

            RuntimeTestHelpers.AttachToHost(list);
            await RuntimeTestHelpers.EnsureLoadedAsync(list);

            Assert.AreEqual(list.ResolvedSize, row.ResolvedSize, "An automatic list passes its resolved size on.");
            var other = list.ResolvedSize == DaisySize.Large ? DaisySize.ExtraSmall : DaisySize.Large;
            list.Size = other;
            Assert.AreEqual(other, row.ResolvedSize, "An authored list size overrides the rows' size (DaisyList.ChildSizeOverride).");
        }

        private static DaisySteps CreateSteps() => new()
        {
            Content = new StackPanel { Children = { new DaisyStepItem { Content = "Start" }, new DaisyStepItem { Content = "Finish" } } }
        };

        private static (DaisyList List, DaisyListRow Row) CreateList()
        {
            var row = new DaisyListRow { Content = new TextBlock { Text = "Row" } };
            return (new DaisyList { Content = new StackPanel { Children = { row } } }, row);
        }

        [TestMethod]
        public async Task When_TagPicker_TogglesSelection_UpdatesSelectedList()
        {
            var tags = new List<string> { "Alpha", "Beta" };
            var selected = new List<string>();
            var picker = new DaisyTagPicker
            {
                Tags = tags,
                SelectedTags = selected
            };

            RuntimeTestHelpers.AttachToHost(picker);
            await RuntimeTestHelpers.EnsureLoadedAsync(picker);

            picker.ToggleTag("Alpha");
            Assert.HasCount(1, selected);
            Assert.AreEqual("Alpha", selected[0]);

            picker.ToggleTag("Alpha");
            Assert.IsEmpty(selected);
        }

        [TestMethod]
        public async Task When_TagPicker_HasPublicStyling_OuterBorderAndTextUseIt()
        {
            var userBorderBrush = new SolidColorBrush(Colors.Red);
            var userForeground = new SolidColorBrush(Colors.Blue);
            var picker = new DaisyTagPicker
            {
                Tags = new List<string> { "Alpha", "Beta" },
                BorderBrush = userBorderBrush,
                BorderThickness = new Thickness(3),
                Foreground = userForeground,
                FontSize = 23,
                FontWeight = FontWeights.Bold
            };

            RuntimeTestHelpers.AttachToHost(picker);
            await RuntimeTestHelpers.EnsureLoadedAsync(picker);

            Assert.IsInstanceOfType(picker.Content, typeof(Border));
            var outerBorder = (Border)picker.Content;
            Assert.AreSame(userBorderBrush, outerBorder.BorderBrush);
            Assert.AreEqual(new Thickness(3), outerBorder.BorderThickness);

            Assert.IsInstanceOfType(outerBorder.Child, typeof(Border));
            var containerBorder = (Border)outerBorder.Child;
            Assert.AreNotSame(userBorderBrush, containerBorder.BorderBrush);
            Assert.AreEqual(new Thickness(1), containerBorder.BorderThickness);

            // The title sits inside a ScrollViewer; on Skia its content is only
            // realized after a layout pass, so retry until it shows up.
            TextBlock? title = null;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                picker.UpdateLayout();
                title = FindDescendant<TextBlock>(picker);
                if (title != null)
                {
                    break;
                }

                await Task.Delay(50);
            }

            Assert.IsNotNull(title);
            Assert.AreEqual(FloweryLocalization.GetStringInternal("TagPicker_SelectedTags", "Selected Tags"), title.Text);
            Assert.AreSame(userForeground, title.Foreground);
            Assert.AreEqual(23, title.FontSize);
            Assert.AreEqual(FontWeights.Bold.Weight, title.FontWeight.Weight);
        }

        [TestMethod]
        public async Task When_AvatarGroup_InitialsOverflow_CalculatesCount()
        {
            var group = new DaisyAvatarGroup
            {
                Initials = "AA,BB,CC",
                MaxVisible = 2
            };

            RuntimeTestHelpers.AttachToHost(group);
            await RuntimeTestHelpers.EnsureLoadedAsync(group);

            Assert.AreEqual(2, group.OverflowCount);
        }

        [TestMethod]
        public async Task When_CheckBox_HasPublicStyling_OuterBorderAndLabelUseIt()
        {
            var userBorderBrush = new SolidColorBrush(Colors.Red);
            var checkBox = new DaisyCheckBox
            {
                Content = "Remember me",
                BorderBrush = userBorderBrush,
                BorderThickness = new Thickness(3),
                FontSize = 23,
                FontWeight = FontWeights.Bold
            };

            RuntimeTestHelpers.AttachToHost(checkBox);
            await RuntimeTestHelpers.EnsureLoadedAsync(checkBox);

            Assert.IsInstanceOfType(checkBox.Content, typeof(Border));
            var outerBorder = (Border)checkBox.Content;
            Assert.AreSame(userBorderBrush, outerBorder.BorderBrush);
            Assert.AreEqual(new Thickness(3), outerBorder.BorderThickness);

            Assert.IsInstanceOfType(outerBorder.Child, typeof(StackPanel));
            var root = (StackPanel)outerBorder.Child;
            Assert.IsInstanceOfType(root.Children[0], typeof(Border));
            var indicatorBorder = (Border)root.Children[0];
            Assert.AreNotSame(userBorderBrush, indicatorBorder.BorderBrush);
            Assert.AreEqual(new Thickness(2), indicatorBorder.BorderThickness);

            Assert.IsInstanceOfType(root.Children[1], typeof(TextBlock));
            var label = (TextBlock)root.Children[1];
            Assert.AreEqual(23, label.FontSize);
            Assert.AreEqual(FontWeights.Bold.Weight, label.FontWeight.Weight);
        }

        [TestMethod]
        public async Task When_SelectionControlsHavePublicStyling_OuterBorderAndContentUseIt()
        {
            var cases = new (string Name, Func<Control> Create, string InnerPart, Thickness InnerThickness)[]
            {
                ("Radio", static () => new DaisyRadio { Content = "Option" }, "PART_IndicatorBorder", new Thickness(2)),
                ("Toggle", static () => new DaisyToggle { Header = "Enabled" }, "SwitchArea", new Thickness(1))
            };

            foreach (var (name, create, innerPart, innerThickness) in cases)
                await RuntimeTestHelpers.RunCaseAsync(name, () => AssertPublicStylingAsync(create, innerPart, innerThickness));
        }

        private static async Task AssertPublicStylingAsync(Func<Control> create, string innerPart, Thickness innerThickness)
        {
            var userBorderBrush = new SolidColorBrush(Colors.Red);
            var control = create();
            control.BorderBrush = userBorderBrush;
            control.BorderThickness = new Thickness(3);
            control.FontSize = 23;
            control.FontWeight = FontWeights.Bold;

            RuntimeTestHelpers.AttachToHost(control);
            await RuntimeTestHelpers.EnsureLoadedAsync(control);
            control.ApplyTemplate();

            var outerBorder = FindDescendantByName<Border>(control, "PART_ControlBorder");
            var innerBorder = FindDescendantByName<Border>(control, innerPart);
            var presenter = FindDescendant<ContentPresenter>(control);

            Assert.IsNotNull(outerBorder, "Outer border");
            Assert.IsNotNull(innerBorder, innerPart);
            Assert.IsNotNull(presenter, "Content presenter");
            Assert.AreSame(userBorderBrush, outerBorder.BorderBrush, "Outer border brush");
            Assert.AreEqual(new Thickness(3), outerBorder.BorderThickness, "Outer border thickness");
            Assert.AreNotSame(userBorderBrush, innerBorder.BorderBrush, "Inner border brush");
            Assert.AreEqual(innerThickness, innerBorder.BorderThickness, "Inner border thickness");
            Assert.AreEqual(23, presenter.FontSize, "Content font size");
            Assert.AreEqual(FontWeights.Bold.Weight, presenter.FontWeight.Weight, "Content font weight");
        }

        [TestMethod]
        public async Task When_ToggleLoads_TheTrackBorderUsesHalfTheBaseContentColor()
        {
            var toggle = new DaisyToggle { Header = "Enabled" };

            RuntimeTestHelpers.AttachToHost(toggle);
            await RuntimeTestHelpers.EnsureLoadedAsync(toggle);
            toggle.ApplyTemplate();

            var switchArea = FindDescendantByName<Border>(toggle, "SwitchArea");
            Assert.IsNotNull(switchArea);
            Assert.AreEqual(0.5, switchArea.BorderBrush.Opacity, "Track border uses half of the base content color (DaisyUI)");
        }

        [TestMethod]
        public async Task When_Breadcrumbs_Assigns_Item_Separator()
        {
            // ">" differs from the items' own default "/", so the items can only show it if the breadcrumbs pass it on.
            var home = new DaisyBreadcrumbItem { Content = "Home" };
            var docs = new DaisyBreadcrumbItem { Content = "Docs" };
            var breadcrumbs = new DaisyBreadcrumbs
            {
                Separator = ">",
                Content = new StackPanel
                {
                    Children =
                    {
                        home,
                        docs
                    }
                }
            };

            RuntimeTestHelpers.AttachToHost(breadcrumbs);
            await RuntimeTestHelpers.EnsureLoadedAsync(breadcrumbs);

            Assert.AreEqual(">", home.Separator, "First item separator");
            Assert.AreEqual(">", docs.Separator, "Second item separator");
            Assert.IsTrue(home.IsClickable, "First item is clickable");
            Assert.IsTrue(docs.IsClickable, "Second item is clickable");
        }

        [TestMethod]
        public void When_MenuFlyoutItem_LocalizationSource_UpdatesText()
        {
            var item = new DaisyMenuFlyoutItem
            {
                LocalizationKey = "KeyA",
                LocalizationSource = new Dictionary<string, string>
                {
                    ["KeyA"] = "Alpha"
                }
            };

            Assert.AreEqual("Alpha", item.Text);
        }

        [TestMethod]
        public async Task When_Stack_Next_WrapsToTheFirstItem()
        {
            var stack = new DaisyStack
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new Border { Width = 10, Height = 10 },
                        new Border { Width = 10, Height = 10 }
                    }
                }
            };

            RuntimeTestHelpers.AttachToHost(stack);
            await RuntimeTestHelpers.EnsureLoadedAsync(stack);

            Assert.AreEqual(2, stack.ItemCount);
            Assert.AreEqual(0, stack.SelectedIndex);

            stack.Next();
            Assert.AreEqual(1, stack.SelectedIndex);
            stack.Next();
            Assert.AreEqual(0, stack.SelectedIndex);
        }

        [TestMethod]
        public async Task When_DateTimeline_Loads_ItemSizesComeFromTokens()
        {
            var timeline = new DaisyDateTimeline();

            // Opt out of the global size so the Medium tokens apply.
            FlowerySizeManager.SetIgnoreGlobalSize(timeline, true);

            RuntimeTestHelpers.AttachToHost(timeline);
            await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

            // ItemWidth is resolved from the DaisyDateTimelineMediumItemWidth token (64)
            // during ApplySizing unless explicitly set.
            Assert.AreEqual(64.0, timeline.ItemWidth);
            // ItemSpacing is resolved from the DaisyDateTimelineItemSpacing token (6)
            // during ApplySizing unless explicitly set.
            Assert.AreEqual(6.0, timeline.ItemSpacing);
        }

        [TestMethod]
        public async Task When_DateTimelineSelectionChanges_HeaderTextFollowsIt()
        {
            var timeline = new DaisyDateTimeline();

            RuntimeTestHelpers.AttachToHost(timeline);
            await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

            // The header shows the month of the selected date, or of today without a selection, in the control's Locale
            // (DaisyDateTimeline.UpdateHeaderText).
            var selected = DateTime.Today.AddMonths(2);
            timeline.SelectedDate = selected;
            Assert.AreEqual(selected.ToString("MMMM yyyy", timeline.Locale), timeline.HeaderText, "HeaderText follows the selected date");

            timeline.SelectedDate = null;
            Assert.AreEqual(DateTime.Today.ToString("MMMM yyyy", timeline.Locale), timeline.HeaderText, "HeaderText falls back to today without a selection");
        }

        [TestMethod]
        public async Task When_DateTimeline_PropertiesSetBeforeLoad_LoadAppliesThem()
        {
            // The timeline builds its items once on Loaded instead of for each property set from XAML.
            var first = DateTime.Today.AddDays(-3);
            var timeline = new DaisyDateTimeline
            {
                FirstDate = first,
                LastDate = first.AddDays(9),
                HeaderType = DateTimelineHeaderType.None,
                ItemWidth = 80
            };
            FlowerySizeManager.SetIgnoreGlobalSize(timeline, true);

            RuntimeTestHelpers.AttachToHost(timeline);
            await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

            var items = RuntimeTestHelpers.Descendants<DaisyDateTimelineItem>(timeline).ToList();
            CollectionAssert.AreEqual(
                Enumerable.Range(0, 10).Select(day => first.AddDays(day)).ToList(),
                items.Select(item => item.Date).ToList());
            Assert.IsTrue(items.All(item => item.Width == 80), "The explicit item width must reach every item.");
            Assert.AreEqual(80.0, timeline.ItemWidth);
            var header = RuntimeTestHelpers.Descendants<TextBlock>(timeline).Single(text => text.Text == timeline.HeaderText);
            Assert.AreEqual(Visibility.Collapsed, ((UIElement)VisualTreeHelper.GetParent(header)).Visibility,
                "HeaderType None must hide the header.");
        }

        [TestMethod]
        public async Task When_DateTimeline_Loads_BuildsItemsOnce()
        {
            var timeline = new DaisyDateTimeline
            {
                FirstDate = DateTime.Today.AddDays(-3),
                LastDate = DateTime.Today.AddDays(6),
                HeaderType = DateTimelineHeaderType.None,
                Size = DaisySize.Large
            };
            FlowerySizeManager.SetIgnoreGlobalSize(timeline, true);
            Assert.AreEqual(0, timeline.ItemBuildCount, "Properties set before loading must not build the items.");

            RuntimeTestHelpers.AttachToHost(timeline);
            await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

            Assert.AreEqual(1, timeline.ItemBuildCount, "Loading must build the items once.");
        }

        [TestMethod]
        public async Task When_DateTimeline_ThemeChanges_ColorsFollowWithoutRebuild()
        {
            var previous = DaisyThemeManager.CurrentThemeName;
            try
            {
                Assert.IsTrue(DaisyThemeManager.ApplyTheme("Light"));
                var timeline = new DaisyDateTimeline
                {
                    FirstDate = DateTime.Today.AddDays(-3),
                    LastDate = DateTime.Today.AddDays(6),
                    SelectedDate = DateTime.Today.AddDays(1)
                };
                RuntimeTestHelpers.AttachToHost(timeline);
                await RuntimeTestHelpers.CompleteLayoutAsync(timeline);

                var header = RuntimeTestHelpers.Descendants<TextBlock>(timeline).Single(text => text.Text == timeline.HeaderText);
                var item = RuntimeTestHelpers.Descendants<DaisyDateTimelineItem>(timeline).First(item => !item.IsSelected);
                // Each item builds its background in its own Loaded handler, which WinUI may raise after the timeline's layout.
                await RuntimeTestHelpers.WaitUntilAsync(() => item.Content is Border { Background: SolidColorBrush },
                    "the date item to build its background");
                var itemBackground = (Border)item.Content;
                // Theme changes mutate the shared brushes, so capture the colors before switching.
                var lightHeader = ((SolidColorBrush)header.Foreground).Color;
                var lightItem = ((SolidColorBrush)itemBackground.Background).Color;
                var builds = timeline.ItemBuildCount;

                Assert.IsTrue(DaisyThemeManager.ApplyTheme("Dark"));
                await RuntimeTestHelpers.CompleteLayoutAsync(timeline);

                Assert.AreEqual(builds, timeline.ItemBuildCount, "A theme change must not rebuild the items.");
                Assert.IsTrue(RuntimeTestHelpers.Descendants<DaisyDateTimelineItem>(timeline).Contains(item), "The items must stay.");
                Assert.AreNotEqual(lightHeader, ((SolidColorBrush)header.Foreground).Color, "The header color must follow the theme.");
                Assert.AreNotEqual(lightItem, ((SolidColorBrush)itemBackground.Background).Color, "The item color must follow the theme.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                DaisyThemeManager.ApplyTheme(previous ?? "Light");
            }
        }

        [TestMethod]
        public async Task When_DateTimeline_GlobalSizeChanges_ItemsTakeTheNewSize()
        {
            var previousSize = FlowerySizeManager.CurrentSize;
            var previousDefault = FlowerySizeManager.UseGlobalSizeByDefault;
            try
            {
                FlowerySizeManager.UseGlobalSizeByDefault = true;
                FlowerySizeManager.ApplySize(DaisySize.Small);
                var timeline = new DaisyDateTimeline
                {
                    FirstDate = DateTime.Today.AddDays(-3),
                    LastDate = DateTime.Today.AddDays(6)
                };
                RuntimeTestHelpers.AttachToHost(timeline);
                await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

                Assert.IsTrue(RuntimeTestHelpers.Descendants<DaisyDateTimelineItem>(timeline).All(item => item.Size == DaisySize.Small));
                var smallWidth = timeline.ItemWidth;
                var builds = timeline.ItemBuildCount;

                FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);

                Assert.AreEqual(builds + 1, timeline.ItemBuildCount, "A new resolved size must rebuild the items once.");
                Assert.AreNotEqual(smallWidth, timeline.ItemWidth, "ExtraLarge must resolve another item width than Small.");
                var items = RuntimeTestHelpers.Descendants<DaisyDateTimelineItem>(timeline).ToList();
                Assert.HasCount(10, items);
                Assert.IsTrue(items.All(item => item.Size == DaisySize.ExtraLarge), "Every item must take the new size.");
                Assert.IsTrue(items.All(item => item.Width == timeline.ItemWidth), "Every item must take the new width.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                FlowerySizeManager.ApplySize(previousSize);
                FlowerySizeManager.UseGlobalSizeByDefault = previousDefault;
            }
        }

        [TestMethod]
        public async Task When_DateTimeline_ResolvesItemWidth_ItemsUseTheResolvedWidth()
        {
            // ApplySizing resolves the width while the items are built; the items must not keep the default width.
            var timeline = new DaisyDateTimeline { Size = DaisySize.ExtraLarge };
            FlowerySizeManager.SetIgnoreGlobalSize(timeline, true);

            RuntimeTestHelpers.AttachToHost(timeline);
            await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

            Assert.AreNotEqual(64.0, timeline.ItemWidth, "ExtraLarge must resolve a width other than the default.");
            var items = RuntimeTestHelpers.Descendants<DaisyDateTimelineItem>(timeline).ToList();
            Assert.IsNotEmpty(items);
            Assert.IsTrue(items.All(item => item.Width == timeline.ItemWidth), "Every item must use the resolved width.");
        }

        [TestMethod]
        public async Task When_DateTimeline_LocaleChanges_HeaderFollows()
        {
            var date = new DateTime(2026, 3, 15);
            var locale = CultureInfo.GetCultureInfo("fr-FR");
            var frenchHeader = date.ToString("MMMM yyyy", locale);
            Assert.AreNotEqual(date.ToString("MMMM yyyy", CultureInfo.CurrentCulture), frenchHeader,
                "The test needs a locale that formats differently from the current culture.");

            // Set before load, as from XAML: the header text is public and must not wait for Loaded.
            var timeline = new DaisyDateTimeline { SelectedDate = date, Locale = locale };
            Assert.AreEqual(frenchHeader, timeline.HeaderText);

            RuntimeTestHelpers.AttachToHost(timeline);
            await RuntimeTestHelpers.EnsureLoadedAsync(timeline);

            Assert.IsTrue(RuntimeTestHelpers.Descendants<TextBlock>(timeline).Any(text => text.Text == frenchHeader));
            Assert.AreEqual(frenchHeader, AutomationProperties.GetName(timeline));

            var english = CultureInfo.GetCultureInfo("en-US");
            timeline.Locale = english;
            Assert.AreEqual(date.ToString("MMMM yyyy", english), timeline.HeaderText);
        }

        [TestMethod]
        public async Task When_IgnoreGlobalSizeChanges_OnlyThatSubtreeRefreshes()
        {
            var inside = new Border();
            var outside = new Border();
            var scope = new StackPanel { Children = { inside } };
            var host = new StackPanel { Children = { scope, outside } };
            var refreshes = new int[2];
            FlowerySizeManager.Register(inside, () => refreshes[0]++, static () => DaisySize.Medium);
            FlowerySizeManager.Register(outside, () => refreshes[1]++, static () => DaisySize.Medium);
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.CompleteLayoutAsync(host, inside, outside);
                var (insideBefore, outsideBefore) = (refreshes[0], refreshes[1]);

                FlowerySizeManager.SetIgnoreGlobalSize(scope, true);
                Assert.IsGreaterThan(insideBefore, refreshes[0], "A control below the changed element must refresh.");
                Assert.AreEqual(outsideBefore, refreshes[1], "A control outside the changed element must not refresh.");

                // A page built from XAML sets the property before it is in the tree: no loaded control depends on it.
                insideBefore = refreshes[0];
                FlowerySizeManager.SetIgnoreGlobalSize(new StackPanel(), true);
                Assert.AreEqual(insideBefore, refreshes[0]);
                Assert.AreEqual(outsideBefore, refreshes[1]);

                FlowerySizeManager.SetIgnoreGlobalSize(inside, false);
                Assert.IsGreaterThan(insideBefore, refreshes[0], "Opting back in must refresh the control itself.");
                Assert.AreEqual(outsideBefore, refreshes[1]);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        [TestMethod]
        public async Task When_ProductThemeDropdown_Loads_MinWidthFollowsSwatchSize()
        {
            var dropdown = new DaisyProductThemeDropdown();

            RuntimeTestHelpers.AttachToHost(dropdown);
            await RuntimeTestHelpers.CompleteLayoutAsync(dropdown);

            // ApplySizing recomputes MinWidth from the swatch size on load; the
            // constructor's 200 only applies before the control is loaded.
            var expectedMinWidth = (DaisyResourceLookup.GetDefaultSwatchSize(dropdown.ResolvedSize) * 5) + 4 + 8 + 60 + 26;
            Assert.AreEqual(expectedMinWidth, dropdown.MinWidth);
        }

        private static DaisySize ResolvedSizeOf(FrameworkElement control) => control switch
        {
            DaisyBaseContentControl content => content.ResolvedSize,
            DaisyCheckBox checkBox => checkBox.ResolvedSize,
            DaisyRadio radio => radio.ResolvedSize,
            DaisyToggle toggle => toggle.ResolvedSize,
            _ => throw new AssertFailedException($"No ResolvedSize on {control.GetType().Name}.")
        };

        private static T? FindDescendantByName<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            return FindDescendant<T>(root, element => element.Name == name);
        }

        [TestMethod]
        public async Task When_AWindowMockupWithAnAppIconLoads_TheIconMovesIntoTheRebuiltHeader()
        {
            // OnLoaded builds the tree, and OnThemeChanged rebuilds it at once, before layout: the icon still belongs to
            // the first header then, which VisualTreeHelper does not report yet.
            var icon = new DaisyIconText { IconSize = 24, IconData = "M12,3L2,12H5V20H19V12H22L12,3Z" };
            var mockup = new DaisyMockup
            {
                Variant = DaisyMockupVariant.Window,
                ChromeStyle = DaisyChromeStyle.Windows,
                Width = 300,
                Height = 160,
                AppIcon = icon
            };
            RuntimeTestHelpers.AttachToHost(mockup);
            try
            {
                await RuntimeTestHelpers.EnsureLoadedAsync(mockup);
                mockup.ChromeStyle = DaisyChromeStyle.Mac;
                mockup.ChromeStyle = DaisyChromeStyle.Windows;
                mockup.UpdateLayout();

                DependencyObject? ancestor = icon;
                while (ancestor is not null && !ReferenceEquals(ancestor, mockup))
                    ancestor = VisualTreeHelper.GetParent(ancestor);
                Assert.AreSame(mockup, ancestor, "The app icon must sit in the header of the mockup's current tree.");
            }
            finally
            {
                await RuntimeTestHelpers.DetachFromHostAsync(mockup);
            }
        }

        private static T? FindDescendant<T>(DependencyObject root)
            where T : FrameworkElement
        {
            return FindDescendant<T>(root, _ => true);
        }

        private static T? FindDescendant<T>(DependencyObject root, Func<T, bool> predicate)
            where T : FrameworkElement
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T element && predicate(element))
                {
                    return element;
                }

                var descendant = FindDescendant<T>(child, predicate);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}
