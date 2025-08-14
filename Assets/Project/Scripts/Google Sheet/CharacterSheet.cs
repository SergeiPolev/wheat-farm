using System.Collections.Generic;

namespace Cathei.BakingSheet.Examples.Google
{
    public class CharacterSheet : Sheet<CharacterSheet.Row>
    {
        public class Row : SheetRowArray<Elem>
        {
            public string Name { get; set; }
        }

        public class Elem : SheetRowElem
        {
            public int Level { get; set; }
            public ItemSheet.Reference RequiredItem { get; set; }
            public Dictionary<StatType, int> Stat { get; set; }
        }
    }
}