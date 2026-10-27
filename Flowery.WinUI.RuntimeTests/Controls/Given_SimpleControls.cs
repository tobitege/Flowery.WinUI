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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_SimpleControls
    {
        [TestMethod]
        public async Task When_ControlsLoadWithoutAuthoredSize_TheyResolveTheGlobalSize()
        {
            // The runner resets the global size to Small (FlowerySizeManager.Reset) while the
            // controls fall back to Medium, so ResolvedSize == CurrentSize proves the global size is taken.
            var cases = new (string Name, Func<FrameworkElement> Create, Func<FrameworkElement, DaisySize> Size, Action<FrameworkElement, string>? Extra)[]
            {
                ("Badge", static () => new DaisyBadge { Content = "New" }, static e => ((DaisyBadge)e).Size, null),
                ("Alert", static () => new DaisyAlert { Message = "Message" }, static e => ((DaisyAlert)e).Size, null),
                ("Divider", static () => new DaisyDivider(), static e => ((DaisyDivider)e).Size, null),
                ("TagPicker", static () => new DaisyTagPicker(), static e => ((DaisyTagPicker)e).Size, null),
                ("Avatar", static () => new DaisyAvatar(), static e => ((DaisyAvatar)e).Size, null),
                ("AvatarGroup", static () => new DaisyAvatarGroup { Initials = "AA,BB" }, static e => ((DaisyAvatarGroup)e).Size, null),
                ("Progress", static () => new DaisyProgress(), static e => ((DaisyProgress)e).Size, null),
                ("Stat", static () => new DaisyStat { Title = "Title", Value = "123" }, static e => ((DaisyStat)e).Size, null),
                ("StatusIndicator", static () => new DaisyStatusIndicator(), static e => ((DaisyStatusIndicator)e).Size, null),
                ("Breadcrumbs", static () => new DaisyBreadcrumbs { Content = new StackPanel { Children = { new DaisyBreadcrumbItem { Content = "Home" } } } },
                    static e => ((DaisyBreadcrumbs)e).Size, null),
                ("Card", static () => new DaisyCard(), static e => ((DaisyCard)e).Size, null),
                ("ChatBubble", static () => new DaisyChatBubble { Content = new TextBlock { Text = "Hello" } }, static e => ((DaisyChatBubble)e).Size, null),
                ("CheckBox", static () => new DaisyCheckBox { Content = "Remember me" }, static e => ((DaisyCheckBox)e).Size, null),
                ("Radio", static () => new DaisyRadio { Content = "Option" }, static e => ((DaisyRadio)e).Size, null),
                ("Toggle", static () => new DaisyToggle(), static e => ((DaisyToggle)e).Size, null),
                ("Steps", static () => new DaisySteps
                    {
                        Content = new StackPanel { Children = { new DaisyStepItem { Content = "Start" }, new DaisyStepItem { Content = "Finish" } } }
                    },
                    static e => ((DaisySteps)e).Size,
                    static (e, name) => Assert.AreEqual(2, ((DaisySteps)e).ItemCount, $"{name}: step items are discovered")),
                ("RadialProgress", static () => new DaisyRadialProgress(), static e => ((DaisyRadialProgress)e).Size, null),
                ("Clock", static () => new DaisyClock(), static e => ((DaisyClock)e).Size, null),
                ("Countdown", static () => new DaisyCountdown(), static e => ((DaisyCountdown)e).Size, null),
                ("Table", static () => new DaisyTable
                    {
                        Content = new StackPanel { Children = { new DaisyTableRow { Content = new DaisyTableCell { Content = "A" } } } }
                    },
                    static e => ((DaisyTable)e).Size, null),
                ("ButtonGroup", static () => new DaisyButtonGroup
                    {
                        Content = new StackPanel { Children = { new DaisyButton { Content = "One" }, new DaisyButton { Content = "Two" } } }
                    },
                    static e => ((DaisyButtonGroup)e).Size, null),
                ("Input", static () => new DaisyInput(), static e => ((DaisyInput)e).Size, null),
                ("List", static () => new DaisyList
                    {
                        Content = new StackPanel { Children = { new DaisyListRow { Content = new TextBlock { Text = "Row" } } } }
                    },
                    static e => ((DaisyList)e).Size,
                    static (e, name) =>
                    {
                        // The list moves its content into its own panel, so find the row in the tree.
                        var row = FindDescendant<DaisyListRow>(e);
                        Assert.IsNotNull(row, $"{name}: row is in the tree");
                        Assert.AreEqual(((DaisyList)e).ResolvedSize, row.ResolvedSize, $"{name}: rows take the list's size");
                    }),
                ("Menu", static () => new DaisyMenu { Content = new StackPanel { Children = { new DaisyMenuItem { Content = "Item" } } } },
                    static e => ((DaisyMenu)e).Size, null),
                ("Dock", static () => new DaisyDock { Content = new StackPanel { Children = { new Border { Width = 10, Height = 10 } } } },
                    static e => ((DaisyDock)e).Size, null),
                ("FileInput", static () => new DaisyFileInput(), static e => ((DaisyFileInput)e).Size, null),
                ("Mockup", static () => new DaisyMockup(), static e => ((DaisyMockup)e).Size, null),
                ("SlideToConfirm", static () => new DaisySlideToConfirm(), static e => ((DaisySlideToConfirm)e).Size, null)
            };

            foreach (var (name, create, size, extra) in cases)
            {
                var control = create();

                RuntimeTestHelpers.AttachToHost(control);
                await RuntimeTestHelpers.EnsureLoadedAsync(control);

                Assert.AreEqual(DaisySize.Auto, size(control), $"{name}: Size defaults to Auto");
                Assert.AreEqual(FlowerySizeManager.CurrentSize, ResolvedSizeOf(control, name), $"{name}: ResolvedSize follows the global size");
                extra?.Invoke(control, name);
            }
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
        public async Task When_Progress_ValueExceedsMaximum_Clamps()
        {
            var progress = new DaisyProgress();

            RuntimeTestHelpers.AttachToHost(progress);
            await RuntimeTestHelpers.EnsureLoadedAsync(progress);

            progress.Value = progress.Maximum + 100;
            Assert.AreEqual(progress.Maximum, progress.Value);
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
            var cases = new (string Name, Func<Control> Create, string InnerPart, Thickness InnerThickness, double? InnerBrushOpacity)[]
            {
                ("Radio", static () => new DaisyRadio { Content = "Option" }, "PART_IndicatorBorder", new Thickness(2), null),
                // The track border uses half of the base content color (DaisyUI).
                ("Toggle", static () => new DaisyToggle { Header = "Enabled" }, "SwitchArea", new Thickness(1), 0.5)
            };

            foreach (var (name, create, innerPart, innerThickness, innerBrushOpacity) in cases)
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

                Assert.IsNotNull(outerBorder, name);
                Assert.IsNotNull(innerBorder, name);
                Assert.IsNotNull(presenter, name);
                Assert.AreSame(userBorderBrush, outerBorder.BorderBrush, name);
                Assert.AreEqual(new Thickness(3), outerBorder.BorderThickness, name);
                Assert.AreNotSame(userBorderBrush, innerBorder.BorderBrush, name);
                Assert.AreEqual(innerThickness, innerBorder.BorderThickness, name);
                if (innerBrushOpacity is { } opacity)
                {
                    Assert.AreEqual(opacity, innerBorder.BorderBrush.Opacity, $"{name}: inner border brush opacity");
                }

                Assert.AreEqual(23, presenter.FontSize, name);
                Assert.AreEqual(FontWeights.Bold.Weight, presenter.FontWeight.Weight, name);
            }
        }

        [TestMethod]
        public async Task When_Breadcrumbs_Load_ItemsTakeTheSeparator()
        {
            var cases = new (string Name, string? Authored, string Expected)[]
            {
                ("Default", null, "/"),
                ("Authored", ">", ">")
            };

            foreach (var (name, authored, expected) in cases)
            {
                var home = new DaisyBreadcrumbItem { Content = "Home" };
                var docs = new DaisyBreadcrumbItem { Content = "Docs" };
                var breadcrumbs = new DaisyBreadcrumbs
                {
                    Content = new StackPanel
                    {
                        Children =
                        {
                            home,
                            docs
                        }
                    }
                };
                if (authored is not null)
                {
                    breadcrumbs.Separator = authored;
                }

                RuntimeTestHelpers.AttachToHost(breadcrumbs);
                await RuntimeTestHelpers.EnsureLoadedAsync(breadcrumbs);

                Assert.AreEqual(expected, breadcrumbs.Separator, name);
                Assert.AreEqual(expected, home.Separator, name);
                Assert.AreEqual(expected, docs.Separator, name);
                Assert.IsTrue(home.IsClickable, name);
                Assert.IsTrue(docs.IsClickable, name);
            }
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

        private static DaisySize ResolvedSizeOf(FrameworkElement control, string name) => control switch
        {
            DaisyBaseContentControl content => content.ResolvedSize,
            DaisyCheckBox checkBox => checkBox.ResolvedSize,
            DaisyRadio radio => radio.ResolvedSize,
            DaisyToggle toggle => toggle.ResolvedSize,
            _ => throw new AssertFailedException($"{name}: no ResolvedSize on {control.GetType().Name}.")
        };

        private static T? FindDescendantByName<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            return FindDescendant<T>(root, element => element.Name == name);
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
