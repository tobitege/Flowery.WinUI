namespace Flowery.Services;

internal static class DaisyMotion
{
    internal static bool AnimationsEnabled => DaisySystemAppearance.AnimationsEnabled;

    internal static event EventHandler? Changed
    {
        add { DaisySystemAppearance.Initialize(); DaisySystemAppearance.MotionChanged += value; }
        remove => DaisySystemAppearance.MotionChanged -= value;
    }
}
