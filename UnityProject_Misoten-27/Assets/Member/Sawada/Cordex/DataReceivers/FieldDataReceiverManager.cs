using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sawada.Cordex
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class FieldDataReceiverManager : MonoBehaviour
    {
        public const int RequiredDataTypes = 9;
        [SerializeField] BaseArmorSO[] dataCatalog = new BaseArmorSO[RequiredDataTypes];
        FieldDataReceiver[] receivers = Array.Empty<FieldDataReceiver>();
        bool initialized, networkMode, networkHost, acquiring;

        public IReadOnlyList<BaseArmorSO> DataCatalog => dataCatalog;
        public IReadOnlyList<FieldDataReceiver> Receivers => receivers;
        public bool CanAcquire => initialized && isActiveAndEnabled && (!networkMode || networkHost);

        void Awake() => CollectReceivers();
        void Start()
        {
            if (!networkMode) InitializeField();
        }

        internal void CollectReceivers()
        {
            receivers = GetComponentsInChildren<FieldDataReceiver>(true)
                .Where(r => r.GetComponentInParent<FieldDataReceiverManager>(true) == this).ToArray();
            for (int i = 0; i < receivers.Length; i++) receivers[i].Bind(this, i + 1);
        }

        public BaseArmorSO GetData(int id) => id > 0 && id <= dataCatalog.Length ? dataCatalog[id - 1] : null;

        public bool InitializeField()
        {
            if (initialized) return true;
            if (networkMode && !networkHost) return false;
            CollectReceivers();
            if (dataCatalog.Length != RequiredDataTypes || dataCatalog.Any(d => d == null)
                || dataCatalog.Distinct().Count() != RequiredDataTypes)
            {
                Debug.LogError("[DataReceiver] Set exactly nine distinct BaseArmorSO assets.", this);
                return false;
            }
            if (receivers.Length < RequiredDataTypes + 1 || receivers.Any(r => !r.isActiveAndEnabled || !r.gameObject.activeInHierarchy))
            {
                Debug.LogError("[DataReceiver] At least ten enabled terminals are required under this manager.", this);
                return false;
            }
            var shuffled = (FieldDataReceiver[])receivers.Clone();
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = temp;
            }
            foreach (var receiver in receivers) receiver.SetData(0);
            for (int i = 0; i < shuffled.Length - 1; i++)
                shuffled[i].SetData(i < RequiredDataTypes ? i + 1 : UnityEngine.Random.Range(1, RequiredDataTypes + 1));
            initialized = true;
            return true;
        }

        public bool TryAcquire(FieldDataReceiver source, PlayerSwapController player, Inventory inventory)
        {
            if (!CanAcquire || acquiring || source == null || source.Manager != this || !source.CanAcquire
                || !source.IsInRange(player) || inventory == null || !player.OwnsPickupInventory(inventory)
                || inventory.ArmorSlotCount <= 0 || inventory.CurrentArmorIndex < 0
                || inventory.CurrentArmorIndex >= inventory.ArmorSlotCount) return false;
            var empty = receivers.Where(r => r != null && r != source && r.isActiveAndEnabled
                && r.gameObject.activeInHierarchy && r.DataId == 0).ToArray();
            if (empty.Length == 0) return false;

            var counts = new int[RequiredDataTypes + 1];
            foreach (var receiver in receivers)
                if (receiver != null && receiver != source && receiver.isActiveAndEnabled
                    && receiver.gameObject.activeInHierarchy && receiver.DataId > 0) counts[receiver.DataId]++;
            var missing = Enumerable.Range(1, RequiredDataTypes).Where(id => counts[id] == 0).ToArray();
            // More than one missing type cannot be repaired by one replacement: do not consume data.
            if (missing.Length > 1) return false;
            var destination = empty[UnityEngine.Random.Range(0, empty.Length)];
            int replacement = missing.Length == 1 ? missing[0] : UnityEngine.Random.Range(1, RequiredDataTypes + 1);
            acquiring = true;
            try
            {
                inventory.SwapArmor(source.Data);
                source.SetData(0);
                destination.SetData(replacement);
                return true;
            }
            finally { acquiring = false; }
        }

        internal void ConfigureNetwork()
        {
            networkMode = true;
            ResetNetworkField();
        }

        internal void SetNetworkAuthority(bool host)
        {
            networkHost = host;
            if (host) InitializeField();
        }

        internal void ResetNetworkField()
        {
            initialized = networkHost = acquiring = false;
            foreach (var receiver in receivers) if (receiver != null) receiver.SetData(0);
        }
    }
}
