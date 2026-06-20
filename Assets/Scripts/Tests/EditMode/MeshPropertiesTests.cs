using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
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

        [Test]
        public void Size_MatchesRuntimeStructStride()
        {
            // ChunkCropRenderer creates the ComputeBuffer with Size() as stride, and the
            // HLSL MeshProperties mirror must match the same byte layout. UnsafeUtility.SizeOf
            // is what Unity uses under the hood — comparing against it catches struct padding /
            // accidental field changes that the hand-computed expected value above would miss.
            Assert.AreEqual(UnsafeUtility.SizeOf<MeshProperties>(), MeshProperties.Size(),
                "Size() must equal the real struct stride; otherwise GPU reads MeshProperties at wrong offsets.");
        }
    }
}
