using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public sealed class Given_RuntimeTestReport
{
    [TestMethod]
    public void When_TheHostCannotRunTheTests_ItsReportPassesTheRunnerChecksAndNamesTheCause()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"runtime-report-{System.Guid.NewGuid():N}.xml");
        try
        {
            RuntimeTestRunner.WriteStartupFailure(path, new System.InvalidOperationException("No window"));

            // The checks of Given_RuntimeTestsRunner: every count is present and they add up to the test entries.
            var root = XDocument.Load(path).Root!;
            var tests = root.Elements("test").ToList();
            var counts = new[] { "total", "passed", "failed", "inconclusive" }.Select(name => (int?)root.Attribute(name)).ToArray();
            CollectionAssert.AreEqual(new int?[] { 1, 0, 1, 0 }, counts);
            Assert.HasCount(1, tests);
            Assert.AreEqual("Failed", (string?)tests[0].Attribute("outcome"));
            StringAssert.Contains((string?)tests[0].Element("message"), "No window");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
