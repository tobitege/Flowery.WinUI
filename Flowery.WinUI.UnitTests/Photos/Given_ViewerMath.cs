using Flowery.WinUI.Photos.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.UnitTests.Photos
{
    [TestClass]
    public class Given_ViewerMath
    {
        [TestMethod]
        public void When_DividerIsPressedInTheMarkupTab_TheDividerMovesInsteadOfDrawing()
        {
            var press = new CanvasPressInput(Left: true, Right: false, Middle: false, Eyedropper: false, OnNavigator: false,
                CropMode: false, OnSplitHandle: true, MarkupMode: true, ImageLargerThanView: false);

            Assert.AreEqual(CanvasPress.Split, ViewerMath.ChoosePress(press));
            Assert.AreEqual(CanvasPress.Markup, ViewerMath.ChoosePress(press with { OnSplitHandle = false }), "away from the divider it draws");
            Assert.AreEqual(CanvasPress.Markup, ViewerMath.ChoosePress(press with { Left = false, Right = true }),
                "the right button draws with the secondary color, also on the divider");
        }

        [TestMethod]
        public void When_PressingTheCanvas_ThePickerNavigatorAndCropComeFirst()
        {
            var all = new CanvasPressInput(Left: true, Right: false, Middle: false, Eyedropper: true, OnNavigator: true,
                CropMode: true, OnSplitHandle: true, MarkupMode: true, ImageLargerThanView: true);

            Assert.AreEqual(CanvasPress.PickColor, ViewerMath.ChoosePress(all));
            Assert.AreEqual(CanvasPress.Navigator, ViewerMath.ChoosePress(all with { Eyedropper = false }));
            Assert.AreEqual(CanvasPress.Crop, ViewerMath.ChoosePress(all with { Eyedropper = false, OnNavigator = false }));
            Assert.AreEqual(CanvasPress.Pan, ViewerMath.ChoosePress(all with
            {
                Left = false, Middle = true, Eyedropper = false, OnNavigator = false, CropMode = false, MarkupMode = false
            }));
            Assert.AreEqual(CanvasPress.None, ViewerMath.ChoosePress(default));
        }

        [TestMethod]
        public void When_PointerIsNearTheDivider_ItIsOnTheHandle()
        {
            // Image from x = 100, 400 wide, split in the middle: the divider is at x = 300.
            Assert.IsTrue(ViewerMath.IsOnSplitHandle(100, 400, 0.5, 285));
            Assert.IsTrue(ViewerMath.IsOnSplitHandle(100, 400, 0.5, 319));
            Assert.IsFalse(ViewerMath.IsOnSplitHandle(100, 400, 0.5, 321));
        }
    }
}
