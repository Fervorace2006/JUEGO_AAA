using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // Green health bar fixed to a lower corner of the view. Screen-space canvases do not reach the headset,
    // so it is an unlit quad pair parented to the camera and drawn over the scene.
    public sealed class HudHealthBar : MonoBehaviour
    {
        static readonly Color Fill = new Color(.16f, .85f, .25f);
        static readonly Color Back = new Color(.03f, .035f, .04f);
        Transform fill;
        Vector2 size;
        Mesh quad;
        Material backMaterial, fillMaterial;
        public static HudHealthBar Create(Transform head, Vector3 localPosition, Vector2 size)
        {
            var go = new GameObject("Player Health Bar");
            go.transform.SetParent(head, false); go.transform.localPosition = localPosition;
            var bar = go.AddComponent<HudHealthBar>();
            bar.size = size;
            bar.quad = new Mesh { name = "Health bar quad" };
            bar.quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
            bar.quad.triangles = new[] { 0,1,2,0,2,3 }; bar.quad.RecalculateBounds();
            float border = size.y * .25f;
            bar.backMaterial = CreateMaterial(Back, 4000);
            bar.fillMaterial = CreateMaterial(Fill, 4001);
            bar.CreateQuad("Background", bar.backMaterial, new Vector3(size.x + border * 2, size.y + border * 2, 1), Vector3.zero);
            bar.fill = bar.CreateQuad("Fill", bar.fillMaterial, new Vector3(size.x, size.y, 1), new Vector3(0, 0, -.0005f));
            return bar;
        }
        static Material CreateMaterial(Color color, int queue)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            // Always on top so hands, weapons or nearby trees cannot cover it.
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);
            material.renderQueue = queue;
            return material;
        }
        Transform CreateQuad(string name, Material material, Vector3 scale, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }
        public void SetFraction(float value)
        {
            value = Mathf.Clamp01(value);
            // Shrinks toward the left edge.
            fill.localScale = new Vector3(size.x * value, size.y, 1);
            fill.localPosition = new Vector3(-size.x * (1 - value) * .5f, 0, fill.localPosition.z);
            fill.gameObject.SetActive(value > 0);
        }
        void OnDestroy()
        {
            if (quad != null) Destroy(quad);
            if (backMaterial != null) Destroy(backMaterial);
            if (fillMaterial != null) Destroy(fillMaterial);
        }
    }
}
