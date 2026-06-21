using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Tests
{
    public class DyeDatabaseTests
    {
        private static DyeData Dye(string id)
        {
            var d = ScriptableObject.CreateInstance<DyeData>();
            d.DyeId = id;
            d.Color = Color.red;
            d.Cost = 10;
            return d;
        }

        [Test]
        public void GetById_ReturnsMatch_AndNullForUnknown()
        {
            var db = ScriptableObject.CreateInstance<DyeDatabase>();
            db.Items = new[] { Dye("red"), Dye("blue") };

            Assert.AreEqual("red", db.GetById("red").DyeId);
            Assert.IsNull(db.GetById("nope"));
        }

        [Test]
        public void All_ReturnsAllItems()
        {
            var db = ScriptableObject.CreateInstance<DyeDatabase>();
            db.Items = new[] { Dye("red"), Dye("blue") };

            Assert.AreEqual(2, db.All.Count);
        }
    }
}
