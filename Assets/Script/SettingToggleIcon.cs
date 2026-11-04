using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Square sound / vibration buttons in Settings (2026-11-04): the icon shows
    // the saved state (ON: speaker with waves / buzzing phone, OFF: speaker
    // with an X / still phone). The button's own On Click still calls
    // GameFlowManager.ToggleSound / ToggleVibration; this only redraws after it.
    [RequireComponent(typeof(Button))]
    public sealed class SettingToggleIcon : MonoBehaviour
    {
        public enum Setting { Sound, Vibration }

        [SerializeField] private Setting setting;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;
        [Tooltip("Icon tint while OFF (the stripe and outline dim with it).")]
        [SerializeField] private Color offTint = new Color(0.62f, 0.55f, 0.66f, 1f);

        private Graphic stripe;
        private Outline outline;
        private Color stripeOn, outlineOn;

        public bool IsOn => setting == Setting.Sound ? GamePreferences.SoundEnabled : GamePreferences.VibrationEnabled;

        private void Awake()
        {
            // Runtime listeners run after the persistent On Click (the toggle).
            GetComponent<Button>().onClick.AddListener(Refresh);
            Transform stripeTransform = transform.Find("Stripe");
            if (stripeTransform != null && stripeTransform.TryGetComponent(out stripe))
                stripeOn = stripe.color;
            if (TryGetComponent(out outline))
                outlineOn = outline.effectColor;
        }

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            bool on = IsOn;
            if (icon != null)
            {
                Sprite sprite = on ? onSprite : offSprite;
                if (sprite != null)
                    icon.sprite = sprite;
                icon.color = on ? Color.white : offTint;
            }
            if (stripe != null)
                stripe.color = on ? stripeOn : stripeOn * offTint;
            if (outline != null)
                outline.effectColor = on ? outlineOn : new Color(outlineOn.r, outlineOn.g, outlineOn.b, outlineOn.a * 0.45f);
        }
    }
}
