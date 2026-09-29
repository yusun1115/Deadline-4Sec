using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    public sealed class ConsumableShopRow : MonoBehaviour
    {
        [SerializeField] private ConsumableKind kind;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text actionText;

        public void ToggleEquip()
        {
            RunInventory inventory = FindFirstObjectByType<RunInventory>();
            if (inventory != null)
                inventory.EquipConsumable(inventory.EquippedConsumable == kind ? ConsumableKind.None : kind);
        }

        public void Refresh(RunInventory inventory)
        {
            if (countText != null)
                countText.text = kind.ToString().ToUpperInvariant() + " x" +
                    (kind == ConsumableKind.Shield ? inventory.ShieldCount : inventory.DashCount);
            if (actionText != null)
                actionText.text = inventory.EquippedConsumable == kind ? "UNEQUIP" : "EQUIP";
        }
    }
}
