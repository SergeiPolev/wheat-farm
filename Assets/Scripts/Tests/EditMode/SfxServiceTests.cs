using System;
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core;

namespace WheatFarm.Tests
{
    public class SfxServiceTests
    {
        [Test]
        public void EverySfx_SynthesizesAudibleUnclippedClip()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var clip = SfxService.Synthesize(id);
                Assert.IsNotNull(clip, id.ToString());
                Assert.Less(clip.length, 0.5f, $"{id} should be a short one-shot");
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f;
                foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                Assert.Greater(peak, 0.05f, $"{id} is silent");
                Assert.LessOrEqual(peak, 1.0f, $"{id} clips");
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
