using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // Every creature type the field battle can spawn, with its encounter weight.
    [CreateAssetMenu(menuName = "Demo5/Battle Creatures", fileName = "BattleCreatures")]
    public sealed class BattleCreatureRoster : ScriptableObject
    {
        public List<BattleCreature> Creatures = new List<BattleCreature>();

        public BattleCreature Find(string id) => Creatures.FirstOrDefault(c => c != null && c.Id == id);
        // Existing callers are the opening site. New regions must explicitly opt into their tier;
        // merely adding art/data to this roster must not expose every creature in the tutorial.
        public IEnumerable<BattleCreature> Pool => PoolForTier(0);
        public IEnumerable<BattleCreature> PoolForTier(int regionTier) => Creatures.Where(c => c != null && c.AvailableInRegion(regionTier));

        // Weighted pick. roll returns 0..99; two rolls give enough spread for a dozen types.
        public static BattleCreature Pick(IEnumerable<BattleCreature> pool, Func<int> roll, ICollection<BattleCreature> avoid = null)
        {
            var list = pool.Where(c => c != null && c.Weight > 0 && (avoid == null || !avoid.Contains(c))).ToList();
            if (list.Count == 0) list = pool.Where(c => c != null && c.Weight > 0).ToList();
            if (list.Count == 0) return null;
            int total = list.Sum(c => c.Weight), value = (Math.Abs(roll()) * 100 + Math.Abs(roll())) % total;
            foreach (var c in list) { if (value < c.Weight) return c; value -= c.Weight; }
            return list[list.Count - 1];
        }
        // Distinct types for one encounter.
        public List<BattleCreature> Lineup(int count, Func<int> roll, int regionTier = 0)
        {
            var picked = new List<BattleCreature>();
            var available = PoolForTier(regionTier).ToList();
            for (int i = 0; i < count; i++) { var c = Pick(available, roll, picked); if (c != null) picked.Add(c); }
            return picked;
        }
    }
}
