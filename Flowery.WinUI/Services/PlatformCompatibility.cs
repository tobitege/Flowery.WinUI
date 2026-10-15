using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Shapes;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace Flowery.Services
{
    /// <summary>
    /// Native WinUI shape and Windows Composition animation helpers.
    /// </summary>
    internal static class PlatformCompatibility
    {
        /// <summary>
        /// Sets the selected stroke caps and join to Round on a Shape.
        /// </summary>
        /// <param name="shape">The shape to configure.</param>
        /// <param name="setStartCap">If true, sets StrokeStartLineCap to Round.</param>
        /// <param name="setEndCap">If true, sets StrokeEndLineCap to Round.</param>
        /// <param name="setLineJoin">If true, sets StrokeLineJoin to Round.</param>
        public static void SafeSetRoundedStroke(Shape? shape, bool setStartCap = true, bool setEndCap = true, bool setLineJoin = false)
        {
            if (shape is null)
                return;

            if (setStartCap)
                shape.StrokeStartLineCap = PenLineCap.Round;
            if (setEndCap)
                shape.StrokeEndLineCap = PenLineCap.Round;
            if (setLineJoin)
                shape.StrokeLineJoin = PenLineJoin.Round;
        }

        public static void StartAnimation(Visual visual, string property, KeyFrameAnimation animation, TimeSpan delay = default)
        {
            if (!DaisyMotion.AnimationsEnabled)
                return;

            if (delay > TimeSpan.Zero)
                animation.DelayTime = delay;

            visual.StartAnimation(property, animation);
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        public static float DegreesToRadians(float degrees) => degrees * (float)(Math.PI / 180.0);

        /// <summary>
        /// Creates a scalar keyframe animation for RotationAngle using degree values.
        /// </summary>
        /// <param name="compositor">The compositor to create the animation.</param>
        /// <param name="keyframes">Array of (progress, angleDegrees) tuples.</param>
        /// <param name="duration">Animation duration.</param>
        /// <param name="iterationBehavior">Animation iteration behavior.</param>
        /// <returns>A configured ScalarKeyFrameAnimation.</returns>
        public static ScalarKeyFrameAnimation CreateRotationAnimation(
            Compositor compositor,
            (float progress, float angleDegrees)[] keyframes,
            TimeSpan duration,
            AnimationIterationBehavior iterationBehavior = AnimationIterationBehavior.Forever)
        {
            var animation = compositor.CreateScalarKeyFrameAnimation();
            foreach (var (progress, angleDegrees) in keyframes)
            {
                animation.InsertKeyFrame(progress, DegreesToRadians(angleDegrees));
            }
            animation.Duration = duration;
            animation.IterationBehavior = iterationBehavior;
            return animation;
        }

        /// <summary>
        /// Starts a rotation animation on a visual using degree values.
        /// </summary>
        /// <param name="visual">The visual to animate.</param>
        /// <param name="keyframes">Array of (progress, angleDegrees) tuples.</param>
        /// <param name="duration">Animation duration.</param>
        /// <param name="delay">Optional delay before starting.</param>
        public static void StartRotationAnimationInDegrees(
            Visual visual,
            (float progress, float angleDegrees)[] keyframes,
            TimeSpan duration,
            TimeSpan delay = default)
        {
            var animation = CreateRotationAnimation(visual.Compositor, keyframes, duration);
            StartAnimation(visual, "RotationAngle", animation, delay);
        }

        public static void StartRotationAnimation(Visual visual, double durationSeconds)
        {
            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            var linear = visual.Compositor.CreateLinearEasingFunction();
            animation.InsertKeyFrame(0f, 0f, linear);
            // End just shy of 360 degrees to avoid a visible hitch when the loop wraps.
            animation.InsertKeyFrame(1f, DegreesToRadians(359.99f), linear);
            animation.Duration = TimeSpan.FromSeconds(durationSeconds);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            StartAnimation(visual, "RotationAngle", animation);
        }

        public static void StopRotationAnimation(Visual? visual)
        {
            if (visual is null)
                return;

            visual.StopAnimation("RotationAngle");
            visual.RotationAngle = 0;
        }
    }
}
