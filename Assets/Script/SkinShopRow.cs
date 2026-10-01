using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    public sealed class SkinShopRow : MonoBehaviour
    {
        [SerializeField] private int skinIndex;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Image swatch;
        [SerializeField] private GameObject equippedMark;

        public void BuyOrEquip()
        {
            RunInventory inventory = FindFirstObjectByType<RunInventory>();
            if (inventory != null)
                inventory.TryBuyOrEquipSkin(skinIndex);
        }

        public void Refresh(RunInventory inventory)
        {
            if (inventory.Catalog == null || skinIndex >= inventory.Catalog.Skins.Length)
                return;
            GameExtrasCatalog.Skin skin = inventory.Catalog.Skins[skinIndex];
            bool owned = inventory.OwnsSkin(skinIndex);
            bool equipped = inventory.EquippedSkinIndex == skinIndex;
            if (nameText != null)
                nameText.text = skin.displayName;
            if (priceText != null)
            {
                priceText.text = owned ? "OWNED" :
                    (skin.coinCost > 0 ? skin.coinCost.ToString("N0") + " COIN" : "") +
                    (skin.currencyCost > 0 ? "  " + skin.currency + " " +
                        inventory.CurrencyCount(skin.currency) + "/" + skin.currencyCost : "");
                if (skinIndex == 0)
                    priceText.text = "DEFAULT";
            }
            if (actionText != null)
                actionText.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : "BUY";
            if (actionButton != null)
                actionButton.interactable = !equipped;
            // Cards tint the scythe icon with the skin's blade color.
            if (swatch != null && skin.weaponMaterial != null && skin.weaponMaterial.HasProperty("_BaseColor"))
            {
                Color c = skin.weaponMaterial.GetColor("_BaseColor");
                c.a = owned ? 1f : 0.45f;
                swatch.color = c;
            }
            if (equippedMark != null)
                equippedMark.SetActive(equipped);
        }
    }
}
