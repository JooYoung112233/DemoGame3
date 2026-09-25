using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Name, segmented health and the next-action tags floating above one standee.
    public sealed class BattlePawnHud : MonoBehaviour
    {
        public Text Name;
        public BattleRoleBadge RoleBadge;
        public RectTransform Segments;
        public Image SegmentTemplate;
        public GameObject Aim, Intent, Danger, Guard;
        public Text AimLabel, IntentLabel, DangerLabel;
        public BattleSlashGraphic IntentClaw;
        public BattleChevronGraphic IntentArrow;
        public Color Empty = new Color(.06f, .08f, .08f, .9f), Lost = new Color(.97f, .9f, .74f), Preview = new Color(1, .93f, .7f);
        [Min(0)] public float LostHold = .35f, LostDrain = .3f;
        readonly List<Image> segments = new List<Image>();
        Color fill; int health, lost, preview, gainedFrom = -1; float lostAge = -1, gainAge = -1;
        public Color Gained = new Color(.8f, 1, .82f);
        [Min(0)] public float GainGlow = .7f;
        [Tooltip("포인터로 적-대원 관계를 확인할 때 태그가 커지는 정도")]
        [Min(0)] public float EmphasisScale = .14f;
        bool emphasized;
        // Linked by the pointer (an infected and the ally it will hit): the intent/danger tags breathe larger.
        public void Emphasize(bool on) { emphasized = on; if (!on) { Intent.transform.localScale = Vector3.one; Danger.transform.localScale = Vector3.one; } }
        public void Setup(string label, int maximum, int current, Color color)
        {
            Name.text = label; fill = color; SegmentTemplate.gameObject.SetActive(false);
            foreach (var s in segments) if (s) Destroy(s.gameObject);
            segments.Clear();
            for (int i = 0; i < Mathf.Max(1, maximum); i++) { var s = Instantiate(SegmentTemplate, Segments); s.gameObject.SetActive(true); s.name = "Segment" + (i + 1); segments.Add(s); }
            health = current; lost = 0; preview = 0; Aim.SetActive(false); Intent.SetActive(false); Danger.SetActive(false); Guard.SetActive(false); Paint();
        }
        public int Health => health;
        public void SetRole(bool enemy, BattleCreature creature)
        {
            if (!RoleBadge) return;
            RoleBadge.gameObject.SetActive(enemy);
            RoleBadge.SetRole(CreatureRoles.For(creature));
        }
        // Visible stack height in canvas pixels: bar+name, then the intent/danger tag, then the aim tag.
        public float ContentHeight => Aim.activeSelf ? 118 : Intent.activeSelf || Danger.activeSelf ? 84 : 52;
        public void SetHealth(int value, bool animate)
        {
            value = Mathf.Clamp(value, 0, segments.Count);
            if (animate && value < health) { lost = Mathf.Max(lost, health) ; lostAge = 0; } else if (!animate) lost = 0;
            if (animate && value > health) { gainedFrom = health; gainAge = 0; }
            health = value; Paint();
        }
        public void SetPreview(int damage) { if (preview == damage) return; preview = damage; Paint(); }
        public void ShowAim(string text) { Aim.SetActive(!string.IsNullOrEmpty(text)); if (AimLabel) AimLabel.text = text; }
        public void ShowDanger(string text) { Danger.SetActive(!string.IsNullOrEmpty(text)); if (DangerLabel) DangerLabel.text = text; }
        public void ShowIntent(EnemyIntentKind kind, string text, float arrowAngle)
        {
            Intent.SetActive(kind != EnemyIntentKind.None);
            if (kind == EnemyIntentKind.None) return;
            IntentLabel.text = text;
            IntentClaw.gameObject.SetActive(kind == EnemyIntentKind.Attack);
            IntentArrow.gameObject.SetActive(kind == EnemyIntentKind.Advance || kind == EnemyIntentKind.Shift);
            IntentArrow.SetAngle(arrowAngle);
        }
        void Update()
        {
            if (emphasized) { float s = 1 + EmphasisScale * (.6f + .4f * Mathf.Sin(Time.unscaledTime * 8)); Intent.transform.localScale = Danger.transform.localScale = Vector3.one * s; }
            if (gainAge >= 0) { gainAge += Time.unscaledDeltaTime; if (gainAge >= GainGlow) { gainAge = -1; gainedFrom = -1; } Paint(); }
            if (lostAge >= 0) { lostAge += Time.unscaledDeltaTime; if (lostAge >= LostHold + LostDrain) { lostAge = -1; lost = 0; } Paint(); }
            else if (preview > 0) Paint();
        }
        void Paint()
        {
            float blink = .55f + .45f * Mathf.Sin(Time.unscaledTime * 10);
            float drain = lostAge < 0 ? 1 : 1 - Mathf.Clamp01((lostAge - LostHold) / Mathf.Max(.01f, LostDrain));
            for (int i = 0; i < segments.Count; i++)
            {
                Color c = i < health ? fill : Empty;
                if (i < health && i >= health - preview) c = Color.Lerp(fill, Preview, blink);
                else if (i >= health && i < lost) c = Color.Lerp(Empty, Lost, drain);
                if (gainAge >= 0 && i >= gainedFrom && i < health) c = Color.Lerp(Gained, fill, gainAge / Mathf.Max(.01f, GainGlow));
                segments[i].color = c;
            }
        }
    }
}
