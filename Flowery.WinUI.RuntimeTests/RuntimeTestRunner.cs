using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Flowery.Controls;
using Flowery.Services;
using Flowery.WinUI.Kanban.Controls;
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
                FlowKanban.DefaultUserProvider = null;

                var host = PrepareHost(window);
                RuntimeTestContext.Initialize(window, host);
                await EnsureLoadedAsync(host);
            }).ConfigureAwait(false);

            var testCases = DiscoverTests();
            using var progress = new StreamWriter(System.IO.Path.ChangeExtension(resultPath, ".progress.log")) { AutoFlush = true };
            foreach (var testCase in testCases)
            {
                progress.WriteLine($"START {testCase.Name}");
                var result = await ExecuteTestCaseAsync(dispatcher, testCase).ConfigureAwait(false);
                results.Add(result);
                progress.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} {testCase.Name}");
                if (result.Message is not null) progress.WriteLine(result.Message);
            }

            WriteResults(resultPath, results);
            return results.Any(result => !result.Passed) ? 1 : 0;
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
                return RuntimeTestCaseResult.Failed(testCase.Name, ex);
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

        private static void WriteResults(string resultPath, IReadOnlyCollection<RuntimeTestCaseResult> results)
        {
            var document = new XDocument(
                new XElement("tests",
                    new XAttribute("total", results.Count),
                    new XAttribute("passed", results.Count(result => result.Passed)),
                    new XAttribute("failed", results.Count(result => !result.Passed)),
                    results.Select(result =>
                        new XElement("test",
                            new XAttribute("name", result.Name),
                            new XAttribute("outcome", result.Passed ? "Passed" : "Failed"),
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

        private sealed class RuntimeTestCaseResult
        {
            private RuntimeTestCaseResult(string name, bool passed, string? message)
            {
                Name = name;
                Passed = passed;
                Message = message;
            }

            public string Name { get; }
            public bool Passed { get; }
            public string? Message { get; }

            public static RuntimeTestCaseResult Success(string name) => new RuntimeTestCaseResult(name, true, null);

            public static RuntimeTestCaseResult Failed(string name, Exception exception)
            {
                return new RuntimeTestCaseResult(name, false, exception.ToString());
            }
        }
    }
}
