using System;
using System.Collections.ObjectModel;
using System.Linq;
using Flowery.Controls;
using Flowery.Localization;

namespace Flowery.WinUI.Gallery
{
    /// <summary>
    /// Provides Gallery-specific sidebar categories and languages.
    /// This data is specific to the Flowery.WinUI Gallery showcase app.
    /// </summary>
    public static class GallerySidebarData
    {
        private static readonly Lazy<Dictionary<string, SidebarItem>> _itemsById = new(() =>
            CreateCategories()
                .SelectMany(category => category.Items)
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// Finds the sidebar item with the given Id (e.g. a demo page's SectionId), or null if there is none.
        /// </summary>
        public static SidebarItem? FindItem(string? id)
        {
            return !string.IsNullOrEmpty(id) && _itemsById.Value.TryGetValue(id, out var item) ? item : null;
        }

        /// <summary>
        /// Creates the default categories for the Gallery showcase sidebar.
        /// </summary>
        public static ObservableCollection<SidebarCategory> CreateCategories()
        {
            var categories = new ObservableCollection<SidebarCategory>
            {
                // Home stays at top
                new()
                {
                    Name = "Sidebar_Home",
                    IconKey = "DaisyIconHome",
                    Items = new()
                    {
                        new() { Id = "welcome", Name = "Sidebar_Welcome", TabHeader = "Sidebar_Home" },
                        new GalleryThemeSelectorItem { Id = "theme", Name = "Sidebar_Theme", TabHeader = "Sidebar_Home" },
                        new GalleryLanguageSelectorItem { Id = "language", Name = "Sidebar_Language", TabHeader = "Sidebar_Home" },
                        new GallerySizeSelectorItem { Id = "size", Name = "Sidebar_Size", TabHeader = "Sidebar_Home" },
                        new GalleryBaseColorSelectorItem { Id = "base-color", Name = "Sidebar_BaseColor", TabHeader = "Sidebar_Home" }
                    }
                },
                // Alphabetically sorted categories
                new()
                {
                    Name = "Sidebar_Actions",
                    IconKey = "DaisyIconActions",
                    Items = new()
                    {
                        new() { Id = "button", Name = "Sidebar_Button", ControlName = "DaisyButton", TabHeader = "Sidebar_Actions" },
                        new() { Id = "button-group", Name = "Sidebar_ButtonGroup", ControlName = "DaisyButtonGroup", TabHeader = "Sidebar_Actions" },
                        new() { Id = "figma-comment", Name = "Sidebar_FigmaComment", TabHeader = "Sidebar_Actions" },
                        new() { Id = "copybutton", Name = "Sidebar_CopyButton", ControlName = "DaisyCopyButton", TabHeader = "Sidebar_Actions" },
                        new() { Id = "dropdown", Name = "Sidebar_Dropdown", TabHeader = "Sidebar_Actions" },
                        new() { Id = "dropdownmenu", Name = "Sidebar_DropdownMenu", ControlName = "DaisyDropdown", TabHeader = "Sidebar_Actions" },
                        new() { Id = "fab", Name = "Sidebar_FAB", ControlName = "DaisyFab", TabHeader = "Sidebar_Actions" },
                        new() { Id = "popover", Name = "Sidebar_Popover", ControlName = "DaisyPopover", TabHeader = "Sidebar_Actions" },
                        new() { Id = "modal", Name = "Sidebar_Modal", ControlName = "DaisyModal", TabHeader = "Sidebar_Actions" },
                        new() { Id = "modal-radii", Name = "Sidebar_ModalRadii", TabHeader = "Sidebar_Actions" },
                        new() { Id = "swap", Name = "Sidebar_Swap", ControlName = "DaisySwap", TabHeader = "Sidebar_Actions" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Cards",
                    IconKey = "DaisyIconCard",
                    Items = new()
                    {
                        new() { Id = "card", Name = "Sidebar_Card", ControlName = "DaisyCard", TabHeader = "Sidebar_Cards" },
                        new() { Id = "card-stack", Name = "Sidebar_CardStack", TabHeader = "Sidebar_Cards" },
                        new() { Id = "expandable-cards", Name = "Showcase_ExpandableCards_Title", ControlName = "DaisyExpandableCard", TabHeader = "Sidebar_Cards" },
                        new() { Id = "patterned-card", Name = "Sidebar_PatternedCard", ControlName = "DaisyPatternedCard", TabHeader = "Sidebar_Cards" },
                        new() { Id = "glass-card", Name = "Sidebar_GlassCard", ControlName = "DaisyCard", TabHeader = "Sidebar_Cards" },
                        new() { Id = "daisyglass-simulated", Name = "Sidebar_DaisyGlassSimulated", ControlName = "DaisyGlass", TabHeader = "Sidebar_Cards" },
                        new() { Id = "daisyglass-bitmap-capture", Name = "Sidebar_DaisyGlassBitmapCapture", ControlName = "DaisyGlass", TabHeader = "Sidebar_Cards" },
                        new() { Id = "daisyglass-full-width", Name = "Sidebar_DaisyGlassFullWidth", ControlName = "DaisyGlass", TabHeader = "Sidebar_Cards" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Carousel",
                    IconKey = "DaisyIconRepost",
                    Items = new()
                    {
                        new() { Id = "carousel", Name = "Sidebar_Carousel", ControlName = "DaisyCarousel", TabHeader = "Sidebar_Carousel" }
                    }
                },
                new()
                {
                    Name = "Sidebar_CarouselGL",
                    IconKey = "DaisyIconRepost",
                    Items = new() { new() { Id = "carousel-gl", Name = "Sidebar_CarouselGL", TabHeader = "Sidebar_CarouselGL" } }
                },
                new()
                {
                    Name = "Sidebar_CarouselGLTransitions",
                    IconKey = "DaisyIconRepost",
                    Items = new() { new() { Id = "carousel-gl-transitions", Name = "Sidebar_CarouselGLTransitions", TabHeader = "Sidebar_CarouselGLTransitions" } }
                },
                new()
                {
                    Name = "Sidebar_DataDisplay",
                    IconKey = "DaisyIconDataDisplay",
                    Items = new()
                    {
                        new() { Id = "accordion", Name = "Sidebar_Accordion", ControlName = "DaisyAccordion", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "animatednumber", Name = "Sidebar_AnimatedNumber", ControlName = "DaisyAnimatedNumber", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "avatar", Name = "Sidebar_Avatar", ControlName = "DaisyAvatar", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "badge", Name = "Sidebar_Badge", ControlName = "DaisyBadge", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "chat-bubble", Name = "Sidebar_ChatBubble", ControlName = "DaisyChatBubble", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "collapse", Name = "Sidebar_Collapse", ControlName = "DaisyCollapse", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "contributiongraph", Name = "Sidebar_ContributionGraph", ControlName = "DaisyContributionGraph", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "clock", Name = "Sidebar_Clock", ControlName = "DaisyClock", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "countdown", Name = "Sidebar_Countdown", ControlName = "DaisyCountdown", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "diff", Name = "Sidebar_Diff", ControlName = "DaisyDiff", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "hover-gallery", Name = "Sidebar_HoverGallery", ControlName = "DaisyHoverGallery", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "kbd", Name = "Sidebar_Kbd", ControlName = "DaisyKbd", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "list", Name = "Sidebar_List", ControlName = "DaisyList", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "numberflow", Name = "Sidebar_NumberFlow", ControlName = "DaisyNumberFlow", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "stat", Name = "Sidebar_Stat", ControlName = "DaisyStat", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "table", Name = "Sidebar_Table", ControlName = "DaisyTable", TabHeader = "Sidebar_DataDisplay" },
                        new() { Id = "text-rotate", Name = "Sidebar_TextRotate", ControlName = "DaisyTextRotate", TabHeader = "Sidebar_DataDisplay" }
                    }
                },
                new()
                {
                    Name = "Sidebar_DateDisplay",
                    IconKey = "DaisyIconDateDisplay",
                    Items = new()
                    {
                        new() { Id = "date-timeline", Name = "Sidebar_DateTimeline", ControlName = "DaisyDateTimeline", TabHeader = "Sidebar_DateDisplay" },
                        new() { Id = "timeline", Name = "Sidebar_Timeline", ControlName = "DaisyTimeline", TabHeader = "Sidebar_DateDisplay" }
                    }
                },
                new()
                {
                    Name = "Sidebar_DataInput",
                    IconKey = "DaisyIconDataInput",
                    Items = new()
                    {
                        new() { Id = "checkbox", Name = "Sidebar_Checkbox", ControlName = "DaisyCheckBox", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "file-input", Name = "Sidebar_FileInput", ControlName = "DaisyFileInput", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "input", Name = "Sidebar_Input", ControlName = "DaisyInput", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "calendar-date-picker", Name = "Sidebar_CalendarDatePicker", ControlName = "DaisyCalendarDatePicker", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "mask-input", Name = "Sidebar_MaskInput", ControlName = "DaisyMaskInput", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "numericupdown", Name = "Sidebar_NumericUpDown", ControlName = "DaisyNumericUpDown", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "otpinput", Name = "Sidebar_OtpInput", ControlName = "DaisyOtpInput", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "passwordbox", Name = "Sidebar_PasswordBox", ControlName = "DaisyPasswordBox", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "radio", Name = "Sidebar_Radio", ControlName = "DaisyRadio", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "range", Name = "Sidebar_Range", ControlName = "DaisyRange", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "rating", Name = "Sidebar_Rating", ControlName = "DaisyRating", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "select", Name = "Sidebar_Select", ControlName = "DaisySelect", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "slide-to-confirm", Name = "Sidebar_SlideToConfirm", ControlName = "DaisySlideToConfirm", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "tagpicker", Name = "Sidebar_TagPicker", ControlName = "DaisyTagPicker", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "textarea", Name = "Sidebar_TextArea", ControlName = "DaisyTextArea", TabHeader = "Sidebar_DataInput" },
                        new() { Id = "toggle", Name = "Sidebar_Toggle", ControlName = "DaisyToggle", TabHeader = "Sidebar_DataInput" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Divider",
                    IconKey = "DaisyIconDivider",
                    Items = new()
                    {
                        new() { Id = "divider", Name = "Sidebar_DividerItem", ControlName = "DaisyDivider", TabHeader = "Sidebar_Divider" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Feedback",
                    IconKey = "DaisyIconFeedback",
                    Items = new()
                    {
                        new() { Id = "alert", Name = "Sidebar_Alert", ControlName = "DaisyAlert", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "loading", Name = "Sidebar_Loading", ControlName = "DaisyLoading", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "progress", Name = "Sidebar_Progress", ControlName = "DaisyProgress", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "radial-progress", Name = "Sidebar_RadialProgress", ControlName = "DaisyRadialProgress", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "status", Name = "Sidebar_Status", ControlName = "DaisyStatusIndicator", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "skeleton", Name = "Sidebar_Skeleton", ControlName = "DaisySkeleton", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "toast", Name = "Sidebar_Toast", ControlName = "DaisyToast", TabHeader = "Sidebar_Feedback" },
                        new() { Id = "tooltip", Name = "Sidebar_Tooltip", ControlName = "DaisyTooltip", TabHeader = "Sidebar_Feedback" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Layout",
                    IconKey = "DaisyIconLayout",
                    Items = new()
                    {
                        new() { Id = "drawer", Name = "Sidebar_Drawer", ControlName = "DaisyDrawer", TabHeader = "Sidebar_Layout" },
                        new() { Id = "hero", Name = "Sidebar_Hero", ControlName = "DaisyHero", TabHeader = "Sidebar_Layout" },
                        new() { Id = "indicator", Name = "Sidebar_Indicator", ControlName = "DaisyIndicator", TabHeader = "Sidebar_Layout" },
                        new() { Id = "join", Name = "Sidebar_Join", ControlName = "DaisyJoin", TabHeader = "Sidebar_Layout" },
                        new() { Id = "mask", Name = "Sidebar_Mask", ControlName = "DaisyMask", TabHeader = "Sidebar_Layout" },
                        new() { Id = "mockup", Name = "Sidebar_Mockup", ControlName = "DaisyMockup", TabHeader = "Sidebar_Layout" },
                        new() { Id = "resizablecolumns", Name = "Sidebar_ResizableColumns", ControlName = "DaisyResizableColumnsHost", TabHeader = "Sidebar_Layout" },
                        new() { Id = "stack", Name = "Sidebar_Stack", ControlName = "DaisyStack", TabHeader = "Sidebar_Layout" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Navigation",
                    IconKey = "DaisyIconNavigation",
                    Items = new()
                    {
                        new() { Id = "breadcrumbs", Name = "Sidebar_Breadcrumbs", ControlName = "DaisyBreadcrumbs", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "breadcrumbbar", Name = "Sidebar_BreadcrumbBar", ControlName = "DaisyBreadcrumbBar", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "dock", Name = "Sidebar_Dock", ControlName = "DaisyDock", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "menu", Name = "Sidebar_Menu", ControlName = "DaisyMenu", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "navbar", Name = "Sidebar_Navbar", ControlName = "DaisyNavbar", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "pagination", Name = "Sidebar_Pagination", ControlName = "DaisyPagination", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "steps", Name = "Sidebar_Steps", ControlName = "DaisySteps", TabHeader = "Sidebar_Navigation" },
                        new() { Id = "tabs", Name = "Sidebar_Tabs", ControlName = "DaisyTabs", TabHeader = "Sidebar_Navigation" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Tour",
                    IconKey = "DaisyIconNavigation",
                    Items = new()
                    {
                        new() { Id = "tour", Name = "Sidebar_Tour", ControlName = "DaisyTour", TabHeader = "Sidebar_Tour" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Theming",
                    IconKey = "DaisyIconTheme",
                    Items = new()
                    {
                        new() { Id = "product-themes", Name = "Sidebar_ProductThemes", ControlName = "DaisyProductThemeDropdown", TabHeader = "Sidebar_ProductThemes" },
                        new() { Id = "theme-controller", Name = "Sidebar_ThemeController", ControlName = "DaisyThemeController", TabHeader = "Sidebar_Theming" },
                        new() { Id = "theme-radio", Name = "Sidebar_ThemeRadio", ControlName = "DaisyThemeRadio", TabHeader = "Sidebar_Theming" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Effects",
                    IconKey = "DaisyIconEffects",
                    Items = new()
                    {
                        new() { Id = "reveal", Name = "Sidebar_Reveal", TabHeader = "Sidebar_Effects" },
                        new() { Id = "scramble", Name = "Sidebar_Scramble", TabHeader = "Sidebar_Effects" },
                        new() { Id = "wave", Name = "Sidebar_Wave", TabHeader = "Sidebar_Effects" },
                        new() { Id = "typewriter", Name = "Sidebar_Typewriter", TabHeader = "Sidebar_Effects" },
                        new() { Id = "cursor-follow", Name = "Sidebar_CursorFollow", TabHeader = "Sidebar_Effects" }
                    }
                },
                new()
                {
                    Name = "Sidebar_Showcase",
                    IconKey = "DaisyIconEffects",
                    Items = new()
                    {
                        new() { Id = "showcase", Name = "Sidebar_Showcase", TabHeader = "Sidebar_Showcase" }
                    }
                },

                // Custom Controls and Color Picker stay at bottom
                new()
                {
                    Name = "Sidebar_CustomControls",
                    IconKey = "DaisyIconSun",
                    Items = new()
                    {
                        new() { Id = "flow-kanban", Name = "Sidebar_FlowKanban", ControlName = "FlowKanban", TabHeader = "Sidebar_FlowKanban" },
                        new() { Id = "scheduler", Name = "Sidebar_Scheduler", ControlName = "DaisyScheduler", TabHeader = "Sidebar_Scheduler" },
                        new() { Id = "scaling", Name = "Sidebar_ScalingItem", TabHeader = "Sidebar_Scaling" },
                        new() { Id = "size-dropdown", Name = "Sidebar_SizeDropdown", ControlName = "DaisySizeDropdown", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "modifier-keys", Name = "Sidebar_ModifierKeys", ControlName = "DaisyModifierKeys", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "weather-card", Name = "Sidebar_WeatherCard", ControlName = "DaisyWeatherCard", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "current-weather", Name = "Sidebar_CurrentWeather", ControlName = "DaisyWeatherCurrent", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "weather-forecast", Name = "Sidebar_WeatherForecast", ControlName = "DaisyWeatherForecast", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "weather-metrics", Name = "Sidebar_WeatherMetrics", ControlName = "DaisyWeatherMetrics", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "weather-conditions", Name = "Sidebar_WeatherConditions", TabHeader = "Sidebar_CustomControls" },
                        new() { Id = "service-integration", Name = "Sidebar_ServiceIntegration", TabHeader = "Sidebar_CustomControls" }
                    }
                },
                new()
                {
                    Name = "Sidebar_ColorPicker",
                    IconKey = "DaisyIconPalette",
                    Items = new()
                    {
                        new() { Id = "colorwheel", Name = "Sidebar_ColorWheel", ControlName = "DaisyColorWheel", TabHeader = "Sidebar_ColorPicker" },
                        new() { Id = "colorgrid", Name = "Sidebar_ColorGrid", ControlName = "DaisyColorGrid", TabHeader = "Sidebar_ColorPicker" },
                        new() { Id = "colorslider", Name = "Sidebar_ColorSliders", ControlName = "DaisyColorSlider", TabHeader = "Sidebar_ColorPicker" },
                        new() { Id = "coloreditor", Name = "Sidebar_ColorEditor", ControlName = "DaisyColorEditor", TabHeader = "Sidebar_ColorPicker" },
                        new() { Id = "screenpicker", Name = "Sidebar_ScreenPicker", ControlName = "DaisyScreenColorPicker", TabHeader = "Sidebar_ColorPicker" },
                        new() { Id = "colorpickerdialog", Name = "Sidebar_ColorPickerDialog", ControlName = "DaisyColorPickerDialog", TabHeader = "Sidebar_ColorPicker" }
                    }
                }
            };

            if (!OperatingSystem.IsBrowser())
            {
                var cardsCategory = categories.FirstOrDefault(category => category.Name == "Sidebar_Cards");
                if (cardsCategory != null)
                {
                    var insertIndex = cardsCategory.Items
                        .Select((item, index) => new { Item = item, Index = index })
                        .FirstOrDefault(entry => entry.Item.Id == "daisyglass-full-width")
                        ?.Index ?? cardsCategory.Items.Count;

                    cardsCategory.Items.Insert(insertIndex,
                        new SidebarItem
                        {
                            Id = "daisyglass-skia-matrix",
                            Name = "Sidebar_DaisyGlassSkiaMatrix",
                            TabHeader = "Sidebar_Cards"
                        });
                }
            }

            ApplyLocalization(categories);

            return categories;
        }

        /// <summary>
        /// Creates the default languages for the Gallery showcase app.
        /// Uses the centralized language data from the library.
        /// </summary>
        public static ObservableCollection<SidebarLanguage> CreateLanguages()
        {
            return SidebarLanguage.CreateAll();
        }

        private static void ApplyLocalization(ObservableCollection<SidebarCategory> categories)
        {
            foreach (var category in categories)
            {
                category.DisplayName = FloweryLocalization.GetString(category.Name);

                foreach (var item in category.Items)
                {
                    item.DisplayName = FloweryLocalization.GetString(item.Name);
                }
            }
        }
    }
}
