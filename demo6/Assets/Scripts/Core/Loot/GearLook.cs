using System;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 플레이어 겉모습 id 4개(장비 문서 9-1): 갑옷(몸 그림 통째)·투구(머리 위 덧그림)·장갑(주먹)·장화. 반지·목걸이는 몸에 그리지 않는다.
    /// Loadout.Look이 만들고 PlayerController.SetLook이 받는다. 그림 쪽(TopDownPlayerRig)은 이 id로 그림 칸·임시 색을 고른다.
    /// id는 GearBaseTable 종류 id다. 비어 있으면 시작 가죽 한 벌로 본다.
    /// </summary>
    public readonly struct GearLook : IEquatable<GearLook>
    {
        public readonly string ArmorId;
        public readonly string HelmId;
        public readonly string GlovesId;
        public readonly string BootsId;

        public GearLook(string armorId, string helmId, string glovesId, string bootsId)
        {
            ArmorId = string.IsNullOrEmpty(armorId) ? GearBaseTable.LeatherArmor : armorId;
            HelmId = string.IsNullOrEmpty(helmId) ? GearBaseTable.LeatherHelm : helmId;
            GlovesId = string.IsNullOrEmpty(glovesId) ? GearBaseTable.LeatherGloves : glovesId;
            BootsId = string.IsNullOrEmpty(bootsId) ? GearBaseTable.LeatherBoots : bootsId;
        }

        /// <summary>시작 가죽 한 벌(4-4).</summary>
        public static GearLook Starting => new GearLook(GearBaseTable.LeatherArmor, GearBaseTable.LeatherHelm, GearBaseTable.LeatherGloves, GearBaseTable.LeatherBoots);

        /// <summary>그 종류 id의 무게(모르면 가벼움). 원화 전 임시 그림의 무게 색을 고를 때 쓴다(9-1).</summary>
        public static ArmorWeight WeightOf(string id)
        {
            var b = GearBaseTable.Get(id);
            return b != null && b.Weight != ArmorWeight.None ? b.Weight : ArmorWeight.Light;
        }

        public bool Equals(GearLook other) =>
            Same(ArmorId, other.ArmorId) && Same(HelmId, other.HelmId) && Same(GlovesId, other.GlovesId) && Same(BootsId, other.BootsId);

        public override bool Equals(object obj) => obj is GearLook o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Hash(ArmorId);
                h = h * 31 + Hash(HelmId);
                h = h * 31 + Hash(GlovesId);
                return h * 31 + Hash(BootsId);
            }
        }

        public override string ToString() => ArmorId + "/" + HelmId + "/" + GlovesId + "/" + BootsId;

        static bool Same(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
        static int Hash(string s) => s == null ? 0 : StringComparer.Ordinal.GetHashCode(s);
    }
}
