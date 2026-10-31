using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Flowery.Controls;
using Flowery.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests
{
    public static class RuntimeTestRunner
    {
        public static async Task<int> RunAsync(Window window, string resultPath)
        {
            var dispatcher = window.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            var results = new List<RuntimeTestCaseResult>();

            await RunOnDispatcherAsync(dispatcher, async () =>
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
                System.Globalization.CultureInfo.CurrentCulture = culture;
                System.Globalization.CultureInfo.CurrentUICulture = culture;
                Flowery.Localization.FloweryLocalization.SetCulture("en-US");
                // Pin the global size so default-value assertions don't depend on
                // whatever size the hosting app (or a previous user session) applied.
                FlowerySizeManager.Reset();

                // Rendering assertions use normal motion regardless of the host's Windows preference.
                // Reduced-motion tests switch this in-memory policy explicitly and restore it.
                var appearance = typeof(DaisyButton).Assembly.GetType("Flowery.Services.DaisySystemAppearance", true)!;
                appearance.GetMethod("SetMotionPreference", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { true });

                // Keep tests away from the app's real state storage: saved settings (e.g. the
                // Kanban's last view) must not leak into tests, and tests must not write user data.
                StateStorageProvider.Configure(new InMemoryStateStorage());

                // The test launch skips persisted Gallery settings, so initialize
                // the shared palette before controls or contrast changes use it.
                Assert.IsTrue(DaisyThemeManager.ApplyTheme(DaisyThemeManager.BaseThemeName));

                var host = PrepareHost(window);
                RuntimeTestContext.Initialize(window, host);
                await EnsureLoadedAsync(host);
            }).ConfigureAwait(false);

            var filter = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TEST_FILTER");
            var testCases = DiscoverTests()
                .Where(test => string.IsNullOrWhiteSpace(filter) || test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (testCases.Length == 0)
                throw new InvalidOperationException($"No native runtime tests match '{filter}'.");
            using var progress = new StreamWriter(System.IO.Path.ChangeExtension(resultPath, ".progress.log")) { AutoFlush = true };
            foreach (var testCase in testCases)
            {
                progress.WriteLine($"START {testCase.Name}");
                var result = await ExecuteTestCaseAsync(dispatcher, testCase).ConfigureAwait(false);
                results.Add(result);
                progress.WriteLine($"{result.Outcome switch { RuntimeTestOutcome.Passed => "PASS", RuntimeTestOutcome.Inconclusive => "WARN", _ => "FAIL" }} {testCase.Name}");
                if (result.Message is not null) progress.WriteLine(result.Message);
            }

            WriteResults(resultPath, results);
            return results.Any(result => result.Outcome == RuntimeTestOutcome.Failed) ? 1 : 0;
        }

        private static Grid PrepareHost(Window window)
        {
            var host = new Grid();
            window.Content = host;
            return host;
        }

        private static IEnumerable<RuntimeTestCase> DiscoverTests()
        {
            var assembly = typeof(RuntimeTestRunner).Assembly;
            var testClasses = assembly.GetTypes()
                .Where(type => type.GetCustomAttribute<TestClassAttribute>() != null);

            foreach (var testClass in testClasses)
            {
                var methods = testClass.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => method.GetCustomAttribute<TestMethodAttribute>() != null)
                    .Where(method => method.GetParameters().Length == 0);

                foreach (var method in methods)
                {
                    yield return new RuntimeTestCase(testClass, method);
                }
            }
        }

        private static async Task<RuntimeTestCaseResult> ExecuteTestCaseAsync(DispatcherQueue dispatcher, RuntimeTestCase testCase)
        {
            try
            {
                await RunOnDispatcherAsync(dispatcher, async () =>
                {
                    var instance = Activator.CreateInstance(testCase.TestClassType)
                                   ?? throw new InvalidOperationException($"Failed to create {testCase.TestClassType.FullName}.");

                    var result = testCase.Method.Invoke(instance, null);
                    if (result is Task task)
                    {
                        await task.ConfigureAwait(true);
                    }
                }).ConfigureAwait(false);

                return RuntimeTestCaseResult.Success(testCase.Name);
            }
            catch (Exception ex)
            {
                // An inconclusive test could not set up its scenario in this environment; it warns instead of failing.
                var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                return cause is AssertInconclusiveException
                    ? RuntimeTestCaseResult.Inconclusive(testCase.Name, cause)
                    : RuntimeTestCaseResult.Failed(testCase.Name, ex);
            }
        }

        private static async Task RunOnDispatcherAsync(DispatcherQueue dispatcher, Func<Task> action)
        {
            if (dispatcher.HasThreadAccess)
            {
                await action().ConfigureAwait(true);
                return;
            }

            var tcs = new TaskCompletionSource<object?>();
            if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await action().ConfigureAwait(true);
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                throw new InvalidOperationException("The native WinUI dispatcher rejected the runtime test action.");
            }

            await tcs.Task.ConfigureAwait(false);
        }

        private static Task EnsureLoadedAsync(FrameworkElement element)
        {
            if (element.IsLoaded)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>();
            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                element.Loaded -= handler;
                tcs.TrySetResult(null);
            };
            element.Loaded += handler;
            return tcs.Task;
        }

        /// <summary>
        /// Writes a report with one failed test for a host that could not start or finish the tests: same format as a test run, so
        /// the runner shows the cause instead of failing on the counts.
        /// </summary>
        public static void WriteHostFailure(string resultPath, Exception exception)
        {
            try
            {
                WriteResults(resultPath, [RuntimeTestCaseResult.Failed("RuntimeTestsHost.Failure", exception)]);
            }
            catch (Exception writeError)
            {
                // The host's exit code still signals the failure.
                System.Diagnostics.Debug.WriteLine(writeError);
            }
        }

        private static void WriteResults(string resultPath, IReadOnlyCollection<RuntimeTestCaseResult> results)
        {
            var document = new XDocument(
                new XElement("tests",
                    new XAttribute("total", results.Count),
                    new XAttribute("passed", results.Count(result => result.Outcome == RuntimeTestOutcome.Passed)),
                    new XAttribute("failed", results.Count(result => result.Outcome == RuntimeTestOutcome.Failed)),
                    new XAttribute("inconclusive", results.Count(result => result.Outcome == RuntimeTestOutcome.Inconclusive)),
                    results.Select(result =>
                        new XElement("test",
                            new XAttribute("name", result.Name),
                            new XAttribute("outcome", result.Outcome.ToString()),
                            result.Message is null ? null : new XElement("message", result.Message)
                        ))
                )
            );

            document.Save(resultPath);
        }

        private sealed class RuntimeTestCase
        {
            public RuntimeTestCase(Type testClassType, MethodInfo method)
            {
                TestClassType = testClassType;
                Method = method;
            }

            public Type TestClassType { get; }
            public MethodInfo Method { get; }

            public string Name => $"{TestClassType.FullName}.{Method.Name}";
        }

        private enum RuntimeTestOutcome { Passed, Failed, Inconclusive }

        private sealed class RuntimeTestCaseResult
        {
            private RuntimeTestCaseResult(string name, RuntimeTestOutcome outcome, string? message)
            {
                Name = name;
                Outcome = outcome;
                Message = message;
            }

            public string Name { get; }
            public RuntimeTestOutcome Outcome { get; }
            public string? Message { get; }

            public static RuntimeTestCaseResult Success(string name) => new RuntimeTestCaseResult(name, RuntimeTestOutcome.Passed, null);

            public static RuntimeTestCaseResult Failed(string name, Exception exception)
            {
                return new RuntimeTestCaseResult(name, RuntimeTestOutcome.Failed, exception.ToString());
            }

            // The reason alone; the stack of an inconclusive assertion carries no information.
            public static RuntimeTestCaseResult Inconclusive(string name, Exception exception)
            {
                return new RuntimeTestCaseResult(name, RuntimeTestOutcome.Inconclusive, exception.Message);
            }
        }
    }
}
