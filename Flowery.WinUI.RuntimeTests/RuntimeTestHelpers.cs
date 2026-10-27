using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Flowery.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests
{
    internal static class RuntimeTestHelpers
    {
        // WinUI sets IsLoaded when an element enters the live tree and raises Loaded later. Attached elements
        // keep a task for that event, so a test does not continue before the Loaded handlers ran.
        private static readonly ConditionalWeakTable<FrameworkElement, Task> PendingLoads = new();

        public static Panel GetHostPanel()
        {
            if (RuntimeTestContext.HostPanel is Panel hostPanel)
            {
                return hostPanel;
            }

            Assert.Inconclusive("Runtime test host panel is not initialized.");
            return new Grid();
        }

        public static void AttachToHost(FrameworkElement element)
        {
            var hostPanel = GetHostPanel();
            hostPanel.Children.Clear();
            if (!element.IsLoaded)
            {
                PendingLoads.AddOrUpdate(element, WaitForLoadedEvent(element));
            }
            hostPanel.Children.Add(element);
            element.UpdateLayout();
        }

        /// <summary>Completes after the Loaded event of <paramref name="element"/> has been raised: its own event
        /// while it is not loaded, otherwise the event of the element it was attached with.</summary>
        /// <remarks>A loaded element that was neither attached through <see cref="AttachToHost"/> nor is inside
        /// such an element completes at once.</remarks>
        public static Task EnsureLoadedAsync(FrameworkElement element)
        {
            if (!element.IsLoaded)
            {
                return PendingLoads.TryGetValue(element, out var own) && !own.IsCompleted ? own : WaitForLoadedEvent(element);
            }

            for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement attached && PendingLoads.TryGetValue(attached, out var pending))
                {
                    return pending;
                }
            }

            return Task.CompletedTask;
        }

        // The continuation runs after WinUI has raised the Loaded events of the whole subtree.
        private static Task WaitForLoadedEvent(FrameworkElement element)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                element.Loaded -= handler;
                tcs.TrySetResult(null);
            };
            element.Loaded += handler;
            return tcs.Task;
        }

        /// <summary>Removes <paramref name="element"/> from the host panel and completes after its Unloaded event.</summary>
        public static async Task DetachFromHostAsync(FrameworkElement element)
        {
            var hostPanel = GetHostPanel();
            if (!hostPanel.Children.Contains(element))
                return;

            var unloaded = WaitForUnloadedAsync(element);
            hostPanel.Children.Remove(element);
            await unloaded.WaitAsync(LayoutTimeout);
        }

        /// <summary>Completes after the next Unloaded event of <paramref name="element"/>.</summary>
        public static Task WaitForUnloadedAsync(FrameworkElement element)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                element.Unloaded -= handler;
                tcs.TrySetResult(null);
            };
            element.Unloaded += handler;
            return tcs.Task;
        }

        /// <summary>Runs <paramref name="body"/> with an empty state storage of its own and restores the runner's
        /// storage afterwards. Detach the elements the body showed inside it, so their Unloaded handlers do not write
        /// into the runner's storage.</summary>
        public static async Task WithStateStorageAsync(Func<Task> body)
        {
            var previous = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            try
            {
                await body();
            }
            finally
            {
                StateStorageProvider.Configure(previous);
            }
        }

        /// <summary>The longest one case of a table-driven test may run.</summary>
        public static readonly TimeSpan CaseTimeout = TimeSpan.FromSeconds(30);

        /// <summary>Runs one case of a table-driven test. A failure, an exception or a timeout names the case;
        /// an inconclusive result passes through unchanged.</summary>
        public static async Task RunCaseAsync(string name, Func<Task> body, TimeSpan? timeout = null)
        {
            try { await body().WaitAsync(timeout ?? CaseTimeout); }
            catch (Exception error) when (error is not AssertInconclusiveException)
            {
                throw new AssertFailedException($"{name}: {error.Message}", error);
            }
        }

        /// <summary>Runs one synchronous case of a table-driven test; a failure or an exception names the case.</summary>
        public static void RunCase(string name, Action body)
        {
            try { body(); }
            catch (Exception error) when (error is not AssertInconclusiveException)
            {
                throw new AssertFailedException($"{name}: {error.Message}", error);
            }
        }

        /// <summary>Waits until <paramref name="condition"/> holds, checking about once per frame, so a test does not
        /// depend on a fixed delay such as the length of an animation.</summary>
        public static async Task WaitUntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"Timed out waiting until {description}.");
                await Task.Delay(16);
            }
        }

        /// <summary>The longest a layout step may wait for the UI thread. Tests call it hundreds of times per suite,
        /// and a shared CI machine can stall the UI thread for several seconds, so it matches a test case's budget;
        /// a UI thread that is really stuck still fails.</summary>
        public static readonly TimeSpan LayoutTimeout = CaseTimeout;

        public static async Task CompleteLayoutAsync(FrameworkElement element, params FrameworkElement[] requiredElements)
        {
            await EnsureLoadedAsync(element).WaitAsync(LayoutTimeout);
            foreach (var required in requiredElements)
                await EnsureLoadedAsync(required).WaitAsync(LayoutTimeout);
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = element.DispatcherQueue;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long highRan = -1, normalRan = -1;
            // Probes at the higher priorities make a timeout say whether the UI thread was blocked (no probe ran),
            // flooded with queued work (only the high probe ran) or busy below that (both ran).
            dispatcher.TryEnqueue(DispatcherQueuePriority.High, () => highRan = watch.ElapsedMilliseconds);
            dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => normalRan = watch.ElapsedMilliseconds);
            if (!dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    element.UpdateLayout();
                    completion.TrySetResult(null);
                }))
                throw new InvalidOperationException("The runtime dispatcher rejected layout verification.");
            try
            {
                await completion.Task.WaitAsync(LayoutTimeout);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    $"The low-priority layout callback did not run within {LayoutTimeout.TotalSeconds:0} s. " +
                    $"High probe: {Describe(highRan)}, normal probe: {Describe(normalRan)}.");
            }

            static string Describe(long elapsed) => elapsed < 0 ? "did not run" : $"ran after {elapsed} ms";
        }
    }
}
