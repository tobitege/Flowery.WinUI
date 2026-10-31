using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Flowery.Controls;
using Flowery.WinUI.Gallery.Examples;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.UnitTests.Controls
{
    /// <summary>
    /// Stands in for the XAML compiler's trimming check WMC1510, which Flowery.WinUI, Flowery.WinUI.Kanban and the
    /// Gallery pages suppress: the XBF generator of this Windows App SDK rejects both remedies the warning names
    /// (x:SuppressXamlTrimWarnings, and x:DataType in a dictionary without code-behind). A {Binding} must reach its source
    /// without reflection, so a NativeAOT app resolves it: the generated property provider of a localization
    /// ({Binding [Key]}); item types along the whole path that are [Bindable] (and not nested, which the generated XAML
    /// metadata leaves out) or have the generated provider; and the generated provider on every Flowery control that a
    /// binding reads by property name (TemplatedParent, ElementName), whose own XAML metadata covers only the members the
    /// compiler saw in markup.
    /// </summary>
    [TestClass]
    public class Given_LibraryXamlBindings
    {
        private static readonly XName XamlKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        private static readonly XName XamlName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        private static readonly string[] ScannedFolders = ["Flowery.WinUI/Themes", "Flowery.WinUI.Kanban/Themes", "Flowery.WinUI.Gallery.Core/Examples"];

        // Item templates that bind to their items, by the x:Key of the template or else by the collection or content
        // their owner binds to (or the owner's x:Name when code sets the items), with the item type.
        private static readonly Dictionary<string, Type> ItemTypes = new(StringComparer.Ordinal)
        {
            ["DaisyThemeDropdownItemTemplate"] = typeof(ThemePreviewInfo),
            ["FilteredBoards"] = typeof(FlowBoardMetadata),
            ["Tag.FilterPresets"] = typeof(FlowKanbanFilterPreset),
            ["StandardColumnsSource"] = typeof(FlowKanbanColumnData),
            ["SwimlaneColumnsSource"] = typeof(FlowKanbanColumnData),
            ["CompactSelectedColumn"] = typeof(FlowKanbanColumnData),
            ["TaskViews"] = typeof(FlowKanbanTaskView),
            ["LaneRows"] = typeof(FlowKanbanLaneRowView),
            ["Cells"] = typeof(FlowKanbanLaneCellView),
            ["MultiDemoGrid"] = typeof(CarouselDemoItem),
            ["BreadcrumbBarIconItems"] = typeof(NavigationExamples.BreadcrumbBarIconItem)
        };

        private static readonly Assembly[] FloweryAssemblies = [typeof(DaisyButton).Assembly, typeof(FlowKanban).Assembly, typeof(CarouselDemoItem).Assembly];

        // The localizations behind {Binding [Key]}.
        private static readonly Type[] Localizations = [typeof(Flowery.Localization.FloweryLocalization), typeof(Flowery.WinUI.Gallery.Localization.GalleryLocalization)];

        [TestMethod]
        public void When_ATemplateOrPageBinds_ItsSourceNeedsNoReflection()
        {
            var root = FindRepositoryRoot();
            List<string> problems = [];
            HashSet<string> usedKeys = [];
            var bindings = 0;
            foreach (var folder in ScannedFolders)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.xaml"))
                {
                    var document = XDocument.Load(file, LoadOptions.SetLineInfo);
                    foreach (var attribute in document.Descendants().Attributes())
                    {
                        if (BindingPath(attribute.Value) is not { } path)
                            continue;

                        bindings++;
                        if (path.StartsWith('[') || ReadsDependencyProperty(attribute.Value))
                            continue;

                        var problem = Check(attribute, path, usedKeys);
                        if (problem is not null)
                            problems.Add($"{Path.GetFileName(file)}({((IXmlLineInfo)attribute).LineNumber}): {attribute.Value} - {problem}");
                    }
                }
            }

            Assert.IsGreaterThan(400, bindings, "The scan found too few {Binding} uses.");
            Assert.IsEmpty(problems,
                "These bindings read a plain CLR data context. Bind to a dependency property (TemplatedParent, ElementName, "
                + "a resource), or list the item template with its item type and make every type on the path [Bindable]:\n"
                + string.Join("\n", problems));
            CollectionAssert.AreEquivalent(ItemTypes.Keys.ToArray(), usedKeys.ToArray(),
                "Every listed item template must still exist and bind to its items.");
        }

        [TestMethod]
        public void When_TextsBindToALocalization_ItHasTheGeneratedPropertyProvider()
        {
            // Read from metadata only: the attributes are WinRT types, which this test process does not activate.
            foreach (var type in Localizations)
            {
                Assert.IsTrue(type.CustomAttributes.Any(static attribute => attribute.AttributeType.FullName == "WinRT.GeneratedBindableCustomPropertyAttribute"),
                    $"{type.Name} needs [WinRT.GeneratedBindableCustomProperty], or a NativeAOT app shows no text for {{Binding [Key]}}.");
            }
        }

        [TestMethod]
        public void When_ATemplateReadsAFloweryControlByPropertyName_TheControlHasTheGeneratedPropertyProvider()
        {
            // WinUI resolves such a property through the generated XAML metadata only when the XAML compiler saw it in
            // markup; otherwise it asks the control for ICustomPropertyProvider, which a NativeAOT app has only from the
            // attribute. Without it the binding throws NotSupportedException, which WinUI turns into E_FAIL in layout.
            var root = FindRepositoryRoot();
            List<string> problems = [];
            var checkedBindings = 0;
            foreach (var folder in ScannedFolders)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.xaml"))
                {
                    var document = XDocument.Load(file, LoadOptions.SetLineInfo);
                    var names = document.Descendants().Where(static element => element.Attribute(XamlName) is not null)
                        .GroupBy(static element => element.Attribute(XamlName)!.Value).ToDictionary(static group => group.Key, static group => group.First());
                    foreach (var attribute in document.Descendants().Attributes())
                    {
                        if (BindingPath(attribute.Value) is not { Length: > 0 } path || path.StartsWith('(') || path.StartsWith('['))
                            continue;
                        var line = $"{Path.GetFileName(file)}({((IXmlLineInfo)attribute).LineNumber}): {attribute.Value}";
                        var type = SourceType(attribute, names, out var sourceProblem);
                        if (sourceProblem is not null)
                            problems.Add($"{line} - {sourceProblem}");
                        if (type is null)
                            continue;

                        checkedBindings++;
                        var segments = path.Split('.');
                        var name = segments[0].Split('[')[0];
                        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                        if (property is null)
                            problems.Add($"{line} - {type.Name} has no property {name}");
                        else if (IsFloweryType(property.DeclaringType!) && !HasGeneratedPropertyProvider(type))
                            problems.Add($"{line} - {type.Name} needs [WinRT.GeneratedBindableCustomProperty]");
                        else if (segments[0] == name && CheckPath(property.PropertyType, segments.Skip(1)) is { } problem)
                            problems.Add($"{line} - {problem}");
                    }
                }
            }

            Assert.IsGreaterThan(100, checkedBindings, "The scan found too few bindings to a Flowery control.");
            Assert.IsEmpty(problems, "These bindings read a Flowery control by property name:\n" + string.Join("\n", problems));
        }

        // The Flowery type a {Binding} with RelativeSource TemplatedParent or ElementName reads; null for anything else.
        private static Type? SourceType(XAttribute attribute, Dictionary<string, XElement> names, out string? problem)
        {
            problem = null;
            var value = attribute.Value;
            XElement? source = null;
            XName? typeName = null;
            if (value.Contains("TemplatedParent", StringComparison.Ordinal))
            {
                // TemplatedParent is the owner of the nearest template of any kind; only a ControlTemplate names its type.
                source = attribute.Parent!.Ancestors().FirstOrDefault(static element => element.Name.LocalName is "ControlTemplate" or "DataTemplate" or "ItemsPanelTemplate");
                if (source is not null && source.Name.LocalName != "ControlTemplate")
                {
                    problem = $"TemplatedParent in this {source.Name.LocalName} is not the control; set the value from the control in code";
                    return null;
                }
                typeName = source?.Attribute("TargetType")?.Value is { } target ? ResolveTypeName(source, target) : null;
            }
            else if (Regex.Match(value, @"ElementName=(\w+)") is { Success: true } match
                && names.TryGetValue(match.Groups[1].Value, out source))
            {
                typeName = source.Name;
            }

            if (typeName is null || !typeName.NamespaceName.StartsWith("using:Flowery", StringComparison.Ordinal))
                return null;
            var clrName = typeName.NamespaceName["using:".Length..] + "." + typeName.LocalName;
            foreach (var assembly in FloweryAssemblies)
            {
                if (assembly.GetType(clrName) is { } type)
                    return type;
            }

            return null;
        }

        private static XName? ResolveTypeName(XElement scope, string prefixedName)
        {
            var parts = prefixedName.Split(':');
            if (parts.Length != 2)
                return null;
            return scope.GetNamespaceOfPrefix(parts[0]) is { } ns ? ns + parts[1] : null;
        }

        private static bool IsFloweryType(Type type) => FloweryAssemblies.Contains(type.Assembly);

        private static bool HasGeneratedPropertyProvider(Type type) =>
            type.CustomAttributes.Any(static attribute => attribute.AttributeType.FullName == "WinRT.GeneratedBindableCustomPropertyAttribute");

        // Follows the binding up to the item template whose items it reads, then its path through the item type.
        private static string? Check(XAttribute attribute, string path, HashSet<string> usedKeys)
        {
            var segments = new List<string>(path.Split('.', StringSplitOptions.RemoveEmptyEntries));
            for (var element = attribute.Parent; element is not null; element = element.Parent)
            {
                if (element != attribute.Parent && element.Attribute("DataContext") is { } context && BindingPath(context.Value) is { } contextPath)
                {
                    // A data context from a dependency property is the object behind it, such as the localization.
                    if (ReadsDependencyProperty(context.Value))
                        return null;
                    segments.InsertRange(0, contextPath.Split('.', StringSplitOptions.RemoveEmptyEntries));
                }

                if (TemplateKey(element) is not { } key)
                    continue;

                if (!ItemTypes.TryGetValue(key, out var itemType))
                    return $"the item template of '{key}' is not listed with its item type";

                usedKeys.Add(key);
                return CheckPath(itemType, segments);
            }

            return "it reads the data context outside an item template";
        }

        // Every type whose property the path reads must be bindable (see IsBindable). The walk ends at object, whose type
        // only the running app knows.
        private static string? CheckPath(Type type, IEnumerable<string> segments)
        {
            foreach (var segment in segments)
            {
                if (type == typeof(object))
                    return null;
                if (!IsBindable(type))
                    return $"{type.Name} needs [Microsoft.UI.Xaml.Data.Bindable] (on a type that is not nested) or [WinRT.GeneratedBindableCustomProperty]";
                var property = type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                if (property is null)
                    return $"{type.Name} has no property {segment}";
                type = property.PropertyType;
            }

            return null;
        }

        // The generated XAML metadata covers the WinUI types and the [Bindable] types that are not nested; any other type
        // of Flowery needs the generated property provider.
        private static bool IsBindable(Type type)
        {
            if (HasGeneratedPropertyProvider(type))
                return true;
            if (IsFloweryType(type))
                return !type.IsNested && type.CustomAttributes.Any(static attribute => attribute.AttributeType.FullName == "Microsoft.UI.Xaml.Data.BindableAttribute");
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (current.FullName == "Microsoft.UI.Xaml.DependencyObject")
                    return true;
            }

            return false;
        }

        // The key of an item template: its x:Key, or what its owner binds as items or content, or the owner's x:Name.
        // An item container style reads the items of its owner the same way.
        private static string? TemplateKey(XElement element)
        {
            var isTemplate = element.Name.LocalName == "DataTemplate";
            var isContainerStyle = element.Name.LocalName == "Style" && element.Parent?.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal) == true;
            if (!isTemplate && !isContainerStyle)
                return null;
            if (element.Attribute(XamlKey)?.Value is { } key)
                return key;

            var owner = element.Parent is { } property && property.Name.LocalName.Contains('.') ? property.Parent : element.Parent;
            if (owner is null)
                return "(no owner)";
            var source = owner.Attribute("ItemsSource") ?? owner.Attribute("Content");
            return (source is null ? null : BindingPath(source.Value, allowCompiled: true)) ?? owner.Attribute(XamlName)?.Value ?? owner.Name.LocalName;
        }

        // The path of a {Binding} (or of an {x:Bind} when allowed); null for any other value.
        private static string? BindingPath(string value, bool allowCompiled = false)
        {
            value = value.Trim();
            var prefix = value.StartsWith("{Binding", StringComparison.Ordinal) ? "{Binding"
                : allowCompiled && value.StartsWith("{x:Bind", StringComparison.Ordinal) ? "{x:Bind" : null;
            if (prefix is null)
                return null;

            var body = value[prefix.Length..^1].Trim();
            var path = body.Split(',')[0].Trim();
            if (path.StartsWith("Path=", StringComparison.Ordinal))
                path = path["Path=".Length..].Trim();
            return path.Contains('=') ? string.Empty : path;
        }

        // These bindings read a dependency object rather than the data context; the control-by-name test checks them.
        private static bool ReadsDependencyProperty(string binding) =>
            binding.Contains("RelativeSource=", StringComparison.Ordinal)
            || binding.Contains("ElementName=", StringComparison.Ordinal)
            || binding.Contains("Source={StaticResource", StringComparison.Ordinal)
            || binding.Contains("Source={ThemeResource", StringComparison.Ordinal);

        private static string FindRepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Flowery.WinUI.sln")))
                    return directory.FullName;
            }

            throw new DirectoryNotFoundException("Flowery.WinUI.sln not found above " + AppContext.BaseDirectory);
        }
    }
}
