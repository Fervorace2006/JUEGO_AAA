using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
namespace ForestVR
{
    [DefaultExecutionOrder(-20)]
    public sealed class PlayerWeaponLoadout : MonoBehaviour
    {
        public WeaponGrip bowPrefab, axePrefab, revolverPrefab;
        Transform head, belt;
        readonly List<WeaponGrip> weapons = new List<WeaponGrip>();
        readonly List<VRWorldLabel> labels = new List<VRWorldLabel>();
        void Start()
        {
            int weaponLayer = LayerMask.NameToLayer("Weapons");
            if (weaponLayer >= 0)
            {
                foreach (var caster in GetComponentsInChildren<SphereInteractionCaster>(true)) caster.physicsLayerMask |= 1 << weaponLayer;
                foreach (var caster in GetComponentsInChildren<CurveInteractionCaster>(true)) caster.raycastMask |= 1 << weaponLayer;
            }
            var health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();
            var camera = GetComponentInChildren<Camera>(true);
            if (camera == null) { Debug.LogError("PlayerWeaponLoadout necesita la camara del jugador.", this); return; }
            head = camera.transform;
            var status = GetComponent<PlayerCombatStatus>();
            if (status == null) status = gameObject.AddComponent<PlayerCombatStatus>();
            status.Initialize(health, head);
            belt = new GameObject("Weapon Holsters").transform;
            belt.SetParent(transform, false); FollowHead();
            Add(bowPrefab, health, "Arco", new Vector3(-0.34f,0,0.50f), Vector3.zero);
            Add(axePrefab, health, "Hacha", new Vector3(0,-0.08f,0.50f), new Vector3(0,0,160));
            Add(revolverPrefab, health, "Revolver", new Vector3(0.34f,0,0.50f), new Vector3(60,0,0));
        }
        void Add(WeaponGrip prefab, Health owner, string label, Vector3 position, Vector3 angles)
        {
            if (prefab == null) return;
            var socket = new GameObject(label).transform; socket.SetParent(belt, false);
            socket.localPosition = position; socket.localRotation = Quaternion.Euler(angles);
            var weapon = Instantiate(prefab, socket.position, socket.rotation);
            weapon.Configure(owner, socket);
            weapons.Add(weapon);
            var hint = VRWorldLabel.Create(belt, label + " - ayuda", position + new Vector3(0, .13f, 0));
            hint.Show(label + "\nGrip: agarrar", .21f, .075f);
            labels.Add(hint);
        }
        void LateUpdate()
        {
            if (belt == null) return;
            FollowHead();
            for (int i = 0; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(weapons[i] != null && !weapons[i].IsHeld);
                labels[i].transform.rotation = Quaternion.LookRotation(labels[i].transform.position - head.position, Vector3.up);
            }
        }
        void FollowHead()
        {
            belt.position = head.position - Vector3.up * 0.45f;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.01f) belt.rotation = Quaternion.LookRotation(forward);
        }
        void OnDisable() { foreach (var weapon in weapons) if (weapon != null) weapon.gameObject.SetActive(false); }
        void OnEnable() { foreach (var weapon in weapons) if (weapon != null) weapon.gameObject.SetActive(true); }
        void OnDestroy() { foreach (var weapon in weapons) if (weapon != null) Destroy(weapon.gameObject); }
    }
}
