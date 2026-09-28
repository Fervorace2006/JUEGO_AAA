using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JuegoAAA.VR
{
    /// <summary>A head-mounted spotlight follows the tracked camera without smoothing lag.</summary>
    [RequireComponent(typeof(XROrigin))]
    public sealed class VRHeadFlashlight : MonoBehaviour
    {
        [SerializeField, Min(0f)] float intensity = 8f;
        [SerializeField, Min(1f)] float range = 40f;
        [SerializeField, Range(1f, 179f)] float coneAngle = 130f;
        [SerializeField] Color lightColor = new Color(1f, 0.94f, 0.82f);
        [SerializeField] Vector3 headOffset = new Vector3(0f, 0.08f, 0.06f);
        Light flashlight;
        static readonly Color NightAmbient = new Color(0.07f, 0.085f, 0.14f), NightSky = new Color(0.03f, 0.04f, 0.075f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForForest()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForestLight();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "ForestScene") EnsureForestLight();
        }

        static void EnsureForestLight()
        {
            foreach (var origin in FindObjectsByType<XROrigin>())
            {
                if (origin.gameObject.scene.name != "ForestScene") continue;
                var component = origin.GetComponent<VRHeadFlashlight>();
                if (component == null) component = origin.gameObject.AddComponent<VRHeadFlashlight>();
                component.enabled = true;
            }
        }

        void ApplyNight()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Dim blue moonlight over the whole map, so the night is dark but not pitch black. The probe is what
            // actually lights the objects, so it gets the same color.
            RenderSettings.ambientLight = NightAmbient;
            RenderSettings.ambientIntensity = 0f;
            var moonlight = new SphericalHarmonicsL2();
            moonlight.AddAmbientLight(NightAmbient);
            RenderSettings.ambientProbe = moonlight;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = NightSky;
            RenderSettings.fogDensity = 0.025f;
            foreach (var light in FindObjectsByType<Light>())
                if (light.gameObject.scene == gameObject.scene && light != flashlight)
                    light.enabled = false;
        }

        void Start()
        {
            ApplyNight();
            var camera = GetComponent<XROrigin>().Camera;
            if (camera == null)
            {
                Debug.LogError("The head flashlight requires the XR Origin camera.", this);
                return;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            // Moonlight and a moon in the sky, after the other scene lights were switched off.
            NightMoon.Create(camera.transform);
            var lamp = new GameObject("Head Flashlight");
            lamp.transform.SetParent(camera.transform, false);
            lamp.transform.localPosition = headOffset;
            lamp.transform.localRotation = Quaternion.identity;
            flashlight = lamp.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.color = lightColor;
            flashlight.intensity = intensity;
            flashlight.range = range;
            flashlight.spotAngle = coneAngle;
            // A wide full-strength core so the sides are lit too, not just a round spot in the middle.
            flashlight.innerSpotAngle = coneAngle * 0.85f;
            // Hard shadows: soft filtering over the whole lit view was too costly on the Quest.
            flashlight.shadows = LightShadows.Hard;
            flashlight.shadowStrength = 1f;
            flashlight.shadowBias = 0.025f;
            flashlight.shadowNormalBias = 0.15f;
            flashlight.shadowNearPlane = 0.05f;
            flashlight.bounceIntensity = 0f;
            flashlight.renderMode = LightRenderMode.ForcePixel;
            flashlight.enabled = enabled;
            Debug.Log("Forest night active: head flashlight attached to " + camera.name, this);
        }

        void OnEnable() { if (flashlight != null) flashlight.enabled = true; }
        void OnDisable() { if (flashlight != null) flashlight.enabled = false; }
        void OnDestroy() { if (flashlight != null) Destroy(flashlight.gameObject); }
    }
}
