using NUnit.Framework;
using Pasjans.UI;

namespace Pasjans.Tests
{
    public sealed class CardTapCounterTests
    {
        [Test]
        public void TwoAndroidSingleClicksBecomeDoubleTap()
        {
            var counter = new CardTapCounter();

            Assert.That(counter.Register(10f, 1), Is.EqualTo(1));
            Assert.That(counter.Register(10.25f, 1), Is.EqualTo(2));
        }

        [Test]
        public void SlowClicksRemainSeparate()
        {
            var counter = new CardTapCounter();

            Assert.That(counter.Register(10f, 1), Is.EqualTo(1));
            Assert.That(counter.Register(10.5f, 1), Is.EqualTo(1));
        }

        [Test]
        public void DragResetPreventsAccidentalDoubleTap()
        {
            var counter = new CardTapCounter();

            Assert.That(counter.Register(10f, 1), Is.EqualTo(1));
            counter.Reset();
            Assert.That(counter.Register(10.2f, 1), Is.EqualTo(1));
        }
    }
}
