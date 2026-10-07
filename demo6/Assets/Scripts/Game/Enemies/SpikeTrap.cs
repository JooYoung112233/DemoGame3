using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 3차 초안 3-4 궁수 가시 덫: 놓인 뒤 0.6초 동안 빨간 예고가 차오르고, 그 뒤 밟으면 궁수 공격력의 100%(약 5.6%).
    /// 구르기 무적이면 그냥 지나간다. 한 번 밟히면 사라지고, 8초 뒤에도 사라진다.
    /// 3차 예고 규칙(검토 1차 Q6, TelegraphRule): 0.6초는 바탕값이고, 밟을 한 방의 최소 예고가 더 길면 늘린다(1~10층 4.5~7.5%라 최소 0.4초, 지금은 늘 0.6초).
    /// 10%를 넘으면 놓일 때 예고 시작 소리를 낸다(지금 수치로는 나지 않음).
    /// 예고는 주인 없는 바닥 예고다(시야와 문 1차 4-5 정리): 놓은 궁수가 안 보여도 덫 자리 빨간 원은 늘 그린다.
    /// </summary>
    public sealed class SpikeTrap : MonoBehaviour
    {
        const float ArmTime = 0.6f;
        const float Radius = 0.7f;
        const float Life = 8f;
        /// <summary>밟으면 궁수 공격력의 100%.</summary>
        const float HitPercent = 100f;

        static readonly System.Collections.Generic.List<SpikeTrap> Live = new System.Collections.Generic.List<SpikeTrap>();

        int _attack;
        float _age;
        /// <summary>예고가 다 차 덫이 서는 시각(놓인 뒤 초). 바탕 0.6초, 규칙에 걸리면 늘어난다.</summary>
        float _armTime = ArmTime;
        Telegraph _telegraph;
        SpriteRenderer _spikes;

        /// <param name="attack">놓은 궁수의 공격력(층·정예 배율 포함).</param>
        /// <param name="floor">놓은 궁수의 층(예고 규칙의 기준 플레이어 체력·방어).</param>
        public static void Place(Vector2 position, int attack, int floor)
        {
            var go = new GameObject("SpikeTrap");
            go.transform.position = position;
            var trap = go.AddComponent<SpikeTrap>();
            Live.Add(trap);
            trap._attack = attack;
            trap._armTime = Demo6.Core.Combat.TelegraphRule.Seconds(ArmTime, attack, HitPercent, floor);
            // 1-2층 탐험 맛 1차 4-5 정리(합친 계획 물결 3): 바닥에 놓인 덫의 예고는 바닥 표시라 주인 없는 예고로 만든다(낙석 빨간 원과 같이 늘 보임).
            // 궁수의 Think 안에서 불리므로 CreatingOwner가 궁수인데, 이 줄 동안만 비우고 되돌린다(궁수가 안 보여도 덫 자리는 그린다).
            var owner = Telegraph.CreatingOwner;
            Telegraph.CreatingOwner = null;
            try
            {
                trap._telegraph = Telegraph.Circle(position, Radius, trap._armTime);
            }
            finally
            {
                Telegraph.CreatingOwner = owner;
            }
            if (Demo6.Core.Combat.TelegraphRule.NeedsSound(attack, HitPercent, floor)) Telegraph.PlayStartCue(TelegraphCue.Trap);
            var spikes = new GameObject("Spikes");
            spikes.transform.SetParent(go.transform, false);
            spikes.transform.localScale = Vector3.one * (Radius * 2f);
            trap._spikes = spikes.AddComponent<SpriteRenderer>();
            trap._spikes.sprite = ShapeSprites.Ring;
            trap._spikes.color = Palette.Trap;
            trap._spikes.sortingOrder = -55;
            trap._spikes.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age < _armTime) return;
            if (_telegraph)
            {
                _telegraph.Resolve();
                _telegraph = null;
                _spikes.enabled = true;
            }
            if (_age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            var player = PlayerController.Instance;
            if (!player || player.IsDown) return;
            if ((player.Position - (Vector2)transform.position).magnitude > Radius + PlayerController.Radius * 0.5f) return;
            // 덫(방패 표 2-7 Trap): 방패로 못 막고 무기 행동은 끊긴다. 회피 반격 창은 열지 않는다(예전 그대로).
            if (player.ReceiveHit(_attack, HitPercent, transform.position, 0.4f, false, Demo6.Core.Combat.HitKind.Trap)) Destroy(gameObject);
        }

        void OnDestroy()
        {
            Live.Remove(this);
            if (_telegraph) _telegraph.Cancel();
        }

        /// <summary>구성을 바꾸거나 다시 세울 때 남은 덫을 지운다.</summary>
        public static void DestroyAll()
        {
            foreach (var t in Live.ToArray())
                if (t) Destroy(t.gameObject);
            Live.Clear();
        }
    }
}
