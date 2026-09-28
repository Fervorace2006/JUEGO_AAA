using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // Name and health bar floating over an enemy's head, always facing the player. Unlit so it reads in the dark;
    // it keeps normal depth, so walls and trees hide it. Colour and size come from the enemy's settings.
    public sealed class EnemyNameplate : MonoBehaviour
    {
        const float BarWidth = .55f, BarHeight = .055f, Border = .012f, HideBeyond = 40;
        static Mesh quad;
        static Material backMaterial;
        GoblinActor actor;
        Health health;
        Transform viewer, fill, content;
        Material fillMaterial;
        float headHeight;

        public static EnemyNameplate Create(GoblinActor actor, Transform viewer)
        {
            var settings = actor.settings;
            var go = new GameObject("Nameplate");
            go.transform.SetParent(actor.transform, false);
            var plate = go.AddComponent<EnemyNameplate>();
            plate.actor = actor; plate.health = actor.Health; plate.viewer = viewer;
            var body = actor.GetComponent<CharacterController>();
            float scale = settings.nameplateScale;
            plate.headHeight = (body != null ? body.center.y + body.height * .5f : 1.6f) + .3f * scale;
            plate.content = new GameObject("Content").transform;
            plate.content.SetParent(go.transform, false);
            plate.content.localScale = Vector3.one * scale;

            var text = new GameObject("Name").AddComponent<TextMesh>();
            text.transform.SetParent(plate.content, false);
            text.transform.localPosition = new Vector3(0, BarHeight * .5f + .09f, 0);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64; text.characterSize = .012f; text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.color = Color.white; text.text = settings.displayName;
            var textRenderer = text.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = text.font.material;
            textRenderer.shadowCastingMode = ShadowCastingMode.Off; textRenderer.receiveShadows = false;

            if (quad == null)
            {
                quad = new Mesh { name = "Nameplate quad" };
                quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
                quad.triangles = new[] { 0,1,2,0,2,3 }; quad.RecalculateBounds();
            }
            if (backMaterial == null) backMaterial = CreateMaterial(new Color(.03f, .035f, .04f));
            plate.fillMaterial = CreateMaterial(settings.healthBarColor);
            plate.CreateQuad("Background", backMaterial, new Vector3(BarWidth + Border * 2, BarHeight + Border * 2, 1), Vector3.zero);
            // Slightly toward the viewer so it draws over the background.
            plate.fill = plate.CreateQuad("Fill", plate.fillMaterial, new Vector3(BarWidth, BarHeight, 1), new Vector3(0, 0, -.002f));
            plate.health.onHealthChanged.AddListener(plate.Refresh);
            plate.Refresh(plate.health.Current);
            plate.LateUpdate();
            return plate;
        }
        static Material CreateMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Nameplate" };
            material.SetColor("_BaseColor", color);
            return material;
        }
        Transform CreateQuad(string name, Material material, Vector3 scale, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(content, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }
        void Refresh(float value)
        {
            float fraction = Mathf.Clamp01(value / health.Maximum);
            // Shrinks toward the left edge.
            fill.localScale = new Vector3(BarWidth * fraction, BarHeight, 1);
            fill.localPosition = new Vector3(-BarWidth * (1 - fraction) * .5f, 0, fill.localPosition.z);
            fill.gameObject.SetActive(fraction > 0);
        }
        void LateUpdate()
        {
            // Hidden once dead and when far away, so the dark forest is not full of floating labels.
            bool show = health != null && !health.IsDead && viewer != null
                && Vector3.Distance(viewer.position, transform.position) <= HideBeyond;
            if (content.gameObject.activeSelf != show) content.gameObject.SetActive(show);
            // Upright over the head regardless of the body's animation or turning.
            transform.SetPositionAndRotation(actor.transform.position + Vector3.up * headHeight, Quaternion.identity);
            if (!show) return;
            var away = transform.position - viewer.position; away.y = 0;
            if (away.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(away);
        }
        void OnDestroy()
        {
            if (health != null) health.onHealthChanged.RemoveListener(Refresh);
            if (fillMaterial != null) Destroy(fillMaterial);
        }
    }
}
