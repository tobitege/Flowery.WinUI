using System;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Effects;
using Flowery.Helpers;
using Flowery.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_BehaviorMotionRegression
{
    private static Type Appearance => typeof(DaisyButton).Assembly.GetType("Flowery.Services.DaisySystemAppearance", true)!;
    private static bool MotionEnabled => (bool)Appearance.GetProperty("AnimationsEnabled", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    private static void SetMotion(bool enabled) => Appearance.GetMethod("SetMotionPreference", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { enabled });

    [TestMethod]
    public async Task When_MotionIsDisabled_ActiveBehaviorsStopWithReadableContent()
    {
        var originalMotion = MotionEnabled;
        var wave = new TextBlock { Text = "Wave" };
        var typewriter = new TextBlock { Text = "Readable text" };
        var scramble = new TextBlock { Text = "Original text" };
        var reveal = new Border { Width = 30, Height = 30 };
        var follower = new Grid { Width = 100, Height = 100 };
        var host = new StackPanel { Children = { wave, typewriter, scramble, reveal, follower } };
        try
        {
            WaveTextBehavior.SetIsEnabled(wave, true);
            TypewriterBehavior.SetSpeed(typewriter, TimeSpan.FromSeconds(10));
            TypewriterBehavior.SetIsEnabled(typewriter, true);
            ScrambleHoverBehavior.SetIsEnabled(scramble, true);
            RevealBehavior.SetManualTriggerOnly(reveal, true);
            RevealBehavior.SetDuration(reveal, TimeSpan.FromSeconds(10));
            RevealBehavior.SetMode(reveal, RevealMode.ScaleSlide);
            RevealBehavior.SetIsEnabled(reveal, true);
            CursorFollowBehavior.SetIsEnabled(follower, true);
            await Load(host, wave, typewriter, scramble, reveal, follower);
            // Establish the active state after native Loaded callbacks and pending settings notifications.
            SetMotion(true);
            TypewriterBehavior.Restart(typewriter);
            RevealBehavior.TriggerReveal(reveal);
            Assert.IsNotNull(GetTimer(typeof(WaveTextBehavior), wave),
                $"Wave start state: Loaded={wave.IsLoaded}, Enabled={WaveTextBehavior.GetIsEnabled(wave)}, Motion={MotionEnabled}, Text={wave.Text}.");
            Assert.IsNotNull(GetTimer(typeof(TypewriterBehavior), typewriter));
            Assert.AreEqual(1, follower.Children.Count);

            SetMotion(false);
            Assert.IsNull(GetTimer(typeof(WaveTextBehavior), wave));
            Assert.IsNull(GetTimer(typeof(TypewriterBehavior), typewriter));
            Assert.AreEqual("Readable text", typewriter.Text);
            Assert.AreEqual("Original text", scramble.Text);
            Assert.AreEqual(0d, ((TranslateTransform)wave.RenderTransform).Y);
            Assert.AreEqual(1d, reveal.Opacity);
            Assert.AreEqual(1d, ((CompositeTransform)reveal.RenderTransform).ScaleX);
            Assert.AreEqual(0, follower.Children.Count);
            ScrambleHoverBehavior.TriggerScramble(scramble);
            Assert.AreEqual("Original text", scramble.Text);

            SetMotion(true);
            Assert.IsNotNull(GetTimer(typeof(WaveTextBehavior), wave));
            Assert.AreEqual(1, follower.Children.Count);
            Assert.AreEqual("Readable text", typewriter.Text);
        }
        finally
        {
            WaveTextBehavior.SetIsEnabled(wave, false);
            TypewriterBehavior.SetIsEnabled(typewriter, false);
            ScrambleHoverBehavior.SetIsEnabled(scramble, false);
            RevealBehavior.SetIsEnabled(reveal, false);
            CursorFollowBehavior.SetIsEnabled(follower, false);
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            SetMotion(originalMotion);
        }
    }

    [TestMethod]
    public async Task When_MotionIsDisabled_TransitionCompletesAndSlideshowPauses()
    {
        var originalMotion = MotionEnabled;
        var oldSlide = new Border();
        var newSlide = new Border();
        var host = new Grid { Width = 120, Height = 80, Children = { oldSlide, newSlide } };
        using var controller = new FlowerySlideshowController(_ => { }, () => 2, () => 0);
        try
        {
            SetMotion(true);
            await Load(host);
            controller.Mode = FlowerySlideshowMode.Slideshow;
            Assert.IsTrue(controller.IsRunning);
            var completions = 0;
            FlowerySlideTransitionHelpers.ApplyTransition(host, oldSlide, newSlide, FlowerySlideTransition.PushLeft,
                new FlowerySlideTransitionParams { Duration = TimeSpan.FromSeconds(10) }, () => completions++);
            Assert.IsTrue(FlowerySlideTransitionHelpers.IsTransitioning(newSlide));

            SetMotion(false);
            Assert.IsFalse(controller.IsRunning);
            Assert.IsFalse(FlowerySlideTransitionHelpers.IsTransitioning(newSlide));
            Assert.AreEqual(1, completions);
            Assert.AreEqual(Visibility.Collapsed, oldSlide.Visibility);
            Assert.AreEqual(Visibility.Visible, newSlide.Visibility);
            Assert.AreEqual(1d, newSlide.Opacity);
            Assert.AreEqual(0d, ((CompositeTransform)newSlide.RenderTransform).TranslateX);
            Assert.AreEqual(FlowerySlideTransition.None,
                FlowerySlideTransitionHelpers.ApplyTransition(host, oldSlide, newSlide, FlowerySlideTransition.Pixelate));
            SetMotion(true);
            Assert.IsTrue(controller.IsRunning);
            controller.Stop();
            SetMotion(false);
            SetMotion(true);
            Assert.IsFalse(controller.IsRunning);
        }
        finally
        {
            controller.Stop();
            FlowerySlideTransitionHelpers.StopTransition(newSlide);
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            SetMotion(originalMotion);
        }
    }

    private static DispatcherTimer? GetTimer(Type behavior, DependencyObject element)
        => element.GetValue((DependencyProperty)behavior.GetField("TimerProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!) as DispatcherTimer;

    private static async Task Load(FrameworkElement element, params FrameworkElement[] children)
    {
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element).WaitAsync(TimeSpan.FromSeconds(5));
        foreach (var child in children)
            await RuntimeTestHelpers.EnsureLoadedAsync(child).WaitAsync(TimeSpan.FromSeconds(5));
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        element.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => flushed.SetResult());
        await flushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        element.UpdateLayout();
    }
}
