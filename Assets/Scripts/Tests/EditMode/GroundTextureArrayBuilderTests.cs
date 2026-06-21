using NUnit.Framework;
using UnityEngine;
using WheatFarm.Editor;

namespace WheatFarm.Tests
{
    /// <summary>
    /// Tests the pure (AssetDatabase-free) array assembly logic. The menu wrapper that reads the
    /// GroundTextureSet and writes assets is exercised manually; this guards the slice validation
    /// and packing that the GroundInstanced shader depends on (slice index = GroundState ordinal).
    /// </summary>
    public class GroundTextureArrayBuilderTests
    {
        [Test]
        public void BuildArray_AssemblesSlices_WithCorrectDimensions()
        {
            var slices = new[]
            {
                new Texture2D(4, 4, TextureFormat.RGBA32, false),
                new Texture2D(4, 4, TextureFormat.RGBA32, false),
            };

            var arr = GroundTextureArrayBuilder.BuildArray(slices, out var error);

            Assert.IsNull(error);
            Assert.IsNotNull(arr);
            Assert.AreEqual(2, arr.depth);
            Assert.AreEqual(4, arr.width);
        }

        [Test]
        public void BuildArray_RejectsMismatchedSizes()
        {
            var slices = new[]
            {
                new Texture2D(4, 4, TextureFormat.RGBA32, false),
                new Texture2D(8, 8, TextureFormat.RGBA32, false),
            };

            var arr = GroundTextureArrayBuilder.BuildArray(slices, out var error);

            Assert.IsNotNull(error);
            Assert.IsNull(arr);
        }

        [Test]
        public void BuildArray_RejectsEmpty()
        {
            var arr = GroundTextureArrayBuilder.BuildArray(new Texture2D[0], out var error);

            Assert.IsNotNull(error);
            Assert.IsNull(arr);
        }

        [Test]
        public void BuildArray_RejectsNullSlice()
        {
            var slices = new Texture2D[]
            {
                new Texture2D(4, 4, TextureFormat.RGBA32, false),
                null,
            };

            var arr = GroundTextureArrayBuilder.BuildArray(slices, out var error);

            Assert.IsNotNull(error);
            Assert.IsNull(arr);
        }
    }
}
