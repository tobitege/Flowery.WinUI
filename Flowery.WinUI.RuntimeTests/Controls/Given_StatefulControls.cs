using System;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_StatefulControls
    {
        [TestMethod]
        public async Task When_ValueExceedsMaximum_ItIsClamped()
        {
            var cases = new (string Name, Func<DaisyBaseContentControl> Create, Action<DaisyBaseContentControl> SetAboveMaximum, Func<DaisyBaseContentControl, double> Read, double Expected)[]
            {
                ("Pagination", static () => new DaisyPagination { TotalPages = 3 }, static control => ((DaisyPagination)control).CurrentPage = 5, static control => ((DaisyPagination)control).CurrentPage, 3),
                ("Rating", static () => new DaisyRating(), static control => ((DaisyRating)control).Value = 10.0, static control => ((DaisyRating)control).Value, 5),
                ("Range", static () => new DaisyRange(), static control => ((DaisyRange)control).Value = 150.0, static control => ((DaisyRange)control).Value, 100)
            };

            foreach (var (name, create, setAboveMaximum, read, expected) in cases)
            {
                var control = create();
                RuntimeTestHelpers.AttachToHost(control);
                await RuntimeTestHelpers.EnsureLoadedAsync(control);

                setAboveMaximum(control);
                Assert.AreEqual(expected, read(control), $"{name} must clamp to its maximum.");
            }
        }
    }
}
