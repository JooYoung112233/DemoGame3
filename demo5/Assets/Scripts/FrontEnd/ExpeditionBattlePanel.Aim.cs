using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Card-game style aiming: after picking 근접/사격 an arrow follows the pointer from the acting ally,
    // snaps onto an infected with bracket marks and an info card, and a click there attacks.
    // Right click or Esc puts the arrow away. Hovering an infected without aiming shows the lighter card.
    public sealed partial class ExpeditionBattlePanel
    {
        [Header("조준 · 카드 게임식 화살표와 정보")]
        public RectTransform AimLayer;
        public BattleAimArrow AimArrow;
        public BattleReticle Reticle;
        public BattleAimTooltip AimTooltip;
        public Color AimReady = new Color(1, .86f, .55f, .95f), AimBlocked = new Color(.9f, .45f, .36f, .9f), AimIdle = new Color(.95f, .92f, .82f, .55f);
        [Min(0)] public float ReticlePadding = 14, TooltipGap = 18, BoardTop = 150, BoardBottom = 730;
        public string AimHint = "적 위에 화살표를 올려 확인하고 누르면 바로 공격 · 우클릭/Esc 취소";
        public bool Aiming => aiming;
        public int AimTarget => aimTarget;
        bool aiming, pointerKnown;
        // Review captures steer the arrow with a scripted pointer (canvas pixels) instead of the real mouse.
        [System.NonSerialized] public bool ScriptedPointer;
        [System.NonSerialized] public Vector2 ScriptedPointerPosition;
        public Vector2 AimPoint(int unit) => unit >= 0 && unit < views.Count && views[unit] ? LayerPoint(AimLayer ? AimLayer : Frame, views[unit].Chest) : Vector2.zero;
        int aimTarget = -1;
        Vector2 pointer;

        void StopAim() { aiming = false; aimTarget = -1; armed = -1; }
        void HideAim()
        {
            if (AimArrow) AimArrow.gameObject.SetActive(false);
            if (Reticle) Reticle.gameObject.SetActive(false);
            if (AimTooltip) AimTooltip.Hide();
        }
        void Update()
        {
            if (!IsOpen || State == null) return;
            var mouse = Mouse.current;
            // The pointer only steers the arrow while the game window has focus (automation and background play ignore it).
            pointerKnown = false;
            if (ScriptedPointer) { pointerKnown = true; pointer = ScriptedPointerPosition; return; }
            if (mouse == null || !Application.isFocused) return;
            var screen = mouse.position.ReadValue();
            pointerKnown = RectTransformUtility.ScreenPointToLocalPointInRectangle(AimLayer ? AimLayer : Frame, screen, canvasCamera, out pointer);
            if (itemMode) { if (mouse.rightButton.wasPressedThisFrame && ItemInput) CloseItems(); return; }
            if (!aiming || !CanInput) return;
            if (mouse.rightButton.wasPressedThisFrame) { StopAim(); Refresh(); return; }
            if (mouse.leftButton.wasReleasedThisFrame && aimTarget >= 0 && PointerOnOpenFloor(screen)) ConfirmAim(aimTarget);
        }
        // Open floor above or around the boards (no cell, no UI button). Cell clicks confirm through ClickCell instead.
        static bool PointerOnOpenFloor(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            if (hits.Count == 0) return false;
            var top = hits[0].gameObject;
            return !top.GetComponent<BattleBoardCell>() && !top.GetComponentInParent<Button>();
        }
        void ConfirmAim(int target)
        {
            if (!CanInput) return;
            if (!State.CanAttack(target, Ranged)) { if (AimTooltip) AimTooltip.Refuse(); if (Presentation) Presentation.Refuse(); return; }
            SelectedTarget = target; Attack();
        }
        Rect BodyRect(int unit)
        {
            if (unit < 0 || unit >= bodies.Count || !views[unit]) return Rect.zero;
            var v = views[unit];
            var motion = LayerPoint(AimLayer, v.transform.position) - LayerPoint(AimLayer, world.transform.TransformPoint(v.Home));
            var r = bodies[unit]; r.position += motion; return r;
        }
        // The infected under the pointer (nearer lanes win overlaps); otherwise the one on the hovered cell.
        int HoveredEnemy()
        {
            if (!CanInput) return -1;
            int best = -1, bestLane = -1;
            if (pointerKnown)
                for (int i = 0; i < State.Units.Count && i < bodies.Count; i++)
                {
                    var u = State.Units[i]; if (!u.Enemy || !u.Alive || down[i] || !views[i]) continue;
                    var r = BodyRect(i); r.xMin -= 10; r.xMax += 10;
                    if (r.Contains(pointer) && views[i].Lane > bestLane) { best = i; bestLane = views[i].Lane; }
                }
            if (best < 0 && hover >= 9) { int d = hover % 3, l = hover % 9 / 3; best = State.Units.FindIndex(u => u.Enemy && u.Alive && u.Depth == d && u.Lane == l); }
            return best;
        }
        void PlaceAim()
        {
            if (!AimLayer) return;
            int hovered = HoveredEnemy(); bool showAim = aiming && CanInput;
            if (!showAim) aimTarget = -1;
            else if (hovered != aimTarget)
            {
                aimTarget = hovered;
                if (hovered >= 0) { SelectedTarget = hovered; if (Presentation) Presentation.Select(hovered); }
                Refresh();
            }
            bool ready = hovered >= 0 && State.CanAttack(hovered, Ranged);
            if (AimArrow)
            {
                bool arrow = showAim && (pointerKnown || hovered >= 0) && State.Actor < views.Count && views[State.Actor];
                if (AimArrow.gameObject.activeSelf != arrow) AimArrow.gameObject.SetActive(arrow);
                if (arrow)
                {
                    AimArrow.Set(LayerPoint(AimLayer, views[State.Actor].Chest), hovered >= 0 ? LayerPoint(AimLayer, views[hovered].Chest) : pointer);
                    AimArrow.color = hovered < 0 ? AimIdle : ready ? AimReady : AimBlocked;
                }
            }
            if (Reticle)
            {
                bool mark = showAim && hovered >= 0;
                if (Reticle.gameObject.activeSelf != mark) Reticle.gameObject.SetActive(mark);
                if (mark)
                {
                    var body = BodyRect(hovered);
                    Reticle.rectTransform.anchoredPosition = body.center; Reticle.rectTransform.sizeDelta = body.size + Vector2.one * ReticlePadding * 2;
                    Reticle.color = ready ? AimReady : AimBlocked;
                }
            }
            if (AimTooltip)
            {
                if (hovered < 0) AimTooltip.Hide();
                else { FillTooltip(hovered, showAim); PlaceTooltip(hovered); }
            }
        }
        void FillTooltip(int i, bool aim)
        {
            var u = State.Units[i]; int armor = State.ArmorOf(i, Ranged); var c = u.Creature;
            if (!aim)
            {
                var intents = State.PredictIntents(); int found = intents.FindIndex(p => p.Enemy == i);
                AimTooltip.ShowHover(u.Name, armor, shown[i], u.Maximum, "다음 행동 · " + (found >= 0 ? IntentLine(intents[found]) : "-"), c != null ? c.Counter : null);
                return;
            }
            bool valid = State.CanAttack(i, Ranged);
            int chance = State.HitChance(i, Ranged), damage = State.ExpectedDamage(i, Ranged), critical = State.ExpectedDamage(i, Ranged, true);
            string factors = string.Join(" · ", State.HitFactors(i, Ranged).Select((f, k) => f.Key + " " + (k > 0 && f.Value > 0 ? "+" : "") + f.Value));
            string note;
            if (!valid)
                note = Ranged ? (arrival.Inventory.CountFor(State.Current.Person, "ammo") == 0 ? "이 대원의 가방에 탄약이 없습니다" : "사격할 수 없습니다")
                    : State.Current.Depth != 0 ? "전열에 서야 근접할 수 있습니다" : "근접 거리 밖 · 앞줄 좌우 " + Rules.LaneReach + "줄까지";
            else if (c != null && c.FrontArmor > 0 && u.Stagger == 0 && State.Current.Lane == u.Lane)
                note = "정면은 문짝이 막음 · 옆 줄에서 치세요";
            else
                note = (damage >= shown[i] ? "처치 가능" : critical >= shown[i] ? "급소면 처치" : !Ranged && Rules.CriticalMeleePush ? "급소면 한 줄 밀어냄" : "처치 불가")
                    + (u.Stagger > 0 ? "  ·  빈틈 · 방어 0" : Ranged ? "  ·  탄약 1 · 소음 +" + Rules.ShotNoise : "  ·  조용함");
            AimTooltip.ShowAim(u.Name, armor, shown[i], u.Maximum, Math.Max(0, shown[i] - damage), chance, factors,
                "피해 " + damage + "  ·  급소 " + Rules.CriticalChance + "% (" + critical + ")", note, valid);
        }
        string IntentLine(EnemyIntent intent)
        {
            var c = State.Units[intent.Enemy].Creature; string attack = c != null ? c.AttackName : "공격";
            string Names() => intent.HitCount == 0 ? "빈 칸" : string.Join(", ", intent.Hits.Select(h => State.Units[h.Target].Name));
            switch (intent.Kind)
            {
                case EnemyIntentKind.Attack: return (intent.Attack == CreatureAttack.Swarm && c != null ? attack : "물기") + " → " + Names() + " " + intent.Chance + "%";
                case EnemyIntentKind.Advance: return "전진";
                case EnemyIntentKind.Shift: return "옆 줄로 이동";
                case EnemyIntentKind.Wait: return intent.Resting ? "숨을 고름" : "틈을 노림";
                case EnemyIntentKind.Windup: return (c != null ? c.WindupName : "준비") + " → 다음에 " + attack;
                case EnemyIntentKind.Strike: return intent.Attack == CreatureAttack.Broadcast ? attack + " · 소음 +" + Rules.BroadcastNoise : attack + " → " + Names();
                case EnemyIntentKind.Recover: return "빈틈 · 이번 차례를 거름";
                default: return "-";
            }
        }
        // Beside the standee on the side with room, kept between the top band and the feedback strip.
        void PlaceTooltip(int i)
        {
            var body = BodyRect(i); var root = AimTooltip.Root; float w = root.sizeDelta.x, h = root.sizeDelta.y;
            float bottom = Mathf.Min(BoardBottom, 646);
            float top = Mathf.Clamp(body.yMax + 10, -bottom + h, -BoardTop);
            float right = Mathf.Clamp(body.xMax + TooltipGap, 16, Mathf.Max(16, Frame.rect.width - w - 16));
            float left = Mathf.Clamp(body.xMin - TooltipGap - w, 16, Mathf.Max(16, Frame.rect.width - w - 16));
            float Overlap(float x)
            {
                var card = new Rect(x, top - h, w, h); float score = 0;
                for (int unit = 0; unit < views.Count; unit++)
                {
                    if (!State.Units[unit].Alive) continue;
                    var other = BodyRect(unit);
                    score += Mathf.Max(0, Mathf.Min(card.xMax, other.xMax) - Mathf.Max(card.xMin, other.xMin))
                        * Mathf.Max(0, Mathf.Min(card.yMax, other.yMax) - Mathf.Max(card.yMin, other.yMin));
                }
                return score;
            }
            root.anchoredPosition = new Vector2(Overlap(left) < Overlap(right) ? left : right, top);
        }
    }
}
