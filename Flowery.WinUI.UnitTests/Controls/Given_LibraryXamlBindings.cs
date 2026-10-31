using System.Xml;
using System.Xml.Linq;
using Flowery.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.UnitTests.Controls
{
    /// <summary>
    /// Stands in for the XAML compiler's trimming check WMC1510, which Flowery.WinUI suppresses: the XBF generator of
    /// this Windows App SDK rejects both remedies the warning names (x:SuppressXamlTrimWarnings, and x:DataType in a
    /// dictionary without code-behind). A {Binding} in the library templates must reach its source without reflection,
    /// so a NativeAOT app resolves it.
    /// </summary>
    [TestClass]
    public class Given_LibraryXamlBindings
    {
        private static readonly XName XamlKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");

        // Item templates whose bindings read the data context, with the item type, which must be [Bindable].
        private static readonly Dictionary<string, Type> DataContextTemplates = new(StringComparer.Ordinal)
        {
            ["DaisyThemeDropdownItemTemplate"] = typeof(ThemePreviewInfo)
        };

        [TestMethod]
        public void When_ALibraryTemplateBinds_ItsSourceNeedsNoReflection()
        {
            var themes = Path.Combine(FindRepositoryRoot(), "Flowery.WinUI", "Themes");
            List<string> problems = [];
            HashSet<string> usedTemplates = [];
            var bindings = 0;
            foreach (var file in Directory.EnumerateFiles(themes, "*.xaml"))
            {
                var document = XDocument.Load(file, LoadOptions.SetLineInfo);
                foreach (var attribute in document.Descendants().Attributes())
                {
                    var value = attribute.Value.TrimStart();
                    if (!value.StartsWith("{Binding", StringComparison.Ordinal))
                        continue;

                    bindings++;
                    if (ReadsDependencyProperty(value))
                        continue;

                    var templateKey = attribute.Parent!.AncestorsAndSelf()
                        .FirstOrDefault(static element => element.Name.LocalName == "DataTemplate")?.Attribute(XamlKey)?.Value;
                    if (templateKey is not null && DataContextTemplates.ContainsKey(templateKey))
                    {
                        usedTemplates.Add(templateKey);
                        continue;
                    }

                    problems.Add($"{Path.GetFileName(file)}({((IXmlLineInfo)attribute).LineNumber}): {value}");
                }
            }

            Assert.IsTrue(bindings > 0, "The scan found no {Binding} in " + themes);
            Assert.AreEqual(0, problems.Count,
                "These bindings read a plain CLR data context. Bind to a dependency property (TemplatedParent, ElementName, "
                + "a resource) or list the item template with its [Bindable] item type:\n" + string.Join("\n", problems));
            CollectionAssert.AreEquivalent(DataContextTemplates.Keys.ToArray(), usedTemplates.ToArray(),
                "Every listed item template must still exist and bind to its data context.");
        }

        [TestMethod]
        public void When_AnItemTemplateReadsItsDataContext_TheItemTypeIsBindable()
        {
            // Read from metadata only: the attribute is a WinUI type, which this test process does not activate.
            foreach (var (key, type) in DataContextTemplates)
            {
                Assert.IsTrue(type.CustomAttributes.Any(static attribute => attribute.AttributeType.FullName == "Microsoft.UI.Xaml.Data.BindableAttribute"),
                    $"{type.Name}, the item type of {key}, needs [Microsoft.UI.Xaml.Data.Bindable].");
            }
        }

        // These sources are dependency objects, which WinUI reads through the generated XAML metadata.
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
