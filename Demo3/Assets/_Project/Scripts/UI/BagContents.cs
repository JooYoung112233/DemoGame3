using System;

namespace Live49.UI
{
    // Presentation data supplied by gameplay. Null values mean unknown, never zero.
    public sealed class BagContents
    {
        public enum Category { Food, Supplies, Keepsakes }
        public sealed class Item
        {
            public string Id, Name, Description;
            public Category Group;
            public int Quantity;
        }
        public sealed class Gauge
        {
            public float Current, Maximum;
            public bool IsKnown => !float.IsNaN(Current) && !float.IsInfinity(Current)
                && !float.IsNaN(Maximum) && !float.IsInfinity(Maximum) && Maximum > 0;
        }
        public sealed class Member
        {
            public string Id, Name;
            public Gauge Hunger, Water, Health, Stamina;
        }
        public bool InventoryKnown;
        public Item[] Items = Array.Empty<Item>();
        public Member[] Members = Array.Empty<Member>();

        public static BagContents Opening(string suhyeokName, string soiName) => new BagContents
        {
            Members = new[]
            {
                new Member { Id = "suhyeok", Name = suhyeokName },
                new Member { Id = "soi", Name = soiName }
            }
        };
    }
}
