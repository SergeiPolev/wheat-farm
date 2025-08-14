using System;
using System.Globalization;

namespace Cathei.BakingSheet.Examples.Google
{
    public class ItemSheet : Sheet<ItemSheet.Row>
    {
        public class Row : SheetRow
        {
            public string Name { get; set; }
            public int Price { get; set; }
        }
    }
}