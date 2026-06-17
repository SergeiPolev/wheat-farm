using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Infrastructure.Save;

namespace WheatFarm.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="FarmSaveService"/>, focused on the
    /// _hasCompatibleSaveCache that drives HasSave().
    ///
    /// Every test runs against a throwaway temp file under Application.temporaryCachePath
    /// (via <see cref="TempFarmSaveService"/>), so real player data in persistentDataPath
    /// is never read or written.
    /// </summary>
    public class FarmSaveServiceTests
    {
        /// <summary>FarmSaveService whose SavePath is redirected to an arbitrary temp file.</summary>
        private sealed class TempFarmSaveService : FarmSaveService
        {
            private readonly string _path;
            public TempFarmSaveService(string path) => _path = path;
            protected override string SavePath => _path;
        }

        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            // Unique per test so runs never collide and nothing leaks between tests.
            _tempPath = Path.Combine(
                Application.temporaryCachePath,
                $"farm_save_test_{Guid.NewGuid():N}.json");
            if (File.Exists(_tempPath))
                File.Delete(_tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath))
                File.Delete(_tempPath);
        }

        private FarmSaveService NewService() => new TempFarmSaveService(_tempPath);

        /// <summary>Writes a minimal save JSON with the given Version field directly to disk.</summary>
        private void WriteSaveJson(int version) =>
            File.WriteAllText(_tempPath, $"{{\"Version\":{version}}}");

        [Test]
        public void HasSave_FreshProbe_NoFile_ReturnsFalse()
        {
            var service = NewService();
            Assert.IsFalse(service.HasSave());
        }

        [Test]
        public void HasSave_WithV2Save_ReturnsTrue()
        {
            WriteSaveJson(2);
            var service = NewService();
            Assert.IsTrue(service.HasSave());
        }

        [Test]
        public void HasSave_WithV1Save_ReturnsFalseAndDeletesFile()
        {
            WriteSaveJson(1);
            var service = NewService();

            Assert.IsFalse(service.HasSave(), "a v1 save is incompatible (requires v2+)");
            Assert.IsFalse(File.Exists(_tempPath), "an incompatible save should be deleted");
        }

        [Test]
        public void HasSave_CachesResult_DoesNotReadDiskAgain()
        {
            WriteSaveJson(2);
            var service = NewService();
            Assert.IsTrue(service.HasSave(), "first probe reads disk and returns true");

            // Remove the file behind the service's back. A real second disk read would now
            // return false; a cached answer stays true. Divergence proves the cache was used.
            File.Delete(_tempPath);
            Assert.IsTrue(service.HasSave(), "second call must serve cached true, not re-read disk");
        }

        [Test]
        public void DeleteSave_UpdatesCache_HasSaveReturnsFalse()
        {
            WriteSaveJson(2);
            var service = NewService();
            Assert.IsTrue(service.HasSave(), "cache primed to true");

            service.DeleteSave();
            Assert.IsFalse(File.Exists(_tempPath), "DeleteSave removes the file");

            // Recreate a valid save behind the service's back. HasSave still reports false:
            // DeleteSave set the cache to a definitive false, so HasSave does not re-probe disk.
            WriteSaveJson(2);
            Assert.IsFalse(service.HasSave(), "DeleteSave caches false; HasSave does not re-probe");
        }

        [Test]
        public async Task Save_SetsCacheTrue_WithoutReadingFile()
        {
            var service = NewService();
            await service.Save(new FarmSaveData());

            // Delete the just-written file behind the service's back. If Save set the cache,
            // HasSave returns true with no disk access; a re-read would instead see no file.
            File.Delete(_tempPath);
            Assert.IsTrue(service.HasSave(), "Save() sets cache=true so HasSave needs no disk read");
        }
    }
}
