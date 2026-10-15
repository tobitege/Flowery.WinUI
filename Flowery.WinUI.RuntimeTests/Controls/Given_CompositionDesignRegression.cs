using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    [DoNotParallelize]
    public class Given_CompositionDesignRegression
    {
        [TestMethod]
        public async Task When_Motion_Preference_Changes_Composition_Controls_Stop_And_Restart()
        {
            var appearance = typeof(DaisyLoading).Assembly.GetType("Flowery.Services.DaisySystemAppearance", true)!;
            var setMotion = appearance.GetMethod("SetMotionPreference", BindingFlags.Static | BindingFlags.NonPublic)!;
            var original = (bool)appearance.GetProperty("AnimationsEnabled", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var loader = new DaisyLoading { Variant = DaisyLoadingVariant.PrinterOutput };
            var indicator = new DaisyStatusIndicator { Variant = DaisyStatusIndicatorVariant.Pulse };
            var progress = new DaisyProgress { Shimmer = true, Value = 50, Width = 100 };
            var radial = new DaisyRadialProgress { Shimmer = true, Value = 50 };
            var skeleton = new DaisySkeleton { Width = 100, Height = 20 };
            var panel = new StackPanel();
            FrameworkElement[] controls = [loader, indicator, progress, radial, skeleton];
            foreach (var control in controls) panel.Children.Add(control);
            try
            {
                setMotion.Invoke(null, new object[] { true });
                RuntimeTestHelpers.AttachToHost(panel);
                foreach (var control in controls) await RuntimeTestHelpers.EnsureLoadedAsync(control);
                await DrainDispatcherAsync(panel);
                Assert.IsTrue((bool)GetField(loader, "_isAnimating")!);
                Assert.IsTrue(((ICollection)GetField(loader, "_activeTimers")!).Count > 0);

                setMotion.Invoke(null, new object[] { false });
                await DrainDispatcherAsync(panel);
                Assert.IsFalse((bool)GetField(loader, "_isAnimating")!);
                Assert.AreEqual(0, ((ICollection)GetField(loader, "_activeTimers")!).Count);
                Assert.IsFalse((bool)GetField(indicator, "_isAnimating")!);
                Assert.IsFalse((bool)GetField(skeleton, "_isAnimating")!);
                Assert.IsNull(GetField(progress, "_shimmerAnimation"));
                Assert.IsNull(GetField(radial, "_shimmerAnimation"));
                Assert.IsNull(GetField(skeleton, "_pulseAnimation"));

                var unloaded = new TaskCompletionSource();
                indicator.Unloaded += (_, _) => unloaded.TrySetResult();
                panel.Children.Remove(indicator);
                await unloaded.Task;
                panel.Children.Add(indicator);
                await RuntimeTestHelpers.EnsureLoadedAsync(indicator);
                Assert.IsFalse((bool)GetField(indicator, "_isAnimating")!);

                setMotion.Invoke(null, new object[] { true });
                await DrainDispatcherAsync(panel);
                Assert.IsTrue((bool)GetField(loader, "_isAnimating")!);
                Assert.IsTrue(((ICollection)GetField(loader, "_activeTimers")!).Count > 0);
                Assert.IsTrue((bool)GetField(indicator, "_isAnimating")!);
                Assert.IsTrue((bool)GetField(skeleton, "_isAnimating")!);
                Assert.IsNotNull(GetField(progress, "_shimmerAnimation"));
                Assert.IsNotNull(GetField(radial, "_shimmerAnimation"));
                Assert.IsNotNull(GetField(skeleton, "_pulseAnimation"));
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await DrainDispatcherAsync(panel);
                setMotion.Invoke(null, new object[] { original });
            }
        }

        private static object? GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);

        private static Task DrainDispatcherAsync(FrameworkElement element)
        {
            var completion = new TaskCompletionSource();
            element.DispatcherQueue.TryEnqueue(() => completion.SetResult());
            return completion.Task;
        }
    }
}
