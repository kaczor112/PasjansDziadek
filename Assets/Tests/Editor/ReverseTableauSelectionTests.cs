using NUnit.Framework;
using Pasjans.Core;
using Pasjans.UI;

namespace Pasjans.Tests
{
    public sealed class ReverseTableauSelectionTests
    {
        [Test]
        public void DifferentTableauColumnsAllowReverseAttempt()
        {
            Assert.That(PasjansApp.CanTryReverseTableauMove(
                new PileRef(PileKind.Tableau, 2), new PileRef(PileKind.Tableau, 7)), Is.True);
        }

        [Test]
        public void SameTableauColumnKeepsExistingSelectionRules()
        {
            Assert.That(PasjansApp.CanTryReverseTableauMove(
                new PileRef(PileKind.Tableau, 2), new PileRef(PileKind.Tableau, 2)), Is.False);
        }

        [TestCase(PileKind.Waste, PileKind.Tableau)]
        [TestCase(PileKind.Tableau, PileKind.Foundation)]
        [TestCase(PileKind.Foundation, PileKind.Tableau)]
        public void ReverseAttemptIsLimitedToTableau(PileKind first, PileKind second)
        {
            Assert.That(PasjansApp.CanTryReverseTableauMove(
                new PileRef(first, 0), new PileRef(second, 1)), Is.False);
        }
    }
}
