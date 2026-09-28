using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // One of Mateo's three notebook fragments. It falls from the last enemy in each encounter
    // and is picked up by walking near it.
    public sealed class PagePickup : MonoBehaviour
    {
        // One piece per enemy encounter, assigned by StoryDirector.
        public static int Required = 3;
        public static int Collected { get; private set; }
        public static int OnGround { get; private set; }
        public static event System.Action<int> PageCollected;
        public static bool WantsMore => Collected + OnGround < Required;

        const float PickupRadius = 1.1f;
        Vector3 groundPosition;
        Transform viewer;
        Rigidbody body;
        Collider solid;
        bool taken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Required = 3; Collected = 0; OnGround = 0; PageCollected = null; }

        public static void ResetCount() { Collected = 0; }

        // Drops the actual notebook model above safe ground so physics lets it fall naturally.
        public static PagePickup TryDrop(Vector3 position, GameObject prefab)
        {
            if (!WantsMore) return null;
            var ground = position;
            if (PlayArea.TryGetWalkableGround(position, out var g) || GroundSafety.TryGetSurface(position, out g)) ground = g;
            if (prefab == null) prefab = Resources.Load<GameObject>("DiaryPage");
            var go = prefab != null ? Instantiate(prefab) : Placeholder();
            go.name = "Pieza de las notas de Mateo";
            var page = go.GetComponent<PagePickup>();
            if (page == null) page = go.AddComponent<PagePickup>();
            page.groundPosition = ground;
            page.solid = go.GetComponent<Collider>();
            if (page.solid == null)
            {
                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                box.size = bounds.size;
                page.solid = box;
            }
            page.solid.enabled = true;
            page.body = go.GetComponent<Rigidbody>();
            if (page.body == null) page.body = go.AddComponent<Rigidbody>();
            page.body.isKinematic = false;
            page.body.useGravity = true;
            page.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.transform.position = ground + Vector3.up * 1.2f;
            OnGround++;
            return page;
        }

        void Start()
        {
            var camera = Camera.main;
            viewer = camera != null ? camera.transform : null;
        }

        void Update()
        {
            if (taken) return;
            if (viewer == null) { var camera = Camera.main; if (camera != null) viewer = camera.transform; return; }
            // A mesh floor can occasionally be crossed by a fast rigidbody; recover vertically in place.
            if (transform.position.y < groundPosition.y - .15f)
            {
                float halfHeight = solid != null ? solid.bounds.extents.y : .03f;
                transform.position = new Vector3(transform.position.x, groundPosition.y + halfHeight + .02f, transform.position.z);
                if (body != null) body.linearVelocity = Vector3.zero;
            }
            // Let the notebook reach the floor before proximity can collect it.
            if (transform.position.y > groundPosition.y + .3f) return;
            var delta = viewer.position - transform.position;
            if (Mathf.Abs(delta.y) > 2.5f) return;
            delta.y = 0;
            if (delta.magnitude <= PickupRadius) Take();
        }

        void Take()
        {
            taken = true;
            OnGround = Mathf.Max(0, OnGround - 1);
            Collected++;
            var audio = GameAudio.Get;
            if (audio != null && audio.pagePickup != null) GameAudio.PlayAt(audio.pagePickup, transform.position, 1, 1, 10);
            PageCollected?.Invoke(Collected);
            Destroy(gameObject);
        }

        void OnDestroy() { if (!taken) OnGround = Mathf.Max(0, OnGround - 1); }

        // Stand-in until the real model: an aged paper sheet that glows faintly, with a small warm light.
        static GameObject Placeholder()
        {
            var root = new GameObject();
            var sheet = new GameObject("Sheet");
            sheet.transform.SetParent(root.transform, false);
            sheet.transform.localScale = new Vector3(.22f, .3f, 1);
            var mesh = new Mesh { name = "Page" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sheet.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Page" };
            material.SetColor("_BaseColor", new Color(.93f, .88f, .72f));
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.type = LightType.Point; glow.range = 2.5f; glow.intensity = 1.5f; glow.color = new Color(1f, .85f, .55f);
            glow.shadows = LightShadows.None; glow.renderMode = LightRenderMode.ForceVertex;
            return root;
        }
    }
}
