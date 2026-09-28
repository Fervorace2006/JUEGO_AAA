using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoAAA.UI
{
    /// <summary>Scary hover on a menu button: the text turns blood red, grows and trembles, and a red line spreads under it.</summary>
    public sealed class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        ShadowwoodMenu menu;
        Text label;
        RectTransform underline;
        float lineWidth, hover;
        Color normal, danger;
        bool inside;
        Vector2 labelHome;
        // Optional backing plate of the button: it warms to a dark blood red while hovered.
        Graphic plate;
        Color plateNormal, plateHover;

        public void Setup(ShadowwoodMenu owner, Text text, RectTransform line, float fullLine, Color normalColor, Color hoverColor)
        {
            menu = owner; label = text; underline = line; lineWidth = fullLine; normal = normalColor; danger = hoverColor;
            labelHome = label.rectTransform.anchoredPosition;
        }

        public void SetPlate(Graphic backing, Color hoverColor)
        {
            plate = backing; plateNormal = backing.color; plateHover = hoverColor;
        }

        public void OnPointerEnter(PointerEventData eventData) { inside = true; menu.OnButtonHover(); }
        public void OnPointerExit(PointerEventData eventData) => inside = false;
        void OnDisable() { inside = false; hover = 0; Apply(); }

        void Update()
        {
            hover = Mathf.MoveTowards(hover, inside ? 1 : 0, Time.unscaledDeltaTime * 6);
            Apply();
        }

        void Apply()
        {
            if (label == null) return;
            label.color = Color.Lerp(normal, danger, hover);
            if (plate != null) plate.color = Color.Lerp(plateNormal, plateHover, hover);
            label.rectTransform.localScale = Vector3.one * (1 + .1f * hover);
            // Nervous tremble while hovered.
            label.rectTransform.anchoredPosition = labelHome + (inside ? Random.insideUnitCircle * 2.5f : Vector2.zero);
            float w = lineWidth * hover;
            underline.sizeDelta = new Vector2(w, underline.sizeDelta.y);
            underline.anchoredPosition = new Vector2(((RectTransform)transform).sizeDelta.x / 2 - w / 2, underline.anchoredPosition.y);
        }
    }
}
