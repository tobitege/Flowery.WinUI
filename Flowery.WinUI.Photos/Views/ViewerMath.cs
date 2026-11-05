namespace Flowery.WinUI.Photos.Views
{
    /// <summary>
    /// Pointer routing of the viewer canvas, kept free of UI types so it can be tested.
    /// </summary>
    public static class ViewerMath
    {
        /// <summary>Half the width of the before/after divider's grab area, in DIPs.</summary>
        public const double SplitHandleReach = 20;

        /// <summary>Whether <paramref name="pointerX"/> is on the before/after divider of an image shown from <paramref name="imageLeft"/>.</summary>
        public static bool IsOnSplitHandle(double imageLeft, double imageWidth, double split, double pointerX) =>
            Math.Abs(pointerX - (imageLeft + imageWidth * split)) < SplitHandleReach;

        /// <summary>
        /// What a press on the photo starts, in this order: the color picker, the navigator, the crop, the before/after
        /// divider, drawing, panning. The divider comes before drawing: in the Markup tab a drag on it moves it instead of
        /// drawing a stroke into the hidden "before" half.
        /// </summary>
        public static CanvasPress ChoosePress(in CanvasPressInput input)
        {
            var leftOrRight = input.Left || input.Right;
            if (leftOrRight && input.Eyedropper)
                return CanvasPress.PickColor;
            if (input.Left && input.OnNavigator)
                return CanvasPress.Navigator;
            if (input.Left && input.CropMode)
                return CanvasPress.Crop;
            if (input.Left && input.OnSplitHandle)
                return CanvasPress.Split;
            if (leftOrRight && input.MarkupMode)
                return CanvasPress.Markup;
            if ((input.Left || input.Middle) && input.ImageLargerThanView)
                return CanvasPress.Pan;
            return CanvasPress.None;
        }
    }

    /// <summary>What a press on the viewer canvas starts; see <see cref="ViewerMath.ChoosePress"/>.</summary>
    public enum CanvasPress
    {
        None,
        PickColor,
        Navigator,
        Crop,
        Split,
        Markup,
        Pan
    }

    /// <summary>
    /// The pressed buttons and the viewer's mode at a press. <see cref="OnSplitHandle"/> is only true while the
    /// before/after split is shown.
    /// </summary>
    public readonly record struct CanvasPressInput(
        bool Left,
        bool Right,
        bool Middle,
        bool Eyedropper,
        bool OnNavigator,
        bool CropMode,
        bool OnSplitHandle,
        bool MarkupMode,
        bool ImageLargerThanView);
}
