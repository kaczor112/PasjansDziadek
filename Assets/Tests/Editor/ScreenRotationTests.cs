using NUnit.Framework;
using Pasjans.UI;
using UnityEngine;

namespace Pasjans.Tests
{
    public sealed class ScreenRotationTests
    {
        [TestCase(ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft)]
        [TestCase(ScreenOrientation.LandscapeLeft, ScreenOrientation.PortraitUpsideDown)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeRight)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.Portrait)]
        public void ForcedOrientationRotatesClockwise(ScreenOrientation current, ScreenOrientation expected)
        {
            Assert.That(PasjansApp.NextForcedOrientation(current, true), Is.EqualTo(expected));
        }

        [Test]
        public void AutomaticPortraitChangesToLandscape()
        {
            Assert.That(PasjansApp.NextForcedOrientation(ScreenOrientation.AutoRotation, true),
                Is.EqualTo(ScreenOrientation.LandscapeLeft));
        }

        [Test]
        public void AutomaticLandscapeChangesToPortrait()
        {
            Assert.That(PasjansApp.NextForcedOrientation(ScreenOrientation.AutoRotation, false),
                Is.EqualTo(ScreenOrientation.Portrait));
        }
    }
}
