using System;
using Flowery.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_GlobalNeumorphicSettings
{
    // The settings are statics of DaisyBaseContentControl, whose type registers WinUI dependency
    // properties, so they are tested here and not in the unit tests.
    [TestMethod]
    public void When_GlobalSettingsChange_ModeAndEnabledStayCoupledAndOnlyRealChangesNotify()
    {
        var originalMode = DaisyBaseContentControl.GlobalNeumorphicMode;
        var originalEnabled = DaisyBaseContentControl.GlobalNeumorphicEnabled;
        var originalIntensity = DaisyBaseContentControl.GlobalNeumorphicIntensity;
        var counter = new ChangeCounter();

        try
        {
            DaisyBaseContentControl.GlobalNeumorphicMode = DaisyNeumorphicMode.None;
            DaisyBaseContentControl.GlobalNeumorphicChanged += counter.OnChanged;

            DaisyBaseContentControl.GlobalNeumorphicMode = DaisyNeumorphicMode.Inset;
            Assert.IsTrue(DaisyBaseContentControl.GlobalNeumorphicEnabled, "A mode other than None enables the effect.");
            DaisyBaseContentControl.GlobalNeumorphicMode = DaisyNeumorphicMode.Inset;
            Assert.AreEqual(1, counter.Count, "Setting the same mode again raises no change.");

            DaisyBaseContentControl.GlobalNeumorphicMode = DaisyNeumorphicMode.None;
            Assert.IsFalse(DaisyBaseContentControl.GlobalNeumorphicEnabled, "None disables the effect.");

            DaisyBaseContentControl.GlobalNeumorphicEnabled = true;
            Assert.AreEqual(DaisyNeumorphicMode.Inset, DaisyBaseContentControl.GlobalNeumorphicMode, "Enabling restores the last mode other than None.");
            DaisyBaseContentControl.GlobalNeumorphicEnabled = true;
            Assert.AreEqual(3, counter.Count, "Enabling an enabled effect raises no change.");

            // Changes below 0.001 are dropped, so a slider does not refresh every control for each step.
            DaisyBaseContentControl.GlobalNeumorphicIntensity = originalIntensity + 0.01;
            Assert.AreEqual(4, counter.Count, "An intensity change raises one change.");
            DaisyBaseContentControl.GlobalNeumorphicIntensity = originalIntensity + 0.0105;
            Assert.AreEqual(4, counter.Count, "An intensity change below the threshold raises no change.");
            DaisyBaseContentControl.GlobalNeumorphicIntensity = originalIntensity + 0.02;
            Assert.AreEqual(5, counter.Count, "A later intensity change raises one change again.");
        }
        finally
        {
            DaisyBaseContentControl.GlobalNeumorphicChanged -= counter.OnChanged;
            DaisyBaseContentControl.GlobalNeumorphicMode = originalMode;
            DaisyBaseContentControl.GlobalNeumorphicEnabled = originalEnabled;
            DaisyBaseContentControl.GlobalNeumorphicIntensity = originalIntensity;
        }
    }

    private sealed class ChangeCounter
    {
        public int Count { get; private set; }

        public void OnChanged(object? sender, EventArgs e) => Count++;
    }
}
