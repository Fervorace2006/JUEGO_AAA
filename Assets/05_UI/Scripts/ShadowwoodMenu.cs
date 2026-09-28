using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace JuegoAAA.UI
{
    /// <summary>Horror start menu for VR: a large world-space panel in front of the player with the Shadowwood art,
    /// a flickering lantern, drifting fog, sudden scares and a low drone. Built at runtime so the scene only holds
    /// the XR rig and this component.</summary>
    public sealed class ShadowwoodMenu : MonoBehaviour
    {
        public Texture background;
        [Tooltip("Font of the buttons and texts, the same Roman capitals as the SHADOWWOOD title.")]
        public Font titleFont;
        public Font textFont;
        public string gameScene = "ForestScene";
        [Min(0.5f)] public float distance = 2.6f;
        [Tooltip("Width of the menu panel in meters.")]
        [Min(0.5f)] public float width = 3.4f;
        public float eyeHeight = 1.6f;
        [Tooltip("Where the lantern of the artwork is, in pixels from the top-left corner.")]
        public Vector2 lanternPixel = new Vector2(1190, 640);
        [Range(0, 1)] public float volume = 0.6f;

        const float W = 1536, H = 1024;
        static readonly Color Bone = new Color(.87f, .83f, .74f);
        static readonly Color Blood = new Color(.72f, .04f, .03f);

        RectTransform root;
        CanvasGroup mainGroup, controlsGroup;
        Image fade;
        RawImage glow, darkness, redPulse;
        RawImage[] fog;
        AudioSource drone, sfx;
        AudioClip heartbeat;
        float scareDarkness, scareRed;
        bool loading;

        void Start()
        {
            SetupCameraAndWorld();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<XRUIInputModule>();
            }
            BuildCanvas();
            BuildAudio();
            StartCoroutine(Fade(1, 0, 2.5f));
            StartCoroutine(Scares());
        }

        void SetupCameraAndWorld()
        {
            // A black void: the only thing to see is the menu.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.05f, .05f, .06f);
            RenderSettings.fog = true; RenderSettings.fogColor = Color.black;
            RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .08f;
            var camera = Camera.main;
            if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; }
        }

        // ---------- UI ----------

        void BuildCanvas()
        {
            var go = new GameObject("Shadowwood Menu Canvas");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, eyeHeight, distance);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(W, H);
            root.localScale = Vector3.one * (width / W);

            var art = Raw("Artwork", root, background, 0, 0, W, H);
            art.color = new Color(.92f, .92f, .92f);

            // Two layers of fog drifting at different speeds.
            var fogTexture = FogTexture();
            fog = new[] { Raw("Fog A", root, fogTexture, 0, 0, W, H), Raw("Fog B", root, fogTexture, 0, 0, W, H) };
            fog[0].color = new Color(.55f, .6f, .65f, .16f);
            fog[1].color = new Color(.4f, .45f, .5f, .12f);

            // Warm light of the lantern, flickering.
            float glowSize = 760;
            glow = Raw("Lantern Glow", root, RadialTexture(true), lanternPixel.x - glowSize / 2, lanternPixel.y - glowSize / 2, glowSize, glowSize);

            // Dark border, and a red one for the scares.
            darkness = Raw("Darkness", root, RadialTexture(false), 0, 0, W, H);
            darkness.color = new Color(0, 0, 0, .8f);
            redPulse = Raw("Red Pulse", root, RadialTexture(false), 0, 0, W, H);
            redPulse.color = new Color(.5f, 0, 0, 0);

            // Dark band at the bottom that holds the buttons.
            var band = Raw("Bottom Band", root, GradientTexture(), 0, H - 300, W, 300);
            band.color = new Color(0, 0, 0, .9f);

            mainGroup = Group("Main", root);
            var tagline = Label("Tagline", (RectTransform)mainGroup.transform, "No salgas del sendero", textFont, 30, 108, 168, 760, 50, TextAnchor.MiddleLeft);
            tagline.color = new Color(.62f, .07f, .05f);
            AddButton("JUGAR", (RectTransform)mainGroup.transform, 468, 930, Play);
            AddButton("CONTROLES", (RectTransform)mainGroup.transform, 768, 930, () => ShowControls(true));
            AddButton("SALIR", (RectTransform)mainGroup.transform, 1068, 930, Quit);

            controlsGroup = Group("Controls", root);
            var panel = Raw("Panel", (RectTransform)controlsGroup.transform, null, 268, 170, 1000, 640);
            panel.color = new Color(0, 0, 0, .86f);
            var title = Label("Controls Title", (RectTransform)controlsGroup.transform, "CONTROLES", titleFont, 58, 268, 195, 1000, 80, TextAnchor.MiddleCenter);
            title.color = Blood;
            Label("Controls Text", (RectTransform)controlsGroup.transform,
                "Grip: agarrar y soltar un arma\n" +
                "Gatillo: disparar el revólver\n" +
                "Arco: en la mano izquierda; tira de la cuerda con la derecha\n" +
                "Hacha: golpea con fuerza\n" +
                "Joystick: moverse y girar\n" +
                "Clic en el joystick izquierdo: correr (gasta aliento)\n" +
                "Si mueres, vuelves al menú y empiezas de nuevo\n\n" +
                "Los duendes duermen. No te acerques demasiado.",
                textFont, 32, 318, 290, 900, 400, TextAnchor.UpperLeft).lineSpacing = 1.15f;
            AddButton("VOLVER", (RectTransform)controlsGroup.transform, 768, 740, () => ShowControls(false));
            ShowControls(false);

            fade = new GameObject("Fade", typeof(RectTransform)).AddComponent<Image>();
            Place(fade.rectTransform, root, 0, 0, W, H);
            fade.color = Color.black; fade.raycastTarget = false;
        }

        void AddButton(string text, RectTransform parent, float centerX, float centerY, UnityEngine.Events.UnityAction onClick)
        {
            const float w = 300, h = 96;
            var go = new GameObject(text + " Button", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            Place(rect, parent, centerX - w / 2, centerY - h / 2, w, h);
            // Invisible hit area; the text is what you see.
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            var label = Label("Text", rect, text, titleFont, 46, 0, 0, w, h, TextAnchor.MiddleCenter);
            label.color = Bone;
            var line = new GameObject("Underline", typeof(RectTransform)).AddComponent<Image>();
            Place(line.rectTransform, rect, w / 2, h - 12, 0, 3);
            line.color = Blood; line.raycastTarget = false;
            go.AddComponent<MenuButtonFx>().Setup(this, label, line.rectTransform, w * .8f, Bone, Blood);
        }

        CanvasGroup Group(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place((RectTransform)go.transform, parent, 0, 0, W, H);
            return go.AddComponent<CanvasGroup>();
        }

        Text Label(string name, RectTransform parent, string text, Font font, int size, float x, float y, float w, float h, TextAnchor anchor)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            Place(label.rectTransform, parent, x, y, w, h);
            label.text = text; label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.alignment = anchor; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            // A dark shadow keeps the letters readable over the artwork.
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(3, -3);
            return label;
        }

        RawImage Raw(string name, RectTransform parent, Texture texture, float x, float y, float w, float h)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            Place(image.rectTransform, parent, x, y, w, h);
            image.texture = texture; image.raycastTarget = false;
            return image;
        }

        // Pixel rectangle measured from the parent's top-left corner, like the artwork.
        static void Place(RectTransform rect, RectTransform parent, float x, float y, float w, float h)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        void ShowControls(bool show)
        {
            SetGroup(controlsGroup, show);
            SetGroup(mainGroup, !show);
        }

        static void SetGroup(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1 : 0; group.interactable = visible; group.blocksRaycasts = visible;
        }

        // ---------- Actions ----------

        void Play()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            { Debug.LogError($"La escena '{gameScene}' no esta en Build Settings.", this); return; }
            loading = true;
            SetGroup(mainGroup, false);
            Heartbeat(1);
            StartCoroutine(LoadGame());
            ForestVR.GameAudio.Music(null, 1.5f);
        }

        IEnumerator LoadGame()
        {
            var load = SceneManager.LoadSceneAsync(gameScene);
            load.allowSceneActivation = false;
            yield return Fade(0, 1, 1.6f);
            load.allowSceneActivation = true;
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds)));
                yield return null;
            }
            SetFade(to);
        }

        void SetFade(float alpha)
        {
            fade.color = new Color(0, 0, 0, alpha);
            fade.enabled = alpha > .001f;
            if (drone != null) drone.volume = volume * .55f * (1 - alpha * .8f);
        }

        // ---------- Atmosphere ----------

        void Update()
        {
            if (root == null) return;
#if UNITY_EDITOR
            // The XR simulator turns off mouse clicks on UI while its devices are active: Enter plays, Escape closes the controls.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && !loading)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Play();
                if (keyboard.escapeKey.wasPressedThisFrame) ShowControls(false);
            }
#endif
            float t = Time.time;
            // Lantern: a steady warm glow with small fast flickers, killed during a scare.
            float flicker = .75f + (Mathf.PerlinNoise(t * 7, 0) - .5f) * .5f + (Mathf.PerlinNoise(t * 23, 3) - .5f) * .2f;
            glow.color = new Color(1f, .6f, .2f, Mathf.Clamp01(flicker * .42f * (1 - scareDarkness)));
            darkness.color = new Color(0, 0, 0, Mathf.Lerp(.72f, .98f, scareDarkness) + (1 - flicker) * .08f);
            redPulse.color = new Color(.55f, 0, 0, scareRed * .75f);
            fog[0].uvRect = new Rect(t * .012f, Mathf.Sin(t * .1f) * .02f, 1, 1);
            fog[1].uvRect = new Rect(-t * .008f + .37f, .5f + Mathf.Cos(t * .07f) * .03f, 1.4f, 1.2f);
            scareRed = Mathf.MoveTowards(scareRed, 0, Time.deltaTime * .8f);
        }

        // Every so often the lantern dies, the view turns red for a moment and a heartbeat sounds.
        IEnumerator Scares()
        {
            yield return new WaitForSeconds(Random.Range(5f, 8f));
            while (true)
            {
                for (int i = Random.Range(1, 4); i > 0; i--)
                {
                    scareDarkness = 1;
                    yield return new WaitForSeconds(Random.Range(.05f, .18f));
                    scareDarkness = .3f;
                    yield return new WaitForSeconds(Random.Range(.04f, .12f));
                }
                scareDarkness = 1;
                scareRed = 1;
                Heartbeat(.9f);
                yield return new WaitForSeconds(Random.Range(.3f, .7f));
                for (float t = 0; t < .6f; t += Time.deltaTime) { scareDarkness = 1 - t / .6f; yield return null; }
                scareDarkness = 0;
                yield return new WaitForSeconds(Random.Range(8f, 15f));
            }
        }

        public void OnButtonHover()
        {
            scareRed = Mathf.Max(scareRed, .35f);
            Heartbeat(.35f);
        }

        void Heartbeat(float strength)
        {
            if (sfx != null) sfx.PlayOneShot(heartbeat, volume * strength);
        }

        void BuildAudio()
        {
            drone = gameObject.AddComponent<AudioSource>();
            drone.clip = DroneClip(); drone.loop = true; drone.spatialBlend = 0; drone.volume = 0;
            drone.Play();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.spatialBlend = 0;
            heartbeat = HeartbeatClip();
            // Music from Assets/05_Sounds (Resources/GameAudio), under the generated drone.
            var audio = ForestVR.GameAudio.Get;
            if (audio != null && audio.menuMusic != null) ForestVR.GameAudio.Music(audio.menuMusic, 4);
        }

        // Low beating tones and dark wind, 8 seconds that loop seamlessly.
        static AudioClip DroneClip()
        {
            const int rate = 22050; const float seconds = 8;
            int n = (int)(rate * seconds);
            var data = new float[n];
            float wind = 0, windSlow = 0;
            var random = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Whole number of cycles in 8 s so the loop has no click.
                float tone = Mathf.Sin(2 * Mathf.PI * 55f * t) * .35f + Mathf.Sin(2 * Mathf.PI * 58.25f * t) * .3f
                    + Mathf.Sin(2 * Mathf.PI * 82.5f * t) * .12f + Mathf.Sin(2 * Mathf.PI * 110.125f * t) * .05f;
                float swell = .6f + .4f * Mathf.Sin(2 * Mathf.PI * t / seconds);
                wind += ((float)random.NextDouble() * 2 - 1 - wind) * .02f;
                windSlow += (wind - windSlow) * .05f;
                float gust = .5f + .5f * Mathf.Sin(2 * Mathf.PI * 2 * t / seconds + 1);
                data[i] = tone * swell * .5f + windSlow * 6f * gust;
            }
            // Blend the ends of the wind noise.
            int blend = rate / 4;
            for (int i = 0; i < blend; i++) { float k = i / (float)blend; data[i] = data[i] * k + data[n - blend + i] * (1 - k); }
            var clip = AudioClip.Create("Menu Drone", n - blend, 1, rate, false);
            var trimmed = new float[n - blend];
            System.Array.Copy(data, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }

        static AudioClip HeartbeatClip()
        {
            const int rate = 22050;
            int n = (int)(rate * .9f);
            var data = new float[n];
            foreach (var (start, gain) in new[] { (0f, 1f), (.28f, .7f) })
                for (int i = (int)(start * rate); i < n; i++)
                {
                    float t = i / (float)rate - start;
                    data[i] += Mathf.Sin(2 * Mathf.PI * (48 + 30 * Mathf.Exp(-t * 30)) * t) * Mathf.Exp(-t * 14) * gain * .9f;
                }
            var clip = AudioClip.Create("Heartbeat", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------- Procedural textures ----------

        // Center-weighted blob (lantern glow) or its inverse: clear center, solid border (vignette).
        static Texture2D RadialTexture(bool glow)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2 - 1, v = y / (size - 1f) * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = glow ? Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f)
                        : Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4) + Mathf.Pow(Mathf.Abs(v), 4), .25f) - .45f) / .6f));
                    pixels[y * size + x] = new Color(1, 1, 1, a);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        static Texture2D GradientTexture()
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) texture.SetPixel(0, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, y / 63f)));
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D FogTexture()
        {
            const int w = 256, h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Tileable noise: sample on a torus.
                    float a = x / (float)w * Mathf.PI * 2, b = y / (float)h * Mathf.PI * 2;
                    float n = Mathf.PerlinNoise(10 + Mathf.Cos(a) * 1.3f, 10 + Mathf.Sin(a) * 1.3f + Mathf.Cos(b) * .9f) * .65f
                        + Mathf.PerlinNoise(30 + Mathf.Cos(a) * 3f + Mathf.Sin(b) * 2f, 30 + Mathf.Sin(a) * 3f) * .35f;
                    // Heavier near the ground (bottom of the view).
                    float ground = Mathf.Lerp(1, .35f, y / (float)h);
                    pixels[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01((n - .35f) * 2.2f) * ground);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }
    }
}
