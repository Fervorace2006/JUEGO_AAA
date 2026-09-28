using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace JuegoAAA.VR
{
    /// <summary>The XR Interaction Toolkit creates its keyboard-and-mouse simulator in every Play in the editor
    /// (XRI settings: automatically instantiate simulator). With a real headset connected (Quest Link / Air Link) its
    /// fake controllers replace the real ones: the hands float in front of the face and ignore the Touch controllers.
    /// As soon as a real headset is running, this removes the simulator and its simulated devices. Without a headset
    /// nothing changes, so the game can still be tried on the PC.</summary>
    public sealed class XRSimulatorGuard : MonoBehaviour
    {
        static XRSimulatorGuard instance;
        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        static readonly List<UnityEngine.XR.InputDevice> heads = new List<UnityEngine.XR.InputDevice>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Application.isEditor || instance != null) return;
            instance = new GameObject("XR Simulator Guard").AddComponent<XRSimulatorGuard>();
            DontDestroyOnLoad(instance.gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => instance.StartCoroutine(instance.Watch());
            instance.StartCoroutine(instance.Watch());
        }

        // The headset session can start a few seconds after Play; keep checking for a while after each scene load.
        IEnumerator Watch()
        {
            for (float t = 0; t < 8; t += .25f)
            {
                if (RealHeadsetRunning()) { RemoveSimulator(); yield break; }
                yield return new WaitForSecondsRealtime(.25f);
            }
        }

        public static bool RealHeadsetRunning()
        {
            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            bool running = false;
            foreach (var display in displays) running |= display.running;
            if (!running) return false;
            // Simulated devices live only in the Input System; XR input devices at the head node are real ones.
            heads.Clear();
            InputDevices.GetDevicesAtXRNode(XRNode.Head, heads);
            foreach (var head in heads) if (head.isValid) return true;
            return false;
        }

        static void RemoveSimulator()
        {
            int removed = 0;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour == null) continue;
                var type = behaviour.GetType().Name;
                if (type != "XRInteractionSimulator" && type != "XRDeviceSimulator") continue;
                Destroy(behaviour.gameObject);
                removed++;
            }
            // Its fake headset and controllers must go too, or they keep overriding the real ones.
            var fakes = new List<UnityEngine.InputSystem.InputDevice>();
            foreach (var device in InputSystem.devices)
                if (device.GetType().Name.StartsWith("XRSimulated")) fakes.Add(device);
            foreach (var device in fakes) InputSystem.RemoveDevice(device);
            if (removed > 0 || fakes.Count > 0)
                Debug.Log($"Visor real detectado: simulador XR desactivado ({removed} simulador, {fakes.Count} dispositivos simulados).");
        }
    }
}
