using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Info card beside the aimed or hovered infected: HP (and what is left after the hit), armor, hit chance and its terms.
    public sealed class BattleAimTooltip : MonoBehaviour
    {
        public RectTransform Root;
        public Text Title, Armor, Health, Chance, Factors, Damage, Note, Intent;
        [Tooltip("대응 요령 한 줄 (크리쳐 호버 카드). 비면 숨기고 카드 높이를 줄인다.")]
        public Text Trait;
        [Min(0)] public float TraitHeight = 34;
        public Image HealthLost, HealthAfter;
        public GameObject AttackBlock, IntentBlock;
        [Min(1)] public float AimHeight = 238, HoverHeight = 140;
        public Color Good = new Color(.62f, .86f, .66f), Bad = new Color(.95f, .5f, .4f), Plain = new Color(.95f, .92f, .82f);
        float shake;
        public void ShowHover(string title, int armor, int health, int maximum, string intent, string trait = null)
        {
            Base(title, armor, health, maximum, health); AttackBlock.SetActive(false); IntentBlock.SetActive(true);
            Intent.text = intent; bool extra = Trait && !string.IsNullOrEmpty(trait);
            if (Trait) { Trait.gameObject.SetActive(extra); Trait.text = trait ?? ""; }
            Root.sizeDelta = new Vector2(Root.sizeDelta.x, HoverHeight + (extra ? TraitHeight : 0));
        }
        public void ShowAim(string title, int armor, int health, int maximum, int after, int chance, string factors, string damage, string note, bool valid)
        {
            Base(title, armor, health, maximum, valid ? after : health); AttackBlock.SetActive(true); IntentBlock.SetActive(false);
            Chance.text = valid ? "명중률  " + chance + "%" : "공격 불가"; Chance.color = valid ? new Color(1, .8f, .42f) : Bad;
            Factors.text = factors; Damage.text = damage; Note.text = note; Note.color = valid ? (after == 0 ? Good : Plain) : Bad;
            Root.sizeDelta = new Vector2(Root.sizeDelta.x, AimHeight);
        }
        void Base(string title, int armor, int health, int maximum, int after)
        {
            gameObject.SetActive(true); Title.text = title; Armor.text = "방어력 " + armor;
            Health.text = "체력  " + health + " / " + maximum + (after < health ? "  →  " + (after <= 0 ? "쓰러짐" : after.ToString()) : "");
            HealthLost.enabled=false;
            SegmentedHealthGraphic.Set(HealthAfter,health,maximum,after);
        }
        // A short shake when the player clicks a target that cannot be attacked.
        public void Refuse() { shake = .3f; }
        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }
        void Update()
        {
            var c = HealthLost.color; c.a = .45f + .45f * Mathf.Sin(Time.unscaledTime * 9); HealthLost.color = c;
            if (shake > 0) { shake -= Time.unscaledDeltaTime; Root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(shake * 60) * 3 * shake / .3f); }
            else Root.localRotation = Quaternion.identity;
        }
    }
}
