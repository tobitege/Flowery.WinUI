using Flowery.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls;

/// <summary>The pinch of two touch pointers, as DaisyScheduler and FlowKanban follow it.</summary>
[TestClass]
public sealed class Given_PinchTracker
{
    [TestMethod]
    public void When_OneFingerOfAPinchLifts_TheTrackerForgetsBoth_AndTheNextTouchIsNoPinch()
    {
        var tracker = new FloweryPinchTracker();
        var starts = 0;
        tracker.Started += () => starts++;
        tracker.Press(1, new Point(100, 100));
        tracker.Press(2, new Point(200, 100));
        Assert.AreEqual(1, starts);

        tracker.Lift(1);
        Assert.IsFalse(tracker.IsPinching);
        Assert.IsFalse(tracker.IsTouching, "The tracker still follows a finger of the pinch.");
        Assert.IsTrue(tracker.HasPinched, "The finger still down may tap or drag after the pinch.");
        Assert.IsFalse(tracker.Move(2, new Point(250, 100)), "The finger still down moved the pinch on.");

        tracker.Press(3, new Point(150, 150));
        Assert.IsFalse(tracker.IsPinching, "One finger pinched with a finger of the last pinch.");
        Assert.IsFalse(tracker.HasPinched);
        Assert.AreEqual(1, starts);
        tracker.Lift(3);
        Assert.IsFalse(tracker.IsTouching);
    }

    [TestMethod]
    public async Task When_BothFingersOfOneTouchEventMove_ThePinchChangesOnceWithBothPositions()
    {
        var tracker = new FloweryPinchTracker();
        var scales = new List<double>();
        tracker.Changed += (scale, _) => scales.Add(scale);
        tracker.Press(1, new Point(100, 100));
        tracker.Press(2, new Point(200, 100));

        // A zoom between the two moves would lay the content out twice in one frame.
        Assert.IsTrue(tracker.Move(1, new Point(50, 100), deferChange: true));
        Assert.IsTrue(tracker.Move(2, new Point(250, 100), deferChange: true));
        Assert.IsEmpty(scales, "The first finger's move zoomed before the second finger's was read.");

        var turn = new TaskCompletionSource();
        DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low, () => turn.SetResult());
        await turn.Task;
        Assert.HasCount(1, scales);
        Assert.AreEqual(2, scales[0], 1e-9);
    }

    [TestMethod]
    public void When_AFingerLiftsBeforeTheDispatcherReportsTheLastMove_ThePinchStillChangesToIt()
    {
        var tracker = new FloweryPinchTracker();
        var scales = new List<double>();
        tracker.Changed += (scale, _) => scales.Add(scale);
        tracker.Press(1, new Point(100, 100));
        tracker.Press(2, new Point(200, 100));
        tracker.Move(1, new Point(125, 100), deferChange: true);
        tracker.Move(2, new Point(175, 100), deferChange: true);

        tracker.Lift(1);
        Assert.HasCount(1, scales, "The last move of the pinch was lost with the lift.");
        Assert.AreEqual(0.5, scales[0], 1e-9);
    }

    [TestMethod]
    public async Task When_TwoFingersPinchInAScrollViewer_ItNeitherScrollsNorZooms_UntilThePinchEnds()
    {
        var content = new Border { Width = 2000, Height = 2000 };
        var scroll = new ScrollViewer { Width = 400, Height = 300, HorizontalScrollMode = ScrollMode.Enabled, Content = content };
        RuntimeTestHelpers.AttachToHost(scroll);
        try
        {
            await RuntimeTestHelpers.CompleteLayoutAsync(scroll);
            var tracker = new FloweryPinchTracker();
            tracker.Press(1, new Point(100, 100), content);
            Assert.AreEqual(ScrollMode.Enabled, scroll.HorizontalScrollMode, "One finger froze the scroll viewer.");

            tracker.Press(2, new Point(200, 100), content);
            Assert.AreEqual(ScrollMode.Disabled, scroll.HorizontalScrollMode);
            Assert.AreEqual(ScrollMode.Disabled, scroll.VerticalScrollMode);
            Assert.AreEqual(ZoomMode.Disabled, scroll.ZoomMode);

            // The scroll viewer gets its own values back: a local value stays local, a default stays a default.
            tracker.Lift(2);
            Assert.AreEqual(ScrollMode.Enabled, scroll.ReadLocalValue(ScrollViewer.HorizontalScrollModeProperty));
            Assert.AreEqual(DependencyProperty.UnsetValue, scroll.ReadLocalValue(ScrollViewer.VerticalScrollModeProperty));
            Assert.AreEqual(DependencyProperty.UnsetValue, scroll.ReadLocalValue(ScrollViewer.ZoomModeProperty));
        }
        finally
        {
            await RuntimeTestHelpers.DetachFromHostAsync(scroll);
        }
    }

    [TestMethod]
    public void When_FingersLiftWithoutAPinch_TheyAreForgottenOneByOne()
    {
        var tracker = new FloweryPinchTracker();
        tracker.Press(1, new Point(100, 100));
        tracker.Lift(1);
        Assert.IsFalse(tracker.IsTouching);
        Assert.IsFalse(tracker.HasPinched);
    }
}
