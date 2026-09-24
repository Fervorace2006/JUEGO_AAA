using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // Small, unlit world-space signs remain readable in the dark without lighting the forest.
    public sealed class VRWorldLabel : MonoBehaviour
    {
        public TextMesh Text { get; private set; }
        Transform panel;
        Material panelMaterial;
        Mesh panelMesh;
        bool fitPending;
        Vector2 panelSize;
        public static VRWorldLabel Create(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.AddComponent<VRWorldLabel>();
            label.Text = new GameObject("Text").AddComponent<TextMesh>();
            label.Text.transform.SetParent(go.transform, false);
            label.Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.Text.fontSize = 64; label.Text.characterSize = 0.014f;
            label.Text.anchor = TextAnchor.MiddleCenter; label.Text.alignment = TextAlignment.Center;
            label.Text.color = Color.white;
            var renderer = label.Text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = label.Text.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var panelObject = new GameObject("Background");
            label.panel = panelObject.transform; label.panel.SetParent(go.transform, false);
            label.panel.localPosition = new Vector3(0, 0, 0.002f);
            label.panelMesh = new Mesh { name = "Hint background" };
            label.panelMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
            label.panelMesh.triangles = new[] { 0,1,2,0,2,3 }; label.panelMesh.RecalculateBounds();
            panelObject.AddComponent<MeshFilter>().sharedMesh = label.panelMesh;
            var panelRenderer = panelObject.AddComponent<MeshRenderer>();
            label.panelMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            label.panelMaterial.SetColor("_BaseColor", new Color(.025f,.03f,.035f));
            panelRenderer.sharedMaterial = label.panelMaterial;
            panelRenderer.shadowCastingMode = ShadowCastingMode.Off; panelRenderer.receiveShadows = false;
            return label;
        }
        public void Show(string content, float width, float height)
        {
            Text.text = content;
            panel.localScale = new Vector3(width, height, 1);
            panelSize = new Vector2(width, height);
            fitPending = true;
        }
        void LateUpdate()
        {
            if (!fitPending || Text == null) return;
            var size = Text.GetComponent<MeshRenderer>().localBounds.size;
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Min((panelSize.x - .025f) / size.x, (panelSize.y - .014f) / size.y);
            Text.transform.localScale = Vector3.one * scale;
            fitPending = false;
        }
        void OnDestroy()
        {
            if (panelMaterial != null) Destroy(panelMaterial);
            if (panelMesh != null) Destroy(panelMesh);
        }
    }
}
