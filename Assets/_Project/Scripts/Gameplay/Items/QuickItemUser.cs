using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// Uses consumables: the quick-use button eats the first food/potion in the bag;
    /// the inventory screen can use a specific one.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Combatant))]
    public sealed class QuickItemUser : MonoBehaviour
    {
        [SerializeField, Tooltip("Real-time seconds between uses, so potions can't be spammed.")] float cooldown = 0.8f;

        Inventory inventory;
        Combatant combatant;
        Stamina stamina;
        PlayerCombat combat;
        InputAction useAction;
        float lastUseTime = float.NegativeInfinity;

        /// <summary>What the quick-use button will consume next.</summary>
        public ConsumableData QuickItem => inventory.FindFirst<ConsumableData>();

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            combatant = GetComponent<Combatant>();
            stamina = GetComponent<Stamina>();
            combat = GetComponent<PlayerCombat>();
        }

        void Start() => useAction = Services.Get<InputService>().UseItem;

        void Update()
        {
            if (useAction.WasPressedThisFrame()) TryUse(QuickItem);
        }

        public bool TryUse(ConsumableData item)
        {
            if (item == null || combatant.IsDead) return false;
            if (Time.unscaledTime - lastUseTime < cooldown) return false;
            if (combat != null && !combat.CanUseItems) return false;
            if (!inventory.Remove(item, 1)) return false;

            combatant.Heal(item.Heal);
            if (stamina != null) stamina.Restore(item.Stamina);
            lastUseTime = Time.unscaledTime;
            EventBus<ItemUsedEvent>.Raise(new ItemUsedEvent(item));
            return true;
        }
    }
}
