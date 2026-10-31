using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.WindowsRunner
{
    [TestClass]
    public class Given_RuntimeTestsRunner
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public async Task When_Running_WindowsRuntimeTests_Succeeds()
        {
            var executable = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TESTS_HOST_PATH");
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            {
                Assert.Fail("Set FLOWERY_RUNTIME_TESTS_HOST_PATH to the prebuilt Flowery.WinUI.RuntimeTests.Host executable. Use scripts/test_windows.ps1.");
            }

            var runDirectory = Environment.GetEnvironmentVariable("FLOWERY_TEST_RESULTS_ROOT");
            if (string.IsNullOrWhiteSpace(runDirectory)
                || !Path.IsPathFullyQualified(runDirectory)
                || !Directory.Exists(runDirectory))
            {
                Assert.Fail("Set FLOWERY_TEST_RESULTS_ROOT to an existing absolute report directory owned by scripts/test_windows.ps1.");
            }

            // MSTest removes TestContext.TestRunDirectory before TRX copies attached files.
            // Keep the native report in the script-owned directory through session completion.
            var resultDirectory = Path.Combine(runDirectory!, "native-runtime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(resultDirectory);
            var resultPath = Path.Combine(resultDirectory, "runtime-tests-windows.xml");

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.GetFullPath(executable!),
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable!))!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--runtime-tests");
            startInfo.ArgumentList.Add(resultPath);
            startInfo.Environment["FLOWERY_RUNTIME_TESTS_PATH"] = resultPath;

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // Only terminate the process tree created above, then wait for its handles to close.
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                await process.WaitForExitAsync();
                throw new TimeoutException("Native WinUI runtime tests did not finish within eight minutes.");
            }
            finally
            {
                var output = await outputTask;
                var error = await errorTask;
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine(output);
                }
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine(error);
                }
                Console.WriteLine($"Native runtime report directory: {resultDirectory}");
            }

            Assert.IsTrue(File.Exists(resultPath), $"Runtime tests produced no report. Exit code: {process.ExitCode}.");
            TestContext?.AddResultFile(resultPath);
            var report = XDocument.Load(resultPath);
            var root = report.Root ?? throw new InvalidOperationException("The runtime report has no root element.");
            var tests = root.Elements("test").ToList();
            var total = (int?)root.Attribute("total") ?? -1;
            var passed = (int?)root.Attribute("passed") ?? -1;
            var failed = (int?)root.Attribute("failed") ?? -1;
            var inconclusive = (int?)root.Attribute("inconclusive") ?? -1;

            foreach (var test in tests.Where(test => (string?)test.Attribute("outcome") != "Passed"))
            {
                Console.WriteLine($"{test.Attribute("name")?.Value}: {test.Element("message")?.Value}");
            }

            Assert.IsTrue(total > 0, "The native runtime report contains no tests.");
            Assert.AreEqual(total, tests.Count, "The runtime report count does not match its test entries.");
            Assert.AreEqual(total, passed + failed + inconclusive, "The runtime report outcome counts are inconsistent.");
            Assert.AreEqual(total, tests.Select(test => (string?)test.Attribute("name")).Distinct().Count(),
                "The runtime report contains duplicate test names.");
            Assert.AreEqual(0, failed, $"{failed} native runtime tests failed. See the attached XML report.");
            // Inconclusive tests could not set up their scenario in this environment; scripts/test_windows.ps1 reports them as warnings.
            Assert.AreEqual(passed, tests.Count(test => (string?)test.Attribute("outcome") == "Passed"));
            Assert.AreEqual(inconclusive, tests.Count(test => (string?)test.Attribute("outcome") == "Inconclusive"));
            Assert.AreEqual(0, process.ExitCode, "The runtime test host returned a nonzero exit code.");
        }
    }
}
