using System;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// Interactable을 직접 물려받지 않는 세계 물체(금 간 벽·자물쇠 문·분필 그림)에 붙이는 F 상호작용.
    /// 막는 물체는 계약서상 MonoBehaviour라 이 부품을 같은 물체에 붙여 문틈 가운데에서 양쪽 다 쓸 수 있게 한다.
    /// 안내 문구·누르는 시간·거리·막힌 이유·쓰기를 만든 쪽이 Setup으로 넘긴다.
    /// </summary>
    public sealed class WorldInteraction : Interactable
    {
        string _prompt = "";
        float _hold;
        float _range = 1.8f;
        Func<bool> _available;
        Func<string> _blocked;
        Action _use;

        public override string Prompt => _prompt;
        public override float HoldSeconds => _hold;
        public override float Range => _range;
        public override bool Available => _available == null || _available();
        public override string BlockedReason => _blocked != null ? _blocked() : null;

        /// <param name="available">null이면 늘 쓸 수 있다.</param>
        /// <param name="blocked">null을 돌려주면 막히지 않음(예: 곡괭이가 있으면 null).</param>
        public void Setup(string prompt, float holdSeconds, float range, Func<bool> available, Func<string> blocked, Action use)
        {
            _prompt = prompt ?? "";
            _hold = Mathf.Max(0f, holdSeconds);
            _range = range;
            _available = available;
            _blocked = blocked;
            _use = use;
        }

        public override void Interact() => _use?.Invoke();
    }
}
