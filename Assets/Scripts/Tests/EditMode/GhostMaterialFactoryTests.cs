using NUnit.Framework;
using UnityEngine;
using WheatFarm.Player.Preview;

namespace WheatFarm.Tests
{
    public class GhostMaterialFactoryTests
    {
        private GhostMaterialFactory _factory;
        private Material _source;

        [SetUp]
        public void SetUp()
        {
            _factory = new GhostMaterialFactory();
            _source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _source.SetColor("_BaseColor", new Color(0.3f, 0.5f, 0.7f, 1f));
            _source.SetTexture("_BaseMap", Texture2D.whiteTexture);
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            Object.DestroyImmediate(_source);
        }

        [Test]
        public void Get_CopiesBaseMapAndColor()
        {
            var ghost = _factory.Get(_source);
            Assert.AreEqual("WheatFarm/GhostPreview", ghost.shader.name);
            Assert.AreEqual(Texture2D.whiteTexture, ghost.GetTexture("_BaseMap"));

            // Compare via the engine's own equality (epsilon-based): Material color
            // properties round-trip through a float->half->float quantization in the
            // editor's serialized storage, so exact struct equality on Color is too strict.
            var expected = new Color(0.3f, 0.5f, 0.7f, 1f);
            var actual = ghost.GetColor("_BaseColor");
            Assert.IsTrue(expected == actual, $"Expected {expected} but was {actual}");
        }

        [Test]
        public void Get_SameSource_ReturnsCachedInstance()
        {
            Assert.AreSame(_factory.Get(_source), _factory.Get(_source));
        }

        [Test]
        public void Get_SourceWithoutBaseMap_DoesNotThrow()
        {
            var bare = new Material(Shader.Find("Sprites/Default"));
            Assert.DoesNotThrow(() => _factory.Get(bare));
            Object.DestroyImmediate(bare);
        }
    }
}
