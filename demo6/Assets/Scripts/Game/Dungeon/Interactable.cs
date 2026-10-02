using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// F로 쓰는 물체(궤짝, 등잔, 말뚝, 광맥, 금 간 벽, 이야기 물건). 가까운 것 하나만 쓴다.
    /// HoldSeconds가 0보다 크면 그 시간 동안 F를 누르고 있어야 한다(등잔 1초, 광맥 1.8초, 금고 2초). 맞거나 멀어지면 처음부터.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        /// <summary>쓸 수 있는 거리(플레이어 중심에서).</summary>
        public virtual float Range => 1.8f;
        public virtual float HoldSeconds => 0f;
        /// <summary>안내 문구 예: "열기", "등잔 켜기".</summary>
        public abstract string Prompt { get; }
        /// <summary>false면 안내도 띄우지 않는다(이미 연 궤짝).</summary>
        public virtual bool Available => true;
        /// <summary>null이 아니면 안내 대신 이 이유를 보이고 F가 듣지 않는다(예: "곡괭이가 필요하다").</summary>
        public virtual string BlockedReason => null;
        /// <summary>안내를 띄울 자리.</summary>
        public virtual Vector2 Position => transform.position;

        public abstract void Interact();

        protected virtual void OnEnable() => All.Add(this);

        protected virtual void OnDisable() => All.Remove(this);

        public static void ResetStatics() => All.Clear();
    }

    /// <summary>가장 가까운 Interactable을 고르고 F 누르기·길게 누르기를 처리한다. 안내 문구도 그린다.</summary>
    public sealed class InteractionSystem : MonoBehaviour
    {
        public static InteractionSystem Instance { get; private set; }

        public Interactable Current { get; private set; }
        public float HoldProgress => Current && Current.HoldSeconds > 0f ? Mathf.Clamp01(_held / Current.HoldSeconds) : 0f;

        float _held;
        int _hurtSeen;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var player = PlayerController.Instance;
            if (!player || player.IsDown || TimeScaleService.Paused || DungeonUi.ModalOpen)
            {
                _held = 0f;
                if (!player || player.IsDown) Current = null;
                return;
            }
            Interactable best = null;
            float bestD = float.MaxValue;
            foreach (var it in Interactable.All)
            {
                if (!it || !it.Available) continue;
                float d = (it.Position - player.Position).magnitude;
                if (d > it.Range || d >= bestD) continue;
                best = it;
                bestD = d;
            }
            if (best != Current)
            {
                Current = best;
                _held = 0f;
            }
            if (!Current) return;
            var input = player.GetComponent<PlayerInputReader>();
            if (!input || Current.BlockedReason != null)
            {
                _held = 0f;
                if (input && input.InteractPressed && Current.BlockedReason != null) DungeonEvents.Say(Current.BlockedReason);
                return;
            }
            if (Current.HoldSeconds <= 0f)
            {
                if (input.InteractPressed) Use();
                return;
            }
            // 길게 누르기: 맞으면 처음부터(켜는 1초 노출, 3차 초안 2-5).
            int hurt = player.Health.Current;
            bool gotHit = hurt < _hurtSeen;
            _hurtSeen = hurt;
            if (input.InteractHeld && !gotHit)
            {
                _held += Time.deltaTime;
                if (_held >= Current.HoldSeconds) Use();
            }
            else _held = 0f;
        }

        void Use()
        {
            var it = Current;
            _held = 0f;
            if (it) it.Interact();
        }

        void OnGUI()
        {
            if (!Current || DungeonUi.ModalOpen) return;
            DungeonUi.Begin();
            var gui = DungeonUi.WorldToGui(Current.Position + Vector2.up * 1.1f);
            if (gui == null) return;
            string reason = Current.BlockedReason;
            string text = reason ?? "[F] " + Current.Prompt + (Current.HoldSeconds > 0f ? " (누르고 있기)" : "");
            var r = new Rect(gui.Value.x - 140f, gui.Value.y - 18f, 280f, 30f);
            DungeonUi.Box(r, 0.7f);
            var prev = GUI.color;
            GUI.color = reason != null ? new Color(1f, 0.75f, 0.6f) : Color.white;
            GUI.Label(r, text, DungeonUi.Center);
            GUI.color = prev;
            if (reason == null && Current.HoldSeconds > 0f && _held > 0f)
                DungeonUi.Bar(new Rect(r.x + 10f, r.yMax + 2f, r.width - 20f, 6f), HoldProgress, new Color(1f, 0.92f, 0.7f));
        }
    }
}
