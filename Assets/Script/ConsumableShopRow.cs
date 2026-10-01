using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Consumable card: icon + "SHIELD x0". Tapping the card equips/unequips it;
    // the pink frame shows which one the double tap will use.
    public sealed class ConsumableShopRow : MonoBehaviour
    {
        [SerializeField] private ConsumableKind kind;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private GameObject equippedMark;

        public void ToggleEquip()
        {
            RunInventory inventory = FindFirstObjectByType<RunInventory>();
            if (inventory != null)
                inventory.EquipConsumable(inventory.EquippedConsumable == kind ? ConsumableKind.None : kind);
        }

        public void Refresh(RunInventory inventory)
        {
            bool equipped = inventory.EquippedConsumable == kind;
            if (countText != null)
                countText.text = kind.ToString().ToUpperInvariant() + " x" +
                    (kind == ConsumableKind.Shield ? inventory.ShieldCount : inventory.DashCount);
            if (actionText != null)
                actionText.text = equipped ? "UNEQUIP" : "EQUIP";
            if (equippedMark != null)
                equippedMark.SetActive(equipped);
        }
    }
}
