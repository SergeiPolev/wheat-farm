using NUnit.Framework;
using WheatFarm.Core.Data;

namespace WheatFarm.Tests
{
    public class MeshPropertiesTests
    {
        [Test]
        public void Size_MatchesExplicitLayout_WithNeighborTypes()
        {
            // 2 matrices (64 each) + 3 float4 (16 each) + 1 uint (4) = 180
            const int expected = 64 * 2 + 16 * 3 + 4;
            Assert.AreEqual(expected, MeshProperties.Size());
        }
    }
}
