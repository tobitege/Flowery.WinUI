using Flowery.WinUI.Photos.Editing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.UnitTests.Photos
{
    [TestClass]
    public class Given_CropAspectLock
    {
        [TestMethod]
        public void When_LockedTo16By9AndTurnedToPortrait_TheLockBecomes9By16()
        {
            var aspectLock = new CropAspectLock(16.0 / 9, 16.0 / 9, Rotation: 0);

            Assert.AreEqual(9.0 / 16, aspectLock.AspectAt(1)!.Value, 1e-9);
            Assert.IsTrue(aspectLock.IsSelected(9.0 / 16, 1), "the 9:16 button shows as chosen");
            Assert.IsFalse(aspectLock.IsSelected(16.0 / 9, 1), "16:9 no longer fits the portrait crop");
            Assert.IsTrue(aspectLock.IsSelected(16.0 / 9, 2));
            Assert.AreEqual(9.0 / 16, aspectLock.AspectAt(3)!.Value, 1e-9, "a quarter turn the other way is portrait too");
            Assert.AreEqual(16.0 / 9, aspectLock.AspectAt(0)!.Value, 1e-9, "undoing the turn restores 16:9");
        }

        [TestMethod]
        public void When_LockedToOriginal_TheOriginalButtonStaysChosenThroughTurns()
        {
            var aspectLock = new CropAspectLock(CropAspectLock.OriginalPreset, 4.0 / 3, Rotation: 1);

            Assert.IsTrue(aspectLock.IsSelected(CropAspectLock.OriginalPreset, 1));
            Assert.IsTrue(aspectLock.IsSelected(CropAspectLock.OriginalPreset, 2));
            Assert.IsFalse(aspectLock.IsSelected(null, 2));
            Assert.AreEqual(3.0 / 4, aspectLock.AspectAt(2)!.Value, 1e-9);
        }

        [TestMethod]
        public void When_NoAspectIsLocked_TheCropIsFree()
        {
            CropAspectLock free = default;

            Assert.IsNull(free.AspectAt(1));
            Assert.IsTrue(free.IsSelected(null, 3));
            Assert.IsFalse(free.IsSelected(1, 0));
            Assert.IsFalse(free.IsSelected(CropAspectLock.OriginalPreset, 0));
        }
    }
}
