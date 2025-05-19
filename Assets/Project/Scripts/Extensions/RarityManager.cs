using System;
using System.Collections.Generic;

public static class RarityManager
{
	private static Dictionary<Rarity, string> IdByRarity = new()
	{
		{ Rarity.COMMON, "common"},
		{ Rarity.UNCOMMON, "uncommon"},
		{ Rarity.RARE, "rare"},
		{ Rarity.EPIC, "epic"},
		{ Rarity.EPIC_1, "epic"},
		{ Rarity.EPIC_2, "epic"},
		{ Rarity.EPIC_3, "epic"},
	};
	
	private static Dictionary<Rarity, int> RareLevel = new()
	{
		{ Rarity.COMMON, 1},
		{ Rarity.UNCOMMON, 2},
		{ Rarity.RARE, 3},
		{ Rarity.EPIC, 4},
		{ Rarity.EPIC_1, 5},
		{ Rarity.EPIC_2, 6},
		{ Rarity.EPIC_3, 7},
		{ Rarity.LEGENDARY, 8},
		{ Rarity.LEGENDARY_01, 9},
		{ Rarity.LEGENDARY_02, 10},
		{ Rarity.LEGENDARY_03, 11},
	};
	
	public static int GetRarityLevel(Rarity Rarity)
    {
        if (RareLevel.TryGetValue(Rarity, out int level))
        {
			return level;
        }

		throw new Exception($"Rarity dont present {Rarity}");
    }
    
    public static string GetRarityShowName(Rarity skillRarity)
    {
	    return skillRarity switch
	    {
		    Rarity.COMMON => "Common",
		    Rarity.UNCOMMON => "Uncommon",
		    Rarity.RARE => "Rare",
		    Rarity.EPIC => "Epic",
		    Rarity.EPIC_1 => "Epic +1",
		    Rarity.EPIC_2 => "Epic +2",
		    Rarity.EPIC_3 => "Epic +3",
			Rarity.LEGENDARY => "Legendary",
			Rarity.LEGENDARY_01 => "Legendary +1",
			Rarity.LEGENDARY_02 => "Legendary +2",
			Rarity.LEGENDARY_03 => "Legendary +3",
			_ => throw new ArgumentOutOfRangeException(nameof(skillRarity), skillRarity, null)
	    };
    }

    public static bool RareEnough(Rarity rarity, Rarity requiredRarity)
    {
	    return RareLevel[rarity] >= RareLevel[requiredRarity];
    }
    public static bool MoreRare(Rarity rarity, Rarity requiredRarity)
    {
	    return RareLevel[rarity] > RareLevel[requiredRarity];
    }
    public static bool BelowRarity(Rarity rarity, Rarity requiredRarity)
    {
	    return RareLevel[rarity] <= RareLevel[requiredRarity];
    }

    public static Rarity NextRarity(Rarity rarity)
    {
	    return rarity switch
	    {
		    Rarity.COMMON => Rarity.UNCOMMON,
		    Rarity.UNCOMMON => Rarity.RARE,
		    Rarity.RARE => Rarity.EPIC,
		    Rarity.EPIC => Rarity.LEGENDARY,
		    Rarity.EPIC_1 => Rarity.LEGENDARY,
		    Rarity.EPIC_2 => Rarity.LEGENDARY,
		    Rarity.EPIC_3 => Rarity.LEGENDARY
	    };
    }
    public static Rarity PreviousRarity(Rarity rarity)
    {
	    return rarity switch
	    {
		    Rarity.UNCOMMON => Rarity.COMMON,
		    Rarity.RARE => Rarity.UNCOMMON,
		    Rarity.EPIC => Rarity.RARE,
		    Rarity.EPIC_1 => Rarity.RARE,
		    Rarity.EPIC_2 => Rarity.RARE,
		    Rarity.EPIC_3 => Rarity.RARE,
		    Rarity.LEGENDARY => Rarity.EPIC
	    };
    }
}

public enum Rarity
{
	COMMON,
	UNCOMMON,
	RARE,
	EPIC,
	EPIC_1,
	EPIC_2,
	EPIC_3,
	LEGENDARY,
	LEGENDARY_01,
	LEGENDARY_02,
	LEGENDARY_03
}