using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo5.NightRun
{
    public enum JourneyStage { Party, HomeChoice, Settlement, Expedition, Lost }
    public sealed class Adventurer
    {
        public readonly string Name, Role, Description;
        public readonly int MaxHealth, Aim;
        public int Health;
        public Adventurer(string name, string role, string description, int health, int aim)
        { Name = name; Role = role; Description = description; Health = MaxHealth = health; Aim = aim; }
    }
    public sealed class HomeSite
    {
        public readonly string Name, Description;
        public readonly int Supplies, Ammo, Recovery;
        public HomeSite(string name, string description, int supplies, int ammo, int recovery)
        { Name = name; Description = description; Supplies = supplies; Ammo = ammo; Recovery = recovery; }
    }
    public sealed class CampaignState
    {
        public JourneyStage Stage { get; private set; } = JourneyStage.Party;
        public readonly Adventurer[] Candidates = {
            new Adventurer("민서", "정찰수", "균형 잡힌 체력과 조준", 5, 5),
            new Adventurer("도윤", "운반꾼", "튼튼한 체력으로 위험 감수", 7, 0),
            new Adventurer("하린", "사수", "낮은 체력 · 높은 사격 명중률", 4, 15),
            new Adventurer("태오", "경비원", "안정적인 체력", 6, 0)
        };
        public readonly HomeSite[] Sites = {
            new HomeSite("폐정비소", "작업대와 남은 탄약이 있는 공간", 2, 6, 2),
            new HomeSite("작은 주택", "남은 식료품을 챙겨 시작하는 집", 4, 3, 2),
            new HomeSite("옛 진료소", "회복하기 좋은 조용한 대피처", 2, 3, 3)
        };
        public List<int> Chosen { get; } = new List<int>();
        public HomeSite Home { get; private set; }
        public int Day { get; private set; } = 1;
        public int Supplies { get; private set; }
        public int Ammo { get; private set; }
        public RunState ActiveRun { get; private set; }
        public string Message { get; private set; } = "함께 시작할 모험가 두 명을 선택하자.";
        public IEnumerable<Adventurer> Party => Chosen.Select(i => Candidates[i]);
        public bool Toggle(int i)
        {
            if (Stage != JourneyStage.Party || i < 0 || i >= Candidates.Length) return false;
            if (Chosen.Contains(i)) { Chosen.Remove(i); return true; }
            if (Chosen.Count == 2) { Message = "두 명을 이미 선택했다. 바꾸려면 먼저 한 명을 해제하자."; return false; }
            Chosen.Add(i); Message = Chosen.Count + "/2명 선택"; return true;
        }
        public bool ConfirmParty()
        {
            if (Stage != JourneyStage.Party || Chosen.Count != 2) return false;
            Stage = JourneyStage.HomeChoice; Message = "두 사람이 돌아올 정착지를 선택하자."; return true;
        }
        public bool BackToParty()
        {
            if (Stage != JourneyStage.HomeChoice) return false;
            Stage = JourneyStage.Party; return true;
        }
        public bool Settle(int index)
        {
            if (Stage != JourneyStage.HomeChoice || index < 0 || index >= Sites.Length) return false;
            Home = Sites[index]; Supplies = Home.Supplies; Ammo = Home.Ammo;
            Stage = JourneyStage.Settlement; Message = Home.Name + "에 자리를 잡았다. 첫 수색을 준비하자."; return true;
        }
        public bool Depart(Func<double> random = null)
        {
            if (Stage != JourneyStage.Settlement || !Party.Any(p => p.Health > 0)) return false;
            ActiveRun = new RunState(random, Party.ToArray(), Ammo);
            Ammo = 0; Stage = JourneyStage.Expedition; return true;
        }
        public bool Return()
        {
            if (Stage != JourneyStage.Expedition || ActiveRun == null || ActiveRun.Phase != RunPhase.Debrief) return false;
            int i = 0;
            foreach (var p in Party) { var member = ActiveRun.Squad[i++]; p.Health = member.Extracted ? member.Health : 0; }
            int recovered = ActiveRun.Supplies;
            Supplies += recovered;
            Ammo += ActiveRun.Squad.Any(p => p.Extracted) ? ActiveRun.Ammo : 0;
            Day++;
            Stage = Party.Any(p => p.Health > 0) ? JourneyStage.Settlement : JourneyStage.Lost;
            Message = "귀환 정산 · 보급품 +" + recovered + " · 남은 탄약 " + Ammo + ". 부상과 탄약은 다음 원정으로 이어진다.";
            return true;
        }
        public bool Rest()
        {
            if (Stage != JourneyStage.Settlement) return false;
            if (!Party.Any(p => p.Health > 0 && p.Health < p.MaxHealth)) { Message = "회복이 필요한 생존자가 없다."; return false; }
            if (Supplies < 1) { Message = "회복에 필요한 보급품 1개가 부족하다."; return false; }
            Supplies--;
            foreach (var p in Party.Where(p => p.Health > 0)) p.Health = Math.Min(p.MaxHealth, p.Health + Home.Recovery);
            Message = "보급품 1개 사용 · 생존 대원 체력 +" + Home.Recovery + "."; return true;
        }
        public bool PrepareAmmo()
        {
            if (Stage != JourneyStage.Settlement) return false;
            if (Supplies < 1) { Message = "탄약 정리에 필요한 보급품 1개가 부족하다."; return false; }
            Supplies--; Ammo += 2; Message = "보급품 1개 사용 · 탄약 +2. (시제품 교환 규칙)"; return true;
        }
    }
}
