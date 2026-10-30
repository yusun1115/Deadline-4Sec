using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Skin shop screen (2026-10-30):
    //  - tapping an owned card equips it at once; tapping a card not owned yet
    //    previews that weapon in the Reaper's hand, with a small
    //    "스킨 미리보기 입니다." tag; leaving the screen (or equipping) shows the
    //    equipped skin again,
    //  - BUY asks "구매하시겠습니까?" (예 / 아니오); without enough Soul it asks
    //    "Soul이 부족합니다. 재화 상점으로 이동할까요?" and 예 opens the currency
    //    shop over this screen (its Back returns here),
    //  - the shop stage (SkinShopStage) replaces the run scenery behind the Reaper.
    public sealed class SkinShopUI : MonoBehaviour
    {
        private enum Pending { None, Buy, CurrencyShop }

        [SerializeField] private GameObject previewTag;
        [Header("Confirm dialog")]
        [SerializeField] private GameObject dialog;
        [SerializeField] private TMP_Text dialogTitle;
        [SerializeField] private TMP_Text dialogMessage;
        [SerializeField] private TMP_Text yesLabel;
        [SerializeField] private CurrencyShopPanel currencyShop;

        private RunInventory inventory;
        private GameExtrasUI extras;
        private CameraFeedbackController feedback;
        private SkinShopStage stage;
        private CoinWallet wallet;
        private int previewIndex = -1;
        private SkinShopRow pendingRow;
        private Pending pending;

        public int PreviewIndex => previewIndex;
        public bool DialogOpen => dialog != null && dialog.activeSelf;

        private void OnEnable()
        {
            inventory = FindFirstObjectByType<RunInventory>();
            extras = FindFirstObjectByType<GameExtrasUI>();
            feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (currencyShop == null)
                currencyShop = FindFirstObjectByType<CurrencyShopPanel>(FindObjectsInactive.Include);
            wallet = inventory != null ? inventory.Wallet : null;
            if (dialog != null)
                foreach (UnityEngine.UI.Button button in dialog.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (button.name.EndsWith("Button") && button.targetGraphic is UnityEngine.UI.Image pill)
                    {
                        pill.sprite = FxSprites.RoundedBox;
                        pill.type = UnityEngine.UI.Image.Type.Sliced;
                        pill.pixelsPerUnitMultiplier = 1f;
                    }
            if (wallet != null)
                wallet.Changed += Refresh;
            previewIndex = -1;
            CloseDialog();
            SetPreviewTag(false);
            Refresh();
            if (stage == null)
                stage = SkinShopStage.Create();
            stage.Show(true);
        }

        private void OnDisable()
        {
            if (wallet != null)
                wallet.Changed -= Refresh;
            if (inventory != null && previewIndex >= 0)
                inventory.ShowEquippedSkin();
            previewIndex = -1;
            HighlightRows();
            CloseDialog();
            if (stage != null)
                stage.Show(false);
        }

        private void OnDestroy()
        {
            if (stage != null)
                Destroy(stage.gameObject);
        }

        public void Preview(int index)
        {
            if (inventory == null || DialogOpen)
                return;
            // An owned skin is worn straight away (no preview step).
            if (inventory.OwnsSkin(index))
            {
                if (index != inventory.EquippedSkinIndex && inventory.TryBuyOrEquipSkin(index))
                {
                    feedback?.PlayMenuResult(true);
                    foreach (SkinShopRow row in GetComponentsInChildren<SkinShopRow>(true))
                        if (row.SkinIndex == index)
                            row.Pop();
                }
                else
                    Click();
                ClearPreview();
                Refresh();
                return;
            }
            Click();
            if (index == previewIndex)
            {
                ClearPreview();
                return;
            }
            ShowPreview(index);
        }

        private void ShowPreview(int index)
        {
            previewIndex = index;
            inventory.PreviewSkin(index);
            SetPreviewTag(true);
            HighlightRows();
        }

        private void ClearPreview()
        {
            previewIndex = -1;
            if (inventory != null)
                inventory.ShowEquippedSkin();
            SetPreviewTag(false);
            HighlightRows();
        }

        public void OnCardAction(SkinShopRow row)
        {
            if (inventory == null || row == null || DialogOpen)
                return;
            int index = row.SkinIndex;
            if (inventory.OwnsSkin(index))
            {
                if (inventory.TryBuyOrEquipSkin(index))
                {
                    feedback?.PlayMenuResult(true);
                    row.Pop();
                }
                ClearPreview();
                Refresh();
                return;
            }
            // Show what is being bought while the question is up.
            if (previewIndex != index)
                ShowPreview(index);
            inventory.SkinShortfall(index, out bool soulShort, out bool currencyShort);
            if (currencyShort)
                return;
            Click();
            string name = inventory.Catalog.Skins[index].displayName;
            if (soulShort)
                OpenDialog(Pending.CurrencyShop, row, "SOUL", "Soul이 부족합니다.\n재화 상점으로 이동할까요?");
            else
                OpenDialog(Pending.Buy, row, name, "구매하시겠습니까?");
        }

        public void ConfirmYes()
        {
            Pending kind = pending;
            SkinShopRow row = pendingRow;
            CloseDialog();
            if (kind == Pending.Buy && row != null)
            {
                if (inventory.TryBuyOrEquipSkin(row.SkinIndex))
                {
                    feedback?.PlayMenuResult(true);
                    row.Pop();
                    ClearPreview();
                }
                else
                    feedback?.PlayMenuResult(false);
                Refresh();
            }
            else if (kind == Pending.CurrencyShop)
            {
                if (currencyShop != null)
                    currencyShop.OpenFromSkinShop();
            }
        }

        public void ConfirmNo()
        {
            Click();
            CloseDialog();
        }

        private void OpenDialog(Pending kind, SkinShopRow row, string title, string message)
        {
            pending = kind;
            pendingRow = row;
            if (dialogTitle != null)
                dialogTitle.text = title;
            if (dialogMessage != null)
                dialogMessage.text = message;
            if (yesLabel != null)
                yesLabel.text = "예";
            if (dialog != null)
            {
                dialog.SetActive(true);
                dialog.transform.SetAsLastSibling();
            }
        }

        private void CloseDialog()
        {
            pending = Pending.None;
            pendingRow = null;
            if (dialog != null)
                dialog.SetActive(false);
        }

        private void SetPreviewTag(bool shown)
        {
            if (previewTag != null)
                previewTag.SetActive(shown);
        }

        private void Refresh()
        {
            if (extras != null)
                extras.Refresh();
            HighlightRows();
        }

        private void HighlightRows()
        {
            foreach (SkinShopRow row in GetComponentsInChildren<SkinShopRow>(true))
                row.SetPreviewed(row.SkinIndex == previewIndex);
        }

        private void Click() => feedback?.PlayUIClick();
    }
}
