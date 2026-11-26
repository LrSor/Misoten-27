using System.Collections.Generic;
using UnityEngine;

namespace Sawada.Cordex
{
    // The terminal stays in the scene even when its data slot is empty.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FieldDataReceiver : NetworkWorldObject
    {
        [SerializeField, Min(.1f)] float pickupDistance = 2.5f;
        [SerializeField] Vector3 displayOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(.01f)] float displayScale = .5f;
        readonly HashSet<PlayerSwapController> nearbyPlayers = new HashSet<PlayerSwapController>();
        GameObject display;
        int dataId;

        public FieldDataReceiverManager Manager { get; private set; }
        public int ReceiverId { get; private set; }
        public int DataId => dataId; // 0: empty, 1..9: manager catalog
        public BaseArmorSO Data => Manager != null ? Manager.GetData(dataId) : null;
        public bool CanAcquire => isActiveAndEnabled && gameObject.activeInHierarchy
            && Manager != null && Manager.CanAcquire && Data != null;

        protected override int Value { get => dataId; set => SetData(value); }

        internal void Bind(FieldDataReceiverManager manager, int id)
        {
            Manager = manager;
            ReceiverId = id;
            var trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            var scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            trigger.radius = pickupDistance / Mathf.Max(.001f, maxScale);
        }

        public bool IsInRange(PlayerSwapController player) => player != null
            && player.gameObject.scene == gameObject.scene
            && (player.transform.position - transform.position).sqrMagnitude <= pickupDistance * pickupDistance;

        public bool TryAcquire(PlayerSwapController player, Inventory inventory)
            => Manager != null && Manager.TryAcquire(this, player, inventory);

        internal void SetData(int id)
        {
            if (id < 0 || Manager == null || id > Manager.DataCatalog.Count) return;
            if (dataId == id && (id == 0 || display != null)) return;
            ClearCandidates();
            dataId = id;
            if (display != null)
            {
                display.SetActive(false);
                if (Application.isPlaying) Destroy(display); else DestroyImmediate(display);
                display = null;
            }
            var data = Data;
            if (data == null || data.m_KARI_icon == null) return;
            display = Instantiate(data.m_KARI_icon, transform);
            display.name = "Data display";
            display.transform.localPosition = displayOffset;
            display.transform.localRotation = Quaternion.identity;
            // The existing terminal can be stretched; keep the data model at a uniform world scale.
            var scale = transform.lossyScale;
            display.transform.localScale = new Vector3(displayScale / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                displayScale / Mathf.Max(.001f, Mathf.Abs(scale.y)), displayScale / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            foreach (var collider in display.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag("PlayerSwapCollider")) return;
            var player = other.GetComponent<PlayerSwapController>();
            if (!CanAcquire || !IsInRange(player)) return;
            nearbyPlayers.Add(player);
            player.AddCanAcquireData(this);
        }

        void OnTriggerExit(Collider other)
        {
            var player = other.GetComponent<PlayerSwapController>();
            if (player == null) return;
            nearbyPlayers.Remove(player);
            player.RemoveCanAcquireData(this);
        }

        void OnDisable() => ClearCandidates();

        void ClearCandidates()
        {
            foreach (var player in nearbyPlayers)
                if (player != null) player.RemoveCanAcquireData(this);
            nearbyPlayers.Clear();
        }
    }
}
