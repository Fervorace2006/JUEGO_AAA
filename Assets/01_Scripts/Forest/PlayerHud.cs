using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ForestVR
{
    // The player's health and stamina, low in the view and centered, where a glance down finds them without covering
    // the aim. It is fixed to the view (always in the same place, no drifting) and is drawn
    // over everything.
    // Health: green → amber → red as it drops; a pale strip shows the damage just taken and drains after a moment;
    // the bar flashes and shakes on each hit, glows on healing, and pulses red when life is low.
    // Stamina: a golden bar that empties while running and turns red and blinks when exhausted.
    // When everything is full and quiet the panel fades back so it does not distract.
    public sealed class PlayerHud : MonoBehaviour
    {
        const float Distance = .7f, Drop = .21f, PixelsPerMeter = 1450;
        const float BarWidth = 520, HealthHeight = 38, StaminaHeight = 17;
        static readonly Vector2 HealthRowPosition = new Vector2(0, 30), StaminaRowPosition = new Vector2(0, -20);
        static readonly Color Full = new Color(.2f, .85f, .3f), Mid = new Color(.95f, .7f, .15f), Low = new Color(.9f, .1f, .08f);
        static readonly Color Gold = new Color(1f, .78f, .25f), Tired = new Color(.65f, .12f, .08f);
        static readonly Color Bone = new Color(.9f, .86f, .77f);

        Transform head;
        Health health;
        PlayerStamina stamina;
        Material overlay;
        CanvasGroup group;
        RectTransform healthRow, healthFill, healthChip, staminaFill, staminaRow;
        RawImage healthFillImage, healthChipImage, staminaFillImage, glow, flash;
        Text healthText;
        float shown = 1, chip = 1, chipHoldUntil, shake, flashAmount, flashIsHeal, lastActivity, alpha = 1;
        float lastHealth = -1;

        public static PlayerHud Create(Transform head, Health health, PlayerStamina stamina)
        {
            var hud = new GameObject("Player HUD").AddComponent<PlayerHud>();
            hud.head = head; hud.health = health; hud.stamina = stamina;
            hud.Build();
            health.onHealthChanged.AddListener(hud.OnHealthChanged);
            hud.OnHealthChanged(health.Current);
            return hud;
        }

        void Build()
        {
            var textFont = Resources.Load<Font>("Fonts/Cinzel");
            overlay = new Material(Canvas.GetDefaultCanvasMaterial());
            overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            group = gameObject.AddComponent<CanvasGroup>();
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(780, 180);
            rect.localScale = Vector3.one / PixelsPerMeter;
            Attach();

            var soft = SoftTexture();
            var shade = ShadeTexture();
            // Dark backing plate, and a red glow behind it for low health.
            glow = Raw("Low Health Glow", transform, soft, new Vector2(0, 6), new Vector2(760, 175), new Color(.8f, 0, 0, 0));
            Raw("Backing", transform, soft, new Vector2(0, 6), new Vector2(730, 140), new Color(0, 0, 0, .7f));

            // Health.
            healthRow = Rect("Health", transform, HealthRowPosition, new Vector2(BarWidth, HealthHeight));
            Raw("Frame", healthRow, null, Vector2.zero, new Vector2(BarWidth + 6, HealthHeight + 6), new Color(.05f, .03f, .03f, .95f));
            Raw("Empty", healthRow, shade, Vector2.zero, new Vector2(BarWidth, HealthHeight), new Color(.18f, .05f, .05f, .9f));
            healthChipImage = Raw("Damage Taken", healthRow, shade, Vector2.zero, new Vector2(BarWidth, HealthHeight), new Color(1f, .9f, .7f, .85f));
            healthChip = healthChipImage.rectTransform; LeftAnchor(healthChip);
            healthFillImage = Raw("Fill", healthRow, shade, Vector2.zero, new Vector2(BarWidth, HealthHeight), Full);
            healthFill = healthFillImage.rectTransform; LeftAnchor(healthFill);
            flash = Raw("Flash", healthRow, null, Vector2.zero, new Vector2(BarWidth, HealthHeight), new Color(1, 1, 1, 0));
            // Tick marks every quarter so the amount reads at a glance.
            for (int i = 1; i < 4; i++)
                Raw("Tick", healthRow, null, new Vector2(-BarWidth / 2 + BarWidth * i / 4f, 0), new Vector2(2, HealthHeight), new Color(0, 0, 0, .45f));
            Label("Health Label", healthRow, textFont, "VIDA", 28, new Vector2(-BarWidth / 2 - 56, 0), new Vector2(110, 46), TextAnchor.MiddleCenter);
            healthText = Label("Health Value", healthRow, textFont, "100", 30, new Vector2(BarWidth / 2 + 50, 0), new Vector2(100, 46), TextAnchor.MiddleCenter);

            // Stamina, thinner, under the health bar.
            staminaRow = Rect("Stamina", transform, StaminaRowPosition, new Vector2(BarWidth, StaminaHeight));
            Raw("Frame", staminaRow, null, Vector2.zero, new Vector2(BarWidth + 4, StaminaHeight + 4), new Color(.05f, .04f, .02f, .95f));
            Raw("Empty", staminaRow, shade, Vector2.zero, new Vector2(BarWidth, StaminaHeight), new Color(.15f, .12f, .05f, .9f));
            staminaFillImage = Raw("Fill", staminaRow, shade, Vector2.zero, new Vector2(BarWidth, StaminaHeight), Gold);
            staminaFill = staminaFillImage.rectTransform; LeftAnchor(staminaFill);
            Label("Stamina Label", staminaRow, textFont, "ALIENTO", 19, new Vector2(-BarWidth / 2 - 56, 0), new Vector2(110, 34), TextAnchor.MiddleCenter);
            if (stamina == null) staminaRow.gameObject.SetActive(false);
        }

        void OnHealthChanged(float value)
        {
            float fraction = Mathf.Clamp01(value / health.Maximum);
            if (lastHealth >= 0)
            {
                if (value < lastHealth)
                {
                    // The pale strip keeps the old amount for a moment, then drains to the new one.
                    chip = Mathf.Max(chip, shown);
                    chipHoldUntil = Time.time + .6f;
                    shake = 1; flashAmount = 1; flashIsHeal = 0;
                }
                else if (value > lastHealth) { flashAmount = 1; flashIsHeal = 1; chip = fraction; }
            }
            lastHealth = value;
            lastActivity = Time.time;
            healthText.text = Mathf.CeilToInt(value).ToString();
        }

        void LateUpdate()
        {
            if (head == null || health == null) return;
            float dt = Time.deltaTime, t = Time.time;
            float fraction = Mathf.Clamp01(health.Current / health.Maximum);

            // Health fill glides to its value; the damage strip waits, then follows.
            shown = Mathf.MoveTowards(shown, fraction, dt * 2.5f);
            if (Time.time >= chipHoldUntil) chip = Mathf.MoveTowards(chip, shown, dt * .6f);
            chip = Mathf.Max(chip, shown);
            SetWidth(healthFill, shown * BarWidth);
            SetWidth(healthChip, chip * BarWidth);
            var color = fraction > .6f ? Color.Lerp(Mid, Full, (fraction - .6f) / .4f) : Color.Lerp(Low, Mid, Mathf.Clamp01((fraction - .25f) / .35f));
            bool critical = fraction <= .3f && !health.IsDead;
            // Heartbeat pulse when life is low: faster the lower it gets.
            float beat = critical ? Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.Lerp(7, 4, fraction / .3f))), 6) : 0;
            healthFillImage.color = Color.Lerp(color, Color.white, beat * .25f);
            glow.color = new Color(.85f, 0, 0, critical ? .25f + beat * .45f : 0);
            healthRow.localScale = Vector3.one * (1 + beat * .04f);

            flashAmount = Mathf.MoveTowards(flashAmount, 0, dt * 3);
            flash.color = flashIsHeal > .5f ? new Color(.6f, 1f, .6f, flashAmount * .6f) : new Color(1f, .95f, .9f, flashAmount * .7f);
            shake = Mathf.MoveTowards(shake, 0, dt * 4);
            healthRow.anchoredPosition = HealthRowPosition + Random.insideUnitCircle * (shake * 5);

            // Stamina.
            if (stamina != null)
            {
                SetWidth(staminaFill, stamina.Fraction * BarWidth);
                bool blink = stamina.IsExhausted && Mathf.Repeat(t * 3, 1) < .5f;
                staminaFillImage.color = stamina.IsExhausted ? Color.Lerp(Tired, Tired * .6f, blink ? 1 : 0)
                    : stamina.IsSprinting ? Color.Lerp(Gold, Color.white, .2f + .1f * Mathf.Sin(t * 12)) : Gold;
                if (stamina.IsSprinting || stamina.Fraction < .999f) lastActivity = t;
            }
            if (fraction < .999f) lastActivity = Mathf.Max(lastActivity, t - 3.5f);

            // Fade back when there is nothing to tell.
            bool quiet = t - lastActivity > 4 && !critical;
            alpha = Mathf.MoveTowards(alpha, quiet ? .8f : 1, dt * (quiet ? .5f : 4));
            group.alpha = alpha;
        }

        // Fixed low in the view, tilted to face the eyes: it never drifts or sways, always in the same spot.
        void Attach()
        {
            transform.SetParent(head, false);
            var local = new Vector3(0, -Drop, Distance);
            transform.localPosition = local;
            transform.localRotation = Quaternion.LookRotation(local, Vector3.up);
        }

        // ---------- Building blocks ----------

        RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        RawImage Raw(string name, Transform parent, Texture texture, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.color = color; image.material = overlay; image.raycastTarget = false;
            return image;
        }

        Text Label(string name, Transform parent, Font font, string text, int size, Vector2 position, Vector2 box, TextAnchor anchor)
        {
            var label = Rect(name, parent, position, box).gameObject.AddComponent<Text>();
            label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = size; label.alignment = anchor; label.color = Bone;
            label.material = overlay; label.raycastTarget = false; label.fontStyle = FontStyle.Bold;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black; outline.effectDistance = new Vector2(2, -2);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(2, -2);
            return label;
        }

        // Fills grow from the left edge of their bar.
        static void LeftAnchor(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = Vector2.zero;
        }

        static void SetWidth(RectTransform rect, float width) => rect.sizeDelta = new Vector2(Mathf.Max(0, width), rect.sizeDelta.y);

        // Lighter at the top, darker at the bottom: gives the bars some volume.
        static Texture2D ShadeTexture()
        {
            var texture = new Texture2D(1, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
            {
                float v = y / 15f;
                float light = Mathf.Lerp(.7f, 1.15f, v) + (v > .7f && v < .85f ? .15f : 0);
                texture.SetPixel(0, y, new Color(Mathf.Min(1, light), Mathf.Min(1, light), Mathf.Min(1, light), 1));
            }
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D SoftTexture()
        {
            const int w = 64, h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs(x / (w - 1f) * 2 - 1), v = Mathf.Abs(y / (h - 1f) * 2 - 1);
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, Mathf.Max((u - .8f) / .2f, (v - .55f) / .45f))));
                }
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            if (health != null) health.onHealthChanged.RemoveListener(OnHealthChanged);
            if (overlay != null) Destroy(overlay);
        }
    }
}
