<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.13.0] - 2026-11-01

### Added

- NativeAOT for the Gallery, the Kiosk and Photos: `publish_windows.ps1 -Aot` compiles each application ahead of time into one native executable, without the .NET runtime and JIT; the Kiosk window appears in about a third of the time. The release workflow publishes `Flowery.WinUI.Gallery-Windows-x64.zip`, `Flowery.WinUI.Kiosk-Windows-x64.zip`, `Flowery.WinUI.Photos-Windows-x64.zip` and the Photos installer this way. CI publishes all three applications with NativeAOT, runs the Gallery smoke run on the NativeAOT Gallery and the Kiosk smoke checks on the NativeAOT Kiosk, and starts the NativeAOT Photos; `test_kiosk.ps1 -ExecutablePath` runs the smoke checks on any published Kiosk, and the new `test_photos_start.ps1` fails when a published Photos ends early or writes `crash.log`. The release workflow runs these checks before it packages the applications.
- `Flowery.WinUI`, `Flowery.WinUI.Win2D`, `Flowery.WinUI.Kanban` and `Flowery.WinUI.Gallery.Core` are marked AOT-compatible (`IsAotCompatible`), so their builds run the trimming, AOT and CsWinRT AOT analyzers; they build without such warnings. The unit test `Given_LibraryXamlBindings` stands in for the XAML compiler's check WMC1510, which these projects suppress, and now covers the Kanban templates and the Gallery pages: a binding to a plain data context must sit in an item template whose item type and every type on its path are bindable, and a Flowery control that a binding reads by property name (`TemplatedParent`, `ElementName`) must carry `[WinRT.GeneratedBindableCustomProperty]`.
- `FloweryLocalization` and the Gallery's `GalleryLocalization` carry `[WinRT.GeneratedBindableCustomProperty]`, so `{Binding [Key]}` resolves their texts in a NativeAOT app. `LOCALIZATION.md` shows the attribute on an app's own localization class, and `llms-static/DaisySelect.md` shows which item lists a NativeAOT app passes to `ItemsSource` as they are.
- Controls whose properties bindings read by name carry `[WinRT.GeneratedBindableCustomProperty]`, without which a NativeAOT app throws `NotSupportedException` in layout: `DaisyButton`, `DaisyCard`, `DaisyPatternedCard`, `DaisyInput`, `DaisyResizableColumnsHost`, the weather card and its parts, and `FlowKanban`, `FlowKanbanHome`, `FlowKanbanColumn` and `FlowTaskCard`. The Kanban item types that templates bind to (`FlowBoardMetadata`, `FlowKanbanLane`, `FlowKanbanLaneRowView`, `FlowKanbanLaneCellView`, `FlowKanbanFilterPreset`, `FlowTaskAssigneeOption`) are `[Bindable]`.
- `FlowKanbanData.ColumnCount` follows the columns; the board's status bar shows it instead of `Columns.Count`, which a NativeAOT app cannot read by name.
- Gallery smoke run: `--smoke-test <report.xml>` makes the Gallery open every category in English, scroll through it and record per category an exception, a page that does not load and a localized text that stays empty. WinUI reports other failed bindings only to a debugger, so the run does not see them. It also records a `NotSupportedException` on the UI thread, which a NativeAOT build raises for missing marshalling or binding code, even when WinUI catches it; the Gallery writes such exceptions to `crash.log` as well. A category that ends the Gallery fails with the entries of `crash.log` that name the cause, and the script starts the Gallery again behind it (`--smoke-skip`), so one run names every such category. `test_gallery_smoke.ps1` runs it with an isolated state directory, also on a published Gallery (`-ExecutablePath`), and CI runs it in every Windows build. The Kiosk and the Gallery write their smoke reports through the shared `SmokeReport` of `Flowery.WinUI.Gallery.Core`.

### Changed

- The GL hosts (`CarouselGlEffectHost`, Gallery `CarouselGlTransitionHost`) write their requests and shader sources through source-generated JSON metadata instead of reflection; the messages the engine script reads stay the same.
- Native calls use source-generated `LibraryImport` instead of `DllImport`, in `DaisyScreenColorPicker` and in the Gallery, Kiosk and Photos.
- `DaisyResourceLookup.GetPaletteBrushes`, the time and duration parsers of `FloweryTimeHelpers` and the icon exporter compare their fixed prefixes ordinally instead of by the user's culture. The analyzer rule CA1310 reports such calls as warnings.
- The tour editor and the Kanban task editor list enum values without code that NativeAOT cannot generate.
- The color picker checkerboards fill whole rows of checks, and the Photos dominant colors sum each cluster in one pass instead of one LINQ pass per cluster, about three times as fast with a twelfth of the allocations; both give the same pixels and colors as before.
- The runtime tests run in their own WinUI application, `Flowery.WinUI.RuntimeTests.Host`, which loads the Gallery resources, assets and fonts. The Gallery no longer contains the tests, MSTest or a test mode, so its published output ships without them. `test_windows.ps1` starts the host through the runner, which reads its path from `FLOWERY_RUNTIME_TESTS_HOST_PATH` instead of `FLOWERY_GALLERY_EXE_PATH`.
- `DaisySelect` (with `DaisySizeDropdown` and `DaisyThemeDropdown`) and `DaisyBreadcrumbBar` hand a plain list to WinUI as a copy in a `List<object>`, with the same items in the same order; a collection that reports its changes passes unchanged. In a NativeAOT app a list of the app's own item type failed with "Value does not fall within the expected range", or "not a supported vector" in the breadcrumb bar, because the app has no marshalling code for that list type behind the `object` property. Photos no longer names its option list for this.
- `DaisyScheduler.AppointmentColors` keeps a copy of the colors in a list of its own. A NativeAOT app cannot hand WinUI an array of `Color` as a dependency property value, so setting the palette as an array threw `NotSupportedException` there. The copy happens in the property setter: `SetValue`, a `{Binding}` or a style setter still hand an array to WinUI as it is.
- `DaisyDateTimeline` builds its date items (about 120 with the default range) once when it loads. Before, it built them again for every property set before loading, such as from XAML, and three times on load: a theme change now only recolors the header, as the items follow the theme themselves, and the size refresh on load rebuilds only when the resolved size differs from the one the items were built with. When the timeline resolves its item width or spacing itself, it builds the items once instead of twice. A `Locale` change still updates `HeaderText` and the accessible name at once, also before loading. Runtime tests count the builds and check that properties set before loading apply, that the items use a resolved width, that the header follows `Locale`, and that a theme or global size change still reaches the items.
- `FlowerySizeManager`: changing `IgnoreGlobalSize` (the attached property or `SetIgnoreGlobalSize`) on a loaded element refreshes only that element and the loaded controls below it, the only ones whose policy can change, instead of every loaded control.

### Fixed

- `FloweryTimeHelpers.TryParseTime` reads the AM/PM labels of every language: it removes the whole label, whatever its length, and also a label before the time, where `FormatClockTime` puts it in right-to-left languages. Before, it always removed two characters and only at the end, so the clock texts of Arabic and Hebrew did not parse back, and `10:30ص` lost its last digit.
- `DaisyMockup` with `ChromeStyle="Windows"` and an `AppIcon` element no longer throws when it loads or changes its chrome: the rebuild right after `OnLoaded` moved the icon into a new header while the previous header still held it, which neither `Parent` nor `VisualTreeHelper` reports before layout. The mockup now releases the icon from the header it gave it. The Gallery smoke run found the crash on the Layout page.
- `DaisyWeatherForecast` spaces its days by `ItemSpacing`. The items panel bound its spacing to `TemplatedParent`, which in an items panel template is not the forecast, so the spacing stayed 0; in a NativeAOT app the binding threw and ended the app. The forecast now sets the spacing of its panel itself.
- A board whose `FlowKanbanData.Columns` gets a new collection shows the new columns: the property now raises `PropertyChanged`, which `FlowKanban` waits for to rebuild its columns, and follows the new collection.
- The GL carousel hosts (`CarouselGlEffectHost`, Gallery `CarouselGlTransitionHost`) draw again after their WebGL context was lost while they compiled a shader, such as when the GPU process restarts. The engines compile a program only after their images and the transition have loaded, and a context lost in between failed the compile without a log ("Shader compile failed"), which ended the renderer for good, although the browser restores the context and the host sets itself up again then. A runtime test loses the context during a compile; CI hit it once.

## [1.12.0] - 2026-10-30

### Added

- `DaisyThemeManager.BaseOverride`, as in Flowery.NET: a base color that replaces `Base100` of every theme at run time and stays across theme switches, which spares palettes that differ only in their base colors. Light or dark follows from its luminance, and with it `IsCurrentThemeDark`, `IsDarkTheme` of the current theme and the neumorphic shadow colors. `Base200`, `Base300` and `BaseContent` keep the contrast ratio and the side their own theme gives them, `BaseContent` at least 4.5:1; the border, the combo box highlight and the muted, subtle and disabled text follow. The text colors of the accents and the link move to 4.5:1 against the new base, and the accent surfaces stay. A theme that a `CustomThemeApplicator` applies takes the override from the colors it put in place. A Windows contrast theme wins over the override. `BaseOverrideChanged` reports a change, which also raises `ThemeChanged` with the current theme.
- `DaisyThemeManager.IsCurrentThemeDark`, and `FindContrastGaps()` with `DaisyContrastGap`: the color pairs of the theme as drawn below the WCAG contrast, from the base text on the three base colors to the text on each accent surface.
- `FloweryColorHelpers.GetRelativeLuminance`, `GetContrastRatio`, `EnsureContrast` and `Composite` for the WCAG 2 contrast, as in Flowery.NET.
- Gallery: the sidebar offers the base color below the size, as a color button with a reset ("Base Color" in all twelve languages). The Gallery saves it next to the theme and restores it at startup.
- `DaisyScheduler.ColumnZoom`, as in Flowery.NET and Flowery.Uno: it widens the day columns of Month, Day and Week and the hours of Planner and Timeline from 1 (default) to 4 times the width that fits them into the view, and the view scrolls sideways. Users zoom by spreading or pinching two fingers on a touch screen or a precision touchpad; by dragging the right edge of a column heading with the mouse, which shows a resize cursor; with Ctrl and the mouse wheel; by a double click or two taps on a column heading, which switches between fitted and twice as wide columns; and with Column width in the settings menu. On a touch screen the columns follow the fingers and the day between them stays in place; a touchpad pinch scales the view while the fingers move, and the columns take the zoom when they lift. A second finger ends what the first one started, a tap, hold or drag, without a change. `DaisyScheduler.StateKey` keeps the user's zoom in `StateStorageProvider` for the next start; the Gallery sets it, and its hints name the gestures.
- Kanban: spreading two fingers zooms `FlowKanban` in and pinching zooms it out, one size per quarter of the fingers' distance, within the sizes of the zoom buttons, on a touch screen and on a precision touchpad. Ctrl and the mouse wheel change the size by one per notch, also from the parts of a notch that precision wheels and touchpads send. A pinch neither opens, selects nor drags a card, and the board does not scroll with the fingers.

### Changed

- `DaisyThemeSwap` goes by the theme as defined, so under a light base on a dark theme it still swaps to the light theme.
- `DaisyPatternedCard` picks its white or black pattern images by the theme as drawn (`IsCurrentThemeDark`). Before, a fixed list of dark theme names decided, which left out dark themes such as Abyss, Black and Smooth.

### Fixed

- Kanban, as in Flowery.NET: a tap on a card decides whether cards are selected through the board it finds at the tap. The card looked its board up in its `Loaded` handler, which WinUI raises after the card is in the tree, so a touch on a card realized just then, such as while a long column scrolls, opened its editor while other cards were selected.
- Kanban, as in Flowery.NET: an edit that still waits for its auto-save is saved at once when the board unloads, before the home view clears the last board ID, and while its window closes. WinUI unloads the board of a closed window only later, and closing the last window ends the app before that. The same happens before a board loads, so reopening the open board shows the edit, and before `Board` or `BoardStore` changes: the edit is saved on the board it was made on and into the store it was meant for, and a replaced board does not become the last board. Deleting the open board drops its waiting edit instead of saving the board back. Before, the timer kept running and later saved whatever board was open then into the store and state storage current then, and set the last board ID again.
- Gallery: the `pixelize` GL transition pixelates again. The transition shaders read the `progress` uniform directly, so global initializers that read `progress` see the real value. Before, they read a copy that `main` set only after them, which was still 0: `pixelize` drew a plain crossfade.
- GL hosts (`CarouselGlEffectHost`, Gallery `CarouselGlTransitionHost`): a paused host, out of view or with animations disabled, draws its frame at once when a setting such as `IntervalSeconds` changes and its effect, variant and images stay, and a host that pauses while a request waits for its next frame draws that frame. Before, the request waited for the paused draw loop and failed the renderer after the 30-second request timeout.
- GL hosts: an engine failure, such as an image that does not decode, raises `RenderFailed` once. Before, the request that the failure also rejected raised it a second time.
- GL hosts: with animations disabled the host shows its last frame as a still image. A resize, which clears the canvas, draws a new still frame, and the Gallery's transition host draws a changed `TransitionName` at once. Before, pausing hid the canvas and the host stayed empty.
- GL hosts, as in Flowery.NET: a host that is unloaded and loaded again while an update of its settings still runs gets its renderer back. Before, the reload left the new renderer to that update, which gave up on the renderer the unload had closed; the host stayed empty, and reading a frame failed with "No GL effect is selected.".
- GL hosts: a host whose WebGL context is lost, such as when the GPU process of WebView2 restarts, sets itself up again once the browser restores the context, and its requests wait for that. Before, the next frame read back `CONTEXT_LOST_WEBGL` and failed the renderer.
- Gallery: the `BookFlip` GL transition selects the page for each side of the fold instead of blending both pages with a weight of 0 or 1. At exactly half progress the unused page divides by zero, which GLSL leaves unspecified, so a GPU could put NaN pixels into the frame; WebView2's ANGLE drew the frame correctly. A test checks that the middle frame shows half of each image.
- Gallery, as in Flowery.NET: the Carousel GL and Carousel GL Transitions pages keep their values when they load again. Before, every reload set the inputs back to their defaults, Carousel GL saved those over the stored settings, and both pages registered their value handlers once more. Carousel GL also saves nothing before it has restored its settings, so the events its inputs raise while the page loads no longer replace them. The dissolve density shows two decimals and the intervals one, so 0.5 no longer shows as 1 and 1.5 s as 2. The note below the controls says they drive the preview, and the descriptions of the pages and their demos describe the WebGL renderer in WebView2 instead of Skia and the browser-only version of Flowery.Uno, in all twelve languages.
- `DaisyToggle` shows its `Content` as the label next to the track, as on a `ToggleButton` and as its documentation shows; `Header` takes its place when it is set. Before, only `Header` was shown, so toggles with `Content`, such as the Infinite toggles of the Carousel pages and the Styling examples of the Gallery, had no label. `llms-static/DaisyToggle.md` describes the label and the `IsOn`/`IsChecked` state.
- Gallery: the guide labels of the Carousel and Carousel GL pages are as wide as the longest label, and their descriptions wrap. Before, a fixed width of 100 cut off longer labels such as "Stagger-Umschalter" and "Dissolve-Dichte".

## [1.11.0] - 2026-10-27

### Added

- `FlowKanban.AssigneeAdapter`, `IFlowKanbanAssigneeAdapter`, `FlowKanbanAssignee` and `FlowKanbanAssigneeAdapter`, as in Flowery.NET 3.1.0: the host application supplies the assignees of a board as stable IDs with display names, optional avatars (`ImageSource`) and descriptive roles, either as a direct list (`SetAssignees`), through a list callback, or through a single-ID resolver for IDs that saved tasks still carry. The board reloads them when it loads, when `Board` or `AssigneeAdapter` changes and when the adapter raises `AssigneesChanged`, also from another thread; `RefreshAssigneesAsync` reloads on request. Added, moved and reassigned tasks take the last loaded assignees and resolve only an ID the adapter has not been asked about, once. A newer reload replaces an older unfinished one, and an unfinished reload cannot change an unloaded board. Names the adapter updates raise no `CardEdited` and start no auto-save.
- `FlowKanban.GetAssigneeOptions()` returns the adapter's assignees, sorted by name, as the choices for `FlowTaskEditorDialog.ShowAsyncWithAssignees`.
- `FlowKanban.AssigneeAdapterFailed` reports a failed adapter call with the operation, the exception and the assignee ID. Saved assignments and names stay as they are.
- `FlowTask.AssigneeAvatarSource` and `AssigneeRoles` (not saved to JSON) and the card properties `AssigneeAvatarSource`, `HasAssigneeAvatar`, `IsAssigneeAvatarPlaceholder`, `AssigneeRolesText` and `HasAssigneeRoles`: a task card shows the assignee's avatar in place of the initials and the roles as the avatar's tooltip.
- `FlowTaskAssigneeOption.IsResolved`: the assignee filter lists an assignee that only saved tasks know, so a saved filter keeps working. With an adapter set, the entry is marked "(Unavailable)" in all twelve languages.
- The demo board of the Kanban home screen gives its tasks the assignee IDs `demo-<name>` ("demo-sam"), as in Flowery.NET.
- Gallery: the Kanban page's sample tasks are assigned; Sam and Dario have avatar images, every demo assignee has a role. Gallery boards saved before 1.11 have their `local:demo-<name>` IDs mapped to `demo-<name>`.
- Docs: the "User API" section in `llms-static/FlowKanban.md` explains the adapter, its three forms and the task editor hand-off.

### Changed

- **Breaking:** The `Flowery.Integrations.WinUI` package is removed. Its Teams and Slack providers threw `NotImplementedException` in every data method, the OAuth provider never re-validated a stored token, the loopback sign-in used a fixed port and blocked the UI thread, and the `IdentityModel.OidcClient` dependency has reached its end of life. External identity, authentication, authorization, account mapping and token storage are the host application's responsibility. The Gallery no longer registers GitHub or OpenID Connect demo providers.
- **Breaking:** The Kanban user-provider API is removed: `IUserProvider`, `ICompositeUserProvider`, `IFlowUser`, `FlowUser`, `FlowUserStatus`, `CompositeUserProvider`, `LocalUserProvider`, `FlowUserIdHelper`, `IUserCacheService`, `UserIdentityLinkStore`, the token interfaces (`ITokenSaveProvider`, `ITokenStateProvider`, `ITokenValidationProvider`, `IInteractiveAuthProvider`), `FlowKanban.UserProvider`, `FlowKanban.DefaultUserProvider`, `FlowKanban.GlobalAdminId`, `FlowKanban.EnsureGlobalAdminAsync`, `FlowKanban.IsUserManagementButtonEnabled`, `FlowKanban.IsUserManagementButtonVisible`, `FlowKanban.ShowUserManagementCommand`, `FlowKanban.IsUserManagementViewActive`, `FlowKanbanView.UserManagement`, the user management view (`FlowKanbanUserManagement`) with its GitHub connect dialog and sidebar button, and the `Kanban_Users_*` texts except `Kanban_Users_Unresolved`. Assignee IDs are opaque: the board keeps them exactly as the host supplied them (no trimming) and no longer clears an ID that no provider knows; on load, an ID longer than 200 characters or with a control character is dropped instead of shortened.
- **Breaking:** `FlowTaskAssigneeOption` takes an optional third constructor parameter (`isResolved`). Source code compiles unchanged, but binaries built against 1.10 must be rebuilt because the two-parameter constructor no longer exists.
- **Breaking:** `FlowKanban.TryLoadUserSettings` and `TrySaveUserSettings` are removed; `TryLoadSettings` and `TrySaveSettings` read and write one settings entry (`kanban.settings`) instead of one per user; the first load moves the local user's per-user entry (or the only one) into it and removes the per-user entries. The "global admin" is gone: the column shortcuts Ctrl+B, Ctrl+D and Ctrl+T and their rows in the keyboard help apply to everyone.
- Kanban: the first settings load deletes the state entries the removed user providers left behind (`FlowKanban.LocalUserId`, `FlowKanban.IdentityLinks`, `kanban.admin.global`). The Gallery removes once the tokens its former GitHub and OpenID Connect demo providers stored in the Windows Credential Manager (`Flowery.GitHub`, `Flowery.OAuth.oidc-demo`). Apps that used the removed providers with `PasswordVaultSecureStorage` should remove their entries under `Flowery.GitHub` and `Flowery.OAuth.<providerKey>`.
- `ISecureStorage` and `PasswordVaultSecureStorage` stay in the core package as a general service.

## [1.10.0] - 2026-10-27

### Added

- `DaisyScheduler`, ported from Flowery.NET 3.6.0 to 3.8.0 and adapted from the MIT-licensed Xtreme.Components scheduler. It has Day, Week, Month, Agenda, Calendar, resource Planner and horizontal Timeline day views, typed resources with resource kinds, named and collapsible Timeline groups, occupancy and conflict queries, continuous and daily appointment spans, week rules, editor time slots, regional date/time formats and 12/24-hour clocks. Selection, move, resize, creation, deletion, tooltips, decorations, templates, renderers, toolbar buttons and external drops raise host events and leave the host data unchanged.
- `DaisyScheduler.ShowWeekends` (on by default) shows or hides Saturday and Sunday in the Week, Month and Planner views; the settings flyout has a check box for it.
- Unlike Flowery.NET, chips and bars never trim their start time: status glyphs and tiles show only when they fit beside it, and the subject takes the rest. A Planner or Timeline hour is as wide as a one-hour bar with its full start time (for the current size, font, culture and clock format), every hour has a header label, and hours widen to fill the visible width; `DaisySchedulerTimelineMinimumHourWidth` (default 0) can raise the minimum.
- Unlike Flowery.NET, Month and Agenda chips and Planner and Timeline bars show their subject in the secondary font size so it fits the bar height in Segoe UI, timed blocks use compact subject and time lines, and the Day and Week time column widens to its longest time label. Bind `SelectedDate`, `SelectedAppointment` and `CollapsedResourceGroupIds` with `Mode=TwoWay`, because WinUI has no per-property default binding mode.
- `DaisyScheduler` on touch or pen, as in Flowery.NET and the apps of the platforms: a tap on an appointment asks to edit it, and holding it selects it; the finger then moves it, or resizes it from an edge up to 12 DIP wide, at most a quarter of the appointment each. While appointments are selected, a tap adds one to the selection or takes it out. Taps are decided on release and count only if the finger did not move, so any swipe without a hold only scrolls, also over a free slot or day, where a tap selects it and a double tap asks to create an appointment; a tap that stops a scrolling view opens nothing. The mouse keeps click, Ctrl-click and double click.
- The scheduler uses the Flowery themes, design tokens (overridable in host resources), all five size tiers and all twelve languages. `ExternalDropHandler.SetData` and `TryGetItem` carry an `ISchedulerDroppable` through a `DataPackage` in the same process.
- The library output and NuGet package include the Xtreme.Components copyright and MIT license (`licenses/Xtreme.Components.txt`).
- Gallery: a Scheduler page under Custom Controls with sample appointments, resource grouping, an appointment editor with overlap confirmation and color palette, a context menu, read-only mode, and resource-kind, date/time format and clock selectors. Its help below the scheduler names the touch gestures after a touch or pen press and the mouse and keyboard ones after a click, in all twelve languages.
- Docs: `llms-static/DaisyScheduler.md`, and the scheduler in the custom-control list of the generated site and in the README.
- `DaisyTour.StepTargetUnavailable` reports a step that cannot show everything it describes, with the step, the cause (`DaisyTourTargetProblem`: `Preparation`, `Target` or `Annotation`), the annotation, the element and the error, once per cause and visit, after the step is laid out. Handlers may skip, move on or replay the tour; a handler that throws is logged. While the editor previews the tour, the editor shows the error instead. `DaisyTourCloseReason.Failed` reports a tour that could not continue.

### Changed

- `DaisyButton` applies style properties set before it loads (`Variant`, `Size`, `ButtonStyle`, `Shape`, `ContentPadding`) once, at its first measure, instead of once per property. On Loaded it skips the size and theme pass while the applied size and theme are still current. Creating and loading many buttons takes a fraction of the time.
- `FlowKanban` on touch or pen, as in `DaisyScheduler`: a tap opens a card's editor and holding a card selects it, and the finger then drags it; while cards are selected, a tap adds a card to the selection or takes it out. Taps are decided on release and count only if the finger did not move, so a swipe over a card only scrolls. Before, a touch or pen press selected a card like a click, and a double tap edited it. The touch model is chosen by pointer type; the old one for Android and iOS (double tap toggles, long press on the title edits) never ran on Windows and is gone.
- `FlowKanban`: a tap or click on a free place of the board (not on a card, a button, an input, a combo box such as the column choice of the compact layout, a scroll bar, or a column header, where a tap shows the header buttons on touch) clears the card selection, also one the board was loaded with.
- `DaisyResourceLookup.TryGetResource` no longer searches the merged dictionaries again after a miss. WinUI's `TryGetValue` already searches nested merged and theme dictionaries, so the result is the same, and lookups of optional keys that do not exist, which every Daisy control makes, are much faster.
- `DaisyTour`: a target that cannot be shown no longer closes the tour, which looked like a crash to the user. A missing, collapsed, transparent, detached or foreign-window step target, one that stays outside the visible area after the tour scrolled it into view once, an unavailable annotation end and a failed `PrepareAsync` leave the step open in a warning state: the card waits at its stationary position (the center for `Target`), its frame and the current progress step take the warning color, a note says that the element cannot be shown right now or that the step could not be prepared completely, screen readers hear the note, and connectors that need the element are hidden. The step returns to normal when the element can be shown again; a failed preparation lasts for the visit. An unexpected failure of the tour itself closes it with `Failed` instead of `TargetUnavailable`, which now only means that the tour could not open because it belongs to no window, and `DaisyTourClosedEventArgs.AnnotationId` is always null.
- Release and CI: publishing Gallery, Kiosk and Photos for a runtime no longer downloads the ASP.NET Core and Windows Desktop runtime packs, which none of the apps uses (`DisableTransitiveFrameworkReferenceDownloads`).
- Development: the runtime tests no longer check property values that a test has just set or that are plain defaults, and tests that built the same scene for different inputs share one table-driven test, so there are about 360 tests instead of 455. The unit tests build and run as their own project in the solution and CI (`scripts/test_windows.ps1`) instead of being compiled into the runtime tests; the global neumorphic settings are tested at run time, because their class registers WinUI dependency properties.

## [1.9.0] - 2026-10-24

### Added

- Sizing: `DaisySize.Auto` (-1) is the default `Size` of every Daisy control. An Auto control takes the size of its containing composite control (button group, avatar group, list row, steps, dock, Fab, accordion, weather forecast) or the global size; `ResolvedSize` reports the concrete tier. The five tiers keep their values 0 to 4. `FlowerySizeManager.ResolveSize` resolves a requested size for an element.
- `DaisyButton.ContentPadding` (`Thickness?`) and `ResolvedContentPadding`; `DaisyCard.ResolvedBodyPadding`.
- `DaisyAccordion` and `DaisyAccordionItem`: `Size`. An accordion's size is the default for its items.
- `DaisyStat`: `ValueFontSize` and `LabelFontSize`.
- `DaisyBreadcrumbs` and `DaisyBreadcrumbItem`: `SeparatorBrush`. The item's brush wins over the container's.
- `DaisyNumberFlow.DigitForeground`.
- `DaisyMenuItem.IsTitle`: a non-interactive section heading (Primary, bold, Base300 divider below) that pointer, keyboard and selection skip.
- Theming: every palette, including registered custom palettes, provides `DaisyBaseContentMuted` (70 % of BaseContent), `DaisyBaseContentSubtle` and `DaisyBaseContentDisabled` (50 %), and `DaisyButton{Variant}Soft`, `SoftHover` and `SoftBorder` (15, 30 and 45 % of the variant color; Business Primary Soft 12 %) colors and brushes. A color or brush the palette defines itself is kept; high contrast keeps its own brushes.

### Changed

- **Breaking:** Sizing: an unset `Size` now reads `Auto`. Code that reads `Size` for layout or token lookups must read `ResolvedSize`. Constructors, `DaisyControlLifecycle`, the size manager and the Gallery no longer write `Size`, so explicit values, styles and bindings keep their precedence and an explicit Medium stays explicit when the global size changes. Without a global size, Auto resolves to Medium, to Small for `DaisyModifierKeys`, `DaisyDivider`, `DaisyTabsHeaderButtons` and `DaisyTagPicker`, and to Large for `DaisyFab`.
- **Breaking:** `FlowerySizeManager.ApplySize` throws `ArgumentOutOfRangeException` for `Auto` or a value outside the five tiers. With `EnableGlobalAutoSize` set to false it no longer refreshes responsive TextBlocks; `RefreshAllSizes` catches up all loaded controls.
- **Breaking:** `DaisyControlLifecycle` only handles theme changes. Its constructor no longer takes `getSize`, `setSize` and `subscribeSizeChanges`. The protected `DaisyComboBoxBase(bool subscribeSizeChanges)` constructor is removed.
- **Breaking:** `DaisyBaseContentControl`: the virtual `OnGlobalSizeChanged` and `SetControlSize` are replaced by `OnResolvedSizeChanged` and `DefaultSize`.
- **Breaking:** Button: `Padding` is outer spacing added to the automatic inner padding. Code that set `Padding` to replace the inner padding sets `ContentPadding` instead; `null` selects the automatic padding, zero is kept. Custom templates bind `ResolvedContentPadding`.
- **Breaking:** Card: `BodyPadding` is `Thickness?`. `null` selects 16/24/32/40/48 by tier (half in Compact); explicit values including zero survive size and variant changes. XAML values stay valid; code and custom templates that need the concrete value read `ResolvedBodyPadding`.
- Composite controls give Auto children their size without overwriting a child's own `Size`. Dock no longer writes its items' `IconSize` or `IgnoreGlobalSize`; a supplied `DaisyWeatherForecastItem` keeps its `Size`.
- Soft buttons and button groups use the Soft surface, hover and border brushes in the seven color variants.
- Breadcrumbs: separators use the Subtle brush at `SeparatorOpacity` 1 (an explicit opacity still multiplies); the last item uses Muted. Authored content inherits the item's foreground.
- Stat: title and description use the Muted brush at full opacity. Input, PasswordBox and TextArea: hint, helper and optional text use Muted, placeholders Subtle. CalendarDatePicker: the date hint uses the placeholder brush, a selected date the full foreground.
- ThemeController: the checkbox mode follows the checkbox size tokens.
- Gallery: the sidebar takes the library widths 190/205/220/235/250 per tier, the four selectors stretch to the sidebar width, and a size change keeps the pane open or closed. Gallery and Photos ignore a saved global size that is not one of the five tiers.
- Gallery: in the glass demos of the Cards page (Glass Card, DaisyGlass Simulated, Bitmap Capture and Full Width), every card or glass panel has a row of six stars. Two stay left of it and four run half behind its lower edge, so sharp and covered stars sit side by side; the cards are 100 apart to make room. In the Glass Card demo, the comparison caption sits above the cards.
- Runtime tests run in English, whatever the saved Gallery language.
- Build: the Skia backdrop capture and image processor call the `DrawBitmap` overloads with `SKSamplingOptions`; the obsolete overloads raised `CS0618`.
- Build: the Kiosk smoke report has an inconclusive outcome. A scaling checkpoint whose only problem is a zoom panel overflowing the slide is inconclusive instead of failed; its text measures taller in local console sessions than over Remote Desktop. `test_kiosk.ps1` lists inconclusive checks as warnings and fails only on failed checks.
- Build: `test_windows.ps1`, `test_kiosk.ps1` and `run-kiosk.ps1` accept `-WindowRegion UpperHalf`, and the Gallery, Kiosk and Photos accept `FLOWERY_MONITOR_REGION=upper-half`, to place the window in the upper half of the chosen monitor. The physical rendering probe checks that region too. Window placement stays optional: `run-kiosk.ps1` no longer defaults to monitor 2 and, like the test scripts, leaves the window to Windows unless `-Monitor` is given.
- Release: every release asset has a signed build provenance attestation, verifiable with `gh attestation verify <file> --repo tobitege/Flowery.WinUI`, and `SHA256SUMS.txt` lists the SHA-256 checksum of each asset. The release notes explain both checks.

### Fixed

- Button: an authored `MinWidth`, including one from a style or binding, is kept. Every size or shape update reset it to 0 (200 for `Wide`); the `Wide` shape still supplies 200 when nothing else sets a minimum width, and leaving `Wide` removes only that value.
- IconText: a control that loads again shows its current `Text` and icon. Changes made while it was unloaded, for example in a hidden tab, kept the old content.
- Glass: `DaisyGlass` applies its `Padding` to the content. It builds its visual tree in code and ignored `Padding`, so the content touched the edges and the glass was only as tall as its content.
- Tooltip: a tooltip opened with `IsOpen` closes when the control unloads, for example on navigation, and opens again when it loads while `IsOpen` is still true. It stayed on screen without its owner, partly outside the window. `IsOpen` set before the first load now opens the tooltip too.
- Tour: a step no longer closes when another scroll, such as a page control bringing itself into view, moves its target out of view. The tour scrolls the targets into view again and closes with `TargetUnavailable` only when the target stays hidden. This made the Gallery connector demo test fail intermittently.

## [1.8.1] - 2026-10-23

### Added

- `DaisyTourEditor`: live tour styling with editable connector handles, movable information cards, undo/redo, and draft profiles that preserve application bindings. The Gallery includes a designer with JSON import/export and replay.
- `DaisyTourStyleProfile`: versioned styling profiles with explicit overrides and inheritance for tour, step, and annotation settings.
- Build: builds and publishes of Gallery, Kiosk and Photos fail when a required font or its license notice is missing from `Assets/Fonts`. The Kiosk smoke test and a Gallery runtime test also check that each font of the Gallery font family is present, has its license notice and renders its script.

### Changed

- Gallery: new logo, a six-petal teal flower with one warm petal, shared by the Flowery family (Flowery.Uno uses blue, Flowery.NET violet). The executable and installer icon (`app_icon.ico`, now with 16-256 px sizes), the README banner, the Gallery and Kiosk SVG icon/splash assets, the Home banner and the sidebar keycap image are redrawn.
- Build: every project declares `x64` as its only platform, matching its fixed `win-x64` runtime identifier. Visual Studio no longer adds `Any CPU` configurations to `Flowery.WinUI.sln`, which built the same x64 binaries; on Windows on ARM the apps keep running under x64 emulation.

### Fixed

- Photos ships the SIL Open Font License notice for its bundled Noto fonts. `Assets/Fonts/StaticFonts-OFL.txt` contains the copyright notices of the five static fonts and the full license, in the build, publish and installer output (#31).

## [1.8.0] - 2026-10-22

### Added

- `FloweryColorHelpers.GetColorDifference`: the CIE76 color difference (Delta E) between two colors.

### Changed

- Toggle: the knob stays visible in themes whose variant color is close to the track color (e.g. Secondary in the HappyHues themes). It always has a thin BaseContent outline (50 % opacity, none in high contrast mode), the checked track border takes the variant color where that color stands out from the track, and a checked knob whose color is too close to the track (CIE76 color difference below 20) is darkened on a light track or lightened on a dark one, keeping its hue. Knob brushes set by the app are used as given.

### Fixed

- Theming: `DaisyThemeManager.ThemeChanged` and the system contrast change notify their handlers one at a time. A handler that threw stopped the notification, so every control subscribed after it kept its old colors (seen as a `DaisyColorSlider` without its contrast border in CI); the failure is now logged under `Events`. A theme that fails to apply is logged under `Theming` instead of failing silently.
- TextArea: every text area with the same `MinRows` has the same height. The visible border kept the 80 px default when the rows needed more, unless something else re-applied the sizing later; `DaisyInput` now re-applies its sizing when `MinHeight` changes.

## [1.7.0] - 2026-10-21

### Added

- `DaisyTour` and `DaisyTourStep`: window-scoped guided tours with typed steps in XAML or C#, live target highlights, modal input, Next/Back/Skip/Finish, replay, cancellable target preparation through `PrepareAsync`, and one `Closed` outcome per session (`Completed`, `Skipped`, `Cancelled`, `TargetUnavailable`) (#25, #27).
- `DaisyTourAnnotation`: arrows, two-control connectors, stroke patterns, clouds, speech bubbles, and custom geometry or templates, with defaults inherited from the tour and the step (#25, #27).
- Connector paths `Straight`, `Curved`, `Elbow`, `SCurve`, `Bezier`, and `Custom`, plus route limits that keep an area between both ends clear (#27).
- `DaisyTour.CardPosition` and `DaisyTourStep.CardPosition` place the step card beside the target or at one of nine fixed window positions. The card avoids connector courses where it can (#27).
- `DaisyTour.CardBorderBrush` and the `DaisyTourCardBorderBrush` resource set the step card's border. By default, it uses the muted theme text color, which stands apart from the Primary highlight frame.
- Tour navigation texts in all 12 languages; the DaisyTour documentation in `llms-static/DaisyTour.md` (#27, #28).
- Gallery: a Guided Tour page with demos for steps, arrows, lines, clouds and bubbles, custom shapes, regions, paths, and route limits (#27).
- Gallery, Kiosk, and Photos embed the Flowery icon and show it on their windows (#29).

### Changed

- Dependabot groups GitHub Actions updates into one pull request. CI generates the documentation site on every pull request (#29).
- Runtime tests that have no room for their scenario report a warning instead of a failure (#27).

### Fixed

- Daisy controls no longer subscribe to `LayoutUpdated` on load. On a large restored window, these subscriptions could block the UI thread and crash the app during startup (#27).
- `DaisySteps` renders its native templates correctly, and compact indicators no longer reserve an empty label row (#27).

## [1.6.0] - 2026-10-19

### Added

- Windows contrast theme support across the library, Kanban, and Gallery, with live system colors and visible control states.
- `DaisyHighContrast.ContentOpacity` keeps captions opaque in Windows contrast themes and restores their normal opacity afterward.
- Shared internal Composition support for visual ownership, retained vector graphics, and geometry updates after layout, wrapping, scrolling, and DPI changes (#24).
- Gallery: a Showcase example combines normal WinUI text and input with shared Composition graphics (#24).

### Changed

- `DaisyShadowContainer` now updates existing outer-shadow visuals when size, corner radius, or appearance changes. Shadow-count changes rebuild the visual graph (#24).

### Fixed

- `DaisyStatusIndicator` Spin no longer flashes horizontal or vertical streaks while its two satellites orbit.
- Custom controls show keyboard focus and keyboard-pressed states. Numeric input keeps its focus border after validation feedback.
- `DaisyShadowContainer` no longer replaces or removes an attached visual owned by another component (#24).

## [1.5.0] - 2026-10-19

### Added

- Weekly Dependabot updates for GitHub Actions (#16).
- A NuGet security audit for all 13 C# projects and their transitive dependencies (#16). GitHub Actions runs the audit on pull requests, on `main`, and weekly. The script also supports local audits.
- A Windows workflow reports resolved direct and transitive NuGet dependencies for all 13 C# projects to the GitHub dependency graph (#16).

### Changed

- Dependabot now covers the unit-test and generator projects outside the solution. Shared packages update across projects, with dedicated MSTest and SkiaSharp groups (#16, #21).
- **Breaking dependency update:** Review direct SkiaSharp use before the update. SkiaSharp and its native Win32 assets change from `3.119.2` to `4.153.1` (#22). SkiaSharp `4.153.1` disables RAW/DNG decoding. See the [migration notes](https://mono.github.io/SkiaSharp/docs/releases/4.148.0.html) and [release notes](https://mono.github.io/SkiaSharp/docs/releases/4.153.1.html).
- Updated `Microsoft.Windows.SDK.BuildTools` from `10.0.28000.1839` to `10.0.28000.2705` (#20).
- The documentation workflow uses supported Python and GitHub Pages actions. CodeQL and documentation workflows now grant write permissions only to the required jobs (#16).

### Fixed

- Glass backdrop capture no longer applies DPI scaling twice. Captures keep the correct pixel size at fractional display scales, including 125% (#16).

## [1.4.1] - 2026-10-18

### Added

- The release contains Flowery Photos: a per-user NSIS installer (`Flowery.WinUI.Photos-Setup-<version>-x64.exe`) with a Start menu shortcut and uninstaller, and a portable `Flowery.WinUI.Photos-Windows-x64.zip`. Before, the release workflow did not build the Photos app.
- `Flowery.WinUI.Photos` has an application icon (`Assets/app_icon.ico`, made from `app_icon.svg`) for its executable, window, taskbar entry and shortcut.

### Changed

- The GitHub workflows use `actions/checkout@v7` and `actions/setup-dotnet@v6`.
- The Kiosk window is at most 1920 × 1080 physical pixels, also when maximized. On larger displays, the slides used scale factors up to 200 %, and the layout broke.

### Fixed

- The published Gallery, Kiosk and Photos apps start. `dotnet publish` left out the resource index of the app itself (`Flowery.WinUI.Gallery.pri`, `Flowery.WinUI.Kiosk.pri`, `Flowery.WinUI.Photos.pri`), so the release ZIPs stopped at startup with a XAML parse error or "Cannot locate resource from 'ms-appx:///…'". The Gallery and Kiosk projects now add their PRI file to the publish output with a small publish target. The Photos project sets `EnableMsixTooling`, which adds the file. All apps stay unpackaged.
- `DaisyTableRow` and `DaisyTableCell` no longer draw their own neumorphic surface when the global neumorphic mode is on; a table showed one raised box per row (#12). `DaisyTable` now gets the one surface it was meant to have: its root border sits in a `Grid`, so the neumorphic helper can place the surface next to it. Before, the helper put the surface into the row panel, where it had no height.
- Gallery: the Table example is 670 px wide instead of 620, so the `Message` button in its last column is no longer clipped.
- `DaisyButton` draws no neumorphic shadows when its face is not opaque. Outline, dash, ghost, link and soft buttons showed the raised shadows through their face as a grey shimmer, most visibly in table cells, where the cell also clipped the halo to a box (#12).

## [1.4.0] - 2026-10-17

### Added

- `Flowery.WinUI.RuntimeTests/VisualChecks/indicator-sizes.html`, a browser page that draws `DaisyToggle`, `DaisyCheckBox` and `DaisyRadio` at their default token sizes in all five size tiers, in the Dark and Light palettes, at real effective pixels.
- The Gallery Toggle and Radio sections show a small `DaisyTable` with the controls in its cells. It follows the global size, so the row height can be checked in every size tier.

### Changed

- **Breaking:** Visual: `DaisyCheckBox`, `DaisyRadio` and `DaisyToggle` indicators are as tall as the line height of their label text in the Windows type ramp, and Medium matches the WinUI defaults. The compact indicators were much smaller than their labels since the font ladder grew in 1.3.0. Checkbox and radio are 14/16/20/24/28 px (was 8/10/12/14/16), the radio dot 8/10/12/14/16 px and the checkmark 8/10/12/14/17 px. Toggle tracks are 28×14, 32×16, 40×20, 48×24 and 56×28 with 8/10/12/14/16 px knobs (was 16×8 to 32×16 with 4–12 px knobs), keeping the WinUI `ToggleSwitch` proportions. The checkbox corner radius is a fifth of the box (3/3/4/5/6 px), so it also follows overridden `DaisyCheckbox{Size}Size` tokens. Review fixed dimensions and style overrides that depend on the previous sizes.
- The `DaisyToggle` track border uses the base content color at 50 % opacity, like DaisyUI's toggle, instead of the full color. The thin full-contrast outline looked jagged on its curves at 100 % display scaling. `DaisyToggleTrackBorderBrush` still overrides it.
- `DaisyToggle.TogglePadding` defaults to `NaN`, which centers the knob with the same gap on every side, like the WinUI `ToggleSwitch` (3 px inside the border at Medium). Set a number to use a fixed horizontal gap as before.
- `DaisyTableRow` reads its lightweight-styling brushes (`DaisyTableRowBorderBrush`, `DaisyTableRowHoverBackground` and the others) from the `Resources` of its `DaisyTable` when the row does not define them itself, so one definition on the table styles all rows.

### Fixed

- The checked `DaisyToggle` knob no longer overlaps the right track border. Its position ignored the 1 px border, so it sat flush with the right edge while the unchecked knob kept a gap on the left.
- `DaisyToggle` no longer keeps the minimum width and height of its first size. It set `MinWidth` and `MinHeight` once, when its template was applied, and never updated them, so a toggle that follows the global size stayed at least 40×20 after switching to a smaller size, which kept table rows and lists from getting lower.
- `DaisyButton` no longer keeps the corner radius of its first size. Like the toggle minimum size, it treated its own token radius as an app override once written, so a button that followed the global size kept the old radius (4/4/8/8/12 px per tier). Radii set by the app, `DaisyJoin` or `DaisyButtonGroup` are still respected.
- A `DaisyToggle` without a `Header` no longer reserves the 8 px gap meant for the header text; it is exactly as wide as its track.
- The `DaisyRadio` dot stays centered at fractional display scales such as 125 % and 175 %. Ring and dot no longer use layout rounding, like the WinUI `RadioButton`.

## [1.3.2] - 2026-10-16

### Fixed

- `DaisyColorPickerDialog` no longer cuts off its right side in windows that are wide but not very tall. The general dialog sizing ties the width to the window height (about 490 px in a 1392×943 window), which was too narrow for the color wheel, palette and color values side by side; the dialog now widens until its content fits, up to the window width.

## [1.3.1] - 2026-10-16

### Changed

- `SidebarItem` has an optional `ControlName` and a `MatchesSearch` method. The `FloweryComponentSidebar` search matches the translated name and the control class name, ignoring case, spaces and punctuation (#9). In the German Gallery, "Tag-Auswahl", "DaisyTagPicker", "TagPicker" and "tag picker" all find the tag picker.
- Gallery section headers show the translated sidebar name followed by the control class, e.g. `Tag-Auswahl (DaisyTagPicker)`, and follow language changes (#9).

### Fixed

- Localized `DaisyColorPickerDialog` and `DaisyScreenColorPicker`. Their labels, buttons, tooltips and the screen picker hint were hard-coded in English; they now use new `ColorPicker_*` and `Common_Ok` keys in all 12 languages, and the screen picker follows language changes while it is shown.

## [1.3.0] - 2026-10-16

### Added

- Added `Flowery.WinUI.Photos`, a demo app for viewing photos and making basic edits with Flowery controls, Win2D, runtime themes, global sizing and 12 languages.

### Changed

- Aligned the font tokens with Flowery.NET. The primary ladder is now 10/12/14/18/20 (was 8/10/12/14/16), and the secondary, tertiary, section header, header, line height, badge, menu, kbd, divider and DateTimeline font tokens grow with it. `GetDefaultFontSize`, `GetDefaultLineHeight` and the `GetFontSizeForTier` fallbacks use the same values. Badge, kbd, DateTimeline, TextArea and floating input heights follow the Flowery.NET tokens so the larger text fits.
- Added `DaisyCheckbox{Size}FontSize`, `DaisyRadio{Size}FontSize` and `DaisyToggle{Size}FontSize` label tokens (Flowery.NET parity).
- Corrected the checkbox, radio and toggle size tables in `llms-static/DesignTokens.md` to the compact indicator tokens (for example a 12 px checkbox and radio and a 24×12 toggle with an 8 px knob at Medium) and documented the separate `DaisyRadio{Size}Size` tokens.

## [1.2.1] - 2026-10-15

### Fixed

- Fixed `FloweryComponentSidebar` categories that stayed expanded but empty after rapid search changes. Deferred item batches now continue with the newest search results.

## [1.2.0] - 2026-10-14

First native WinUI 3 release. Package versions match the Flowery.Uno 1.2.0 baseline.

- Repository: renamed project namespaces, documentation, resource examples, and repository links to Flowery.WinUI.
- Windows: migrated the nine solution projects to native WinUI 3 and Windows App SDK, including native resources, shadows, and graphics.
- Build: added exact SDK selection and local dotnet-host selection for early .NET 11 SDK RC2 testing. The application target remains .NET 10 for Windows; the CI matrix includes both SDK 10 and SDK 11 RC2.
- Build: all projects now use the shared version metadata from `Directory.Build.props`.
- Effects: preserved 50 carousel effects and 100 transition shaders through a native WinUI 3 WebView2 host.
- Animations: applied attached zoom intensity to PanAndZoom, kept locked zoom at a constant cover scale, and corrected deferred-start and unload/reload handling.
- Glass: corrected native snapshot cropping with blur padding, excluded sibling glass overlays from capture, applied tint once, and refreshed snapshots after appearance, size, rasterization, and load changes.
- Glass: corrected saturation channel weights and centered the Gallery comparison over a shared backdrop.
- Shadows: corrected native shadow demo cleanup and direct helper sizing, mask updates, and resource lifetime.
- Kiosk: preserved all six slides, slide themes, keywords, progress, eight-second autoplay, and manual navigation in the native Windows application.
- Kiosk: kept the Scaling header and progress fixed, made the demo fit the available width, and allowed titles and control rows to wrap. The selected percentage applies once, while controls retain their five size tiers. The loading caption remains readable on the Nord theme.
- Controls: made badge content fit each size tier and explicit dimensions without duplicate borders. Added the missing ExtraLarge badge tokens.
- Documentation: replaced obsolete backend and toolkit guidance with native WinUI 3 and Windows App SDK instructions. The generator reads native dependency property types, defaults, and descriptions.
- Copyright: changed current copyright and Gallery license headers to 2026.

## [Original source history]

The following entries describe the original source before the native WinUI migration.

### Final changes before the native migration

- **Breaking:** Build: moved all Windows, Desktop (Skia), Browser and tooling target frameworks from net9.0 to net10.0 (Android was already net10.0); consumers must now target net10.0. Build/run scripts, CI build, runtime test runners and developer docs updated accordingly.
- Historical Linux target: the original desktop Gallery resumed startup after removal of the incompatible `Tmds.DBus.Protocol` 0.93.0 pin. Its original framework resolved 0.21.3 transitively.
- Tests: the Windows runtime tests no longer fail with path-too-long errors when the repository sits in a deep folder (e.g. a VM shared folder). Both runtime test runners now build the Gallery under `<temp>/frt/<8-char id>/` and log that folder as `Runtime test build root: ...`; the folder is deleted after each run. This is needed because the Windows App SDK XAML compiler (a net472 tool) ignores `LongPathsEnabled`. The results directory also drops a redundant project-name segment.
- Tests: runtime tests now use an in-memory state storage instead of the app's real one. Saved user settings no longer leak into tests (the Kanban drop-index test failed whenever the last saved Kanban view was Home), and test runs no longer add "Untitled Board" files to the Gallery's storage.
- **Visual:** Theming: contrast fixes ported from Flowery.NET 3.2.3 (8132bca) and 3.3.0 (62800f1). New palette keys `DaisyPrimaryForegroundBrush`, `DaisySecondaryForegroundBrush`, `DaisyAccentForegroundBrush` and `DaisyButtonPrimaryForegroundBrush` drive the text of outline, dash, soft and link buttons and of `DaisyIconText` on base surfaces; they equal the fill unless a theme overrides them. Black, Coffee, Cupcake, Dark, Lemonade, Luxury, Pastel, Retro and Wireframe use more readable shades there. Business uses white primary content and an opaque fill for disabled primary buttons. Smooth uses a lighter `Base300`, so borders and separators are visible. Review custom color overrides that depend on the previous values.
- **Breaking:** Visual: `DaisyCheckBox`, `DaisyRadio` and `DaisyToggle` use the compact indicators from Flowery.NET 3.3.0 (62800f1) in every size tier, for example a 12 px checkbox and radio and a 24×12 toggle at Medium (before 20 px and 36×20). `DaisyRadio` gets its own `DaisyRadio{Size}Size` tokens instead of reusing the checkbox sizes. The default `TogglePadding` is 1 instead of 4, so the knob sits at the track edge in every size. Label font sizes stay as they were, since the original implementation already used smaller sizes than the new Flowery.NET ones. Review fixed dimensions and style overrides that depend on the previous sizes.
- Windows: `DaisyTableCell` with string content showed empty cells on the WinUI head, because its default style had no template and WinUI never displayed the string. The style now has an explicit `ContentPresenter` template; cells with element content were not affected.
- Theming: `DaisyThemeManager.TryCreatePalette` and `GetPaletteFactory` expose the factory passed to `RegisterTheme`. `AvailableThemesChanged` fires when `RegisterTheme` adds a new theme name; built-in themes raise it only when `NotifyForInternalThemesChanged` is true (default false). Ported from Flowery.NET 3.2.0 (4c1cc89).
- Theming: `DaisyThemeDropdown` preview swatches now come from the registered palette factory. Themes registered with a custom factory previously showed the Dark palette, because the dropdown created palettes by name and unknown names fall back to Dark. Themes registered after a dropdown was loaded now appear in it.
- Theming: theme lists can be customized, ported from Flowery.NET 3.3.0 (62800f1). `DaisyThemeManager.ExcludedThemes`, `HideTheme`, `ShowTheme` and `IsThemeVisible` hide themes from both dropdowns. `PreferredThemes` and `AddTheme(originalName, displayName, preferred)` put themes into a preferred group at the top, separated by a line, with optional display aliases; `RemoveThemeOverride` undoes an addition. `DaisyThemeDropdown` and `DaisyProductThemeDropdown` gain local `AddTheme`/`RemoveThemeOverride`. Selecting an added product theme registers and applies it. `AvailableThemes` now lists preferred themes first.
- Security: `System.Security.Cryptography.Xml` (Gallery Browser head) bumped from 10.0.7 to 10.0.13, the first 10.0.x release without known vulnerabilities; this removes the `NU1903` build warnings.
- ColorPicker: added `DaisyColorPickerDialog`, ported from Flowery.NET 3.1.1. It combines the color wheel, lightness slider, screen color picker, palette and editor in a `FloweryDialogBase`, with a 50-step Undo/Redo history, and `ShowAsync(XamlRoot, Color)` returns the selected color or `null`. Every component, including hex input and the editor sliders, updates the preview and the returned color. The Gallery's Color Picker Dialog sample now uses it instead of a hand-built modal, and `DaisyColorPickerDialog.md` documents the XamlRoot-based dialog API.

### [1.2.0] - 2026-07-09

- Windows: fixed the Gallery startup crash (0xC000027B) caused by library XBFs missing from the app output; the WinAppSDK PRI merge skips multi-targeted library projects, so the Windows head now copies theme/resource XBFs to the library layout paths after build.
- Kanban: fixed dropping cards onto swimlane cells (lane reassignment within the same column did nothing and drop positions were mapped incorrectly in lane-filtered cells).
- Kanban: card moves now update the board incrementally instead of rebuilding all lane rows and task lists, removing the visible flicker on drag and drop.
- Kanban: bounded lane-cell heights in swimlane layout so task lists virtualize and scroll instead of realizing every card.
- Kanban: made drop-index calculation order-independent under ListView container recycling and simplified the internal visual caches.
- Tests: runtime tests now run with deterministic theme and global size instead of inheriting user-persisted Gallery settings, and no longer overwrite them.
- Tests: fixed stale default-value assertions and added coverage for drop-index calculation and lane-aware moves; full Skia runtime suite passes again.

### [1.1.0] - 2026-07-04

- Added 25 DaisyLoading animations across business workflow and Win95 retro variants.
- Added the new DaisyLoading variants to the Gallery feedback examples.
- Improved small-size readability for Win95 recycle-bin document animations.
- Ported theme fixes for neutral outline buttons, focused radios, and selected Gallery sidebar items.

### [1.0.0] - 2026-05-10

- Bumped version to 1.0.0 as there's no reason not to. :)
- Fixed Daisy control public styling behavior for `DaisyRadio`, `DaisyCheckBox`, `DaisyToggle`, and `DaisyTagPicker`.
- Public `BorderBrush` and `BorderThickness` now apply to each whole control border while internal themed indicators and tag panels keep their own borders.
- Public text styling such as `Foreground`, `FontSize`, `FontFamily`, `FontStyle`, and `FontWeight` now flows to visible labels, headers, and tag text.
- Added runtime coverage for explicit public border and font styling on the updated Daisy controls.

### [0.1.3] - 2026-05-07

- Updated Flowery shared package version metadata to `0.1.3`.
- Updated the original framework SDK and its package pins to the 6.5 line across the shared projects.
- Updated central package versions for SkiaSharp, the original UI toolkit, MSTest, Octokit, Win2D, WebAssembly crypto support, Tmds.DBus.Protocol, and Microsoft Testing extensions.
- Kept `Microsoft.Graphics.Win2D` on `1.3.2` because `1.4.0` introduced a mixed Windows App SDK 1.7/1.8 build graph with the original framework SDK.
- Updated Gallery desktop/browser package references required by the refreshed package graph.
- Updated release artifact actions for Node.js 24 and pinned Windows CI jobs to `windows-2025-vs2026`.
- Historical mobile packages: normalized library-layout paths so `dotnet pack` emitted matching library and dependency groups.

### [0.1.2] - 2026-02-12

- Refined size design tokens around a 4px grid and introduced mobile-specific sizing via `PlatformCompatibility.IsMobile`, keeping desktop/Windows ergonomics with the existing size presets.
- Increased mobile touch-target defaults for interactive controls and aligned global default size to `DaisySize.Medium`.
- Added line-height size tokens and applied token-driven text vertical alignment improvements in `DaisyButton`, `DaisyInput`, and `DaisyNumericUpDown` (including Android-specific centering adjustments).
- Updated `DaisyToggle` sizing behavior to enforce mobile-friendly minimum tap targets while preserving compact desktop rendering.
- Fixed browser build scripts to force browser-only framework evaluation and suppress EOL-check noise on preview SDKs (`build_browser.ps1`, `build_core.ps1`, `run-browser.ps1`).
- Historical mobile targets: migrated the original projects and their build/run scripts from .NET 9 to .NET 10.

### [0.1.1] - 2026-02-07

- Centralized versioning in `Directory.Build.props`.
- Improved Gallery startup responsiveness by prioritizing initial Home rendering and deferring restore navigation to heavy pages.
- Reduced Gallery startup overhead by deferring Kanban-specific initialization and resource loading until first Kanban usage.
- Library: mitigated repeated first-chance `NotImplementedException` churn for unsupported stroke APIs by caching runtime support checks.
- Common versions across projects updated.

### [0.1.0] - 2026-02-03

Initial port to the original cross-platform framework (alpha).
