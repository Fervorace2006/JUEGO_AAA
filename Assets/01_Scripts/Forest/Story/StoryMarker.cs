using UnityEngine;
using UnityEngine.UI;

namespace ForestVR
{
    // Red diamond floating over the current objective with its distance. It keeps the same apparent size at any
    // distance, faces the player, bobs gently and hides when the player is already there.
    public sealed class StoryMarker : MonoBehaviour
    {
        Transform head, target;
        float height;
        Text distance;
        RectTransform diamond;
        CanvasGroup group;

        public static StoryMarker Create(Transform head, Material overlay, Font font)
        {
            var go = new GameObject("Story Marker");
            var marker = go.AddComponent<StoryMarker>();
            marker.head = head;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(200, 200);
            marker.group = go.AddComponent<CanvasGroup>();

            var image = new GameObject("Diamond", typeof(RectTransform)).AddComponent<Image>();
            marker.diamond = image.rectTransform;
            marker.diamond.SetParent(rect, false);
            marker.diamond.sizeDelta = new Vector2(46, 46);
            marker.diamond.anchoredPosition = new Vector2(0, 20);
            marker.diamond.localRotation = Quaternion.Euler(0, 0, 45);
            image.color = new Color(.8f, .08f, .05f, .9f); image.material = overlay; image.raycastTarget = false;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .9f); outline.effectDistance = new Vector2(3, -3);

            marker.distance = new GameObject("Distance", typeof(RectTransform)).AddComponent<Text>();
            marker.distance.rectTransform.SetParent(rect, false);
            marker.distance.rectTransform.sizeDelta = new Vector2(200, 50);
            marker.distance.rectTransform.anchoredPosition = new Vector2(0, -38);
            marker.distance.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            marker.distance.fontSize = 30; marker.distance.alignment = TextAnchor.MiddleCenter;
            marker.distance.color = new Color(.9f, .86f, .77f); marker.distance.material = overlay; marker.distance.raycastTarget = false;
            go.SetActive(false);
            return marker;
        }

        public void Track(Transform newTarget, float heightAbove)
        {
            target = newTarget; height = heightAbove;
            gameObject.SetActive(target != null);
        }

        void LateUpdate()
        {
            if (target == null || head == null) { gameObject.SetActive(false); return; }
            var position = target.position + Vector3.up * (height + Mathf.Sin(Time.time * 2) * .08f);
            float meters = Vector3.Distance(head.position, target.position);
            // Constant apparent size: about 2.5 degrees for the diamond.
            float viewDistance = Vector3.Distance(head.position, position);
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(position - head.position);
            transform.localScale = Vector3.one * (viewDistance * .0009f);
            distance.text = Mathf.RoundToInt(meters) + " m";
            group.alpha = Mathf.Clamp01((meters - 2.5f) / 2f);
        }
    }
}
