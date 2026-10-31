using Flowery.Helpers;
using Flowery.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public sealed class Given_FloweryTimeHelpers
{
    [TestMethod]
    public void When_ATwelveHourTimeIsParsed_EveryLanguageReadsItsOwnAmPmLabels()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        try
        {
            foreach (var language in FloweryLocalization.SupportedLanguages)
            {
                FloweryLocalization.SetCulture(language);
                foreach (var hours in (int[])[0, 9, 12, 15, 23])
                {
                    // Right-to-left cultures write the label before the time, and labels are one to six characters long.
                    var text = FloweryTimeHelpers.FormatClockTime(hours, 30, 0, ClockFormat.TwelveHour, showSeconds: false);
                    AssertParses(text, new TimeSpan(hours, 30, 0), language);
                }

                var pm = FloweryTimeHelpers.GetPmLabel();
                AssertParses("10:30" + pm, new TimeSpan(22, 30, 0), language);
                AssertParses(pm + "10:30", new TimeSpan(22, 30, 0), language);
                AssertParses("10:30 " + FloweryTimeHelpers.GetAmLabel().ToLowerInvariant(), new TimeSpan(10, 30, 0), language);
                AssertParses("10:30 pm", new TimeSpan(22, 30, 0), language);
            }
        }
        finally
        {
            FloweryLocalization.SetCulture(previousCulture);
        }

        static void AssertParses(string text, TimeSpan expected, string language)
        {
            Assert.IsTrue(FloweryTimeHelpers.TryParseTime(text, out var parsed), $"{language}: '{text}'");
            Assert.AreEqual(expected, parsed, $"{language}: '{text}'");
        }
    }
}
