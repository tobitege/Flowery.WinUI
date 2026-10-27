using Flowery.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.UnitTests.Controls
{
    [TestClass]
    public class Given_SidebarItemSearch
    {
        [TestMethod]
        [DataRow("DaisyTagPicker", "Tag-Auswahl", true)]
        [DataRow("DaisyTagPicker", "tag-auswahl", true)]
        [DataRow("DaisyTagPicker", "Auswahl", true)]
        [DataRow("DaisyTagPicker", "DaisyTagPicker", true)]
        [DataRow("DaisyTagPicker", "TagPicker", true)]
        [DataRow("DaisyTagPicker", "tag picker", true)]
        [DataRow("DaisyTagPicker", "Toggle", false)]
        [DataRow("DaisyTagPicker", "---", false)]
        [DataRow(null, "corner radii", true)]
        [DataRow(null, "DaisyModal", false)]
        public void When_Searching_MatchesTranslatedAndControlNames(string? controlName, string search, bool expected)
        {
            // Without a control name only the translated name matches.
            var item = controlName is null
                ? new SidebarItem { Id = "modal-radii", Name = "Sidebar_ModalRadii", DisplayName = "Modal Corner Radii" }
                : new SidebarItem { Id = "tagpicker", Name = "Sidebar_TagPicker", DisplayName = "Tag-Auswahl", ControlName = controlName };

            Assert.AreEqual(expected, item.MatchesSearch(search), search);
        }
    }
}
