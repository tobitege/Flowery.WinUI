using Flowery.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.UnitTests.Controls
{
    [TestClass]
    public class Given_SidebarItemSearch
    {
        [TestMethod]
        [DataRow(true, "Tag-Auswahl", true)]
        [DataRow(true, "tag-auswahl", true)]
        [DataRow(true, "Auswahl", true)]
        [DataRow(true, "DaisyTagPicker", true)]
        [DataRow(true, "TagPicker", true)]
        [DataRow(true, "tag picker", true)]
        [DataRow(true, "Toggle", false)]
        [DataRow(true, "---", false)]
        [DataRow(false, "corner radii", true)]
        [DataRow(false, "DaisyModal", false)]
        public void When_Searching_MatchesTranslatedAndControlNames(bool itemHasControlName, string search, bool expected)
        {
            // The tag picker item has the control name DaisyTagPicker; without a control name only the translated
            // name matches.
            var item = itemHasControlName
                ? new SidebarItem { Id = "tagpicker", Name = "Sidebar_TagPicker", DisplayName = "Tag-Auswahl", ControlName = "DaisyTagPicker" }
                : new SidebarItem { Id = "modal-radii", Name = "Sidebar_ModalRadii", DisplayName = "Modal Corner Radii" };

            Assert.AreEqual(expected, item.MatchesSearch(search), search);
        }
    }
}
