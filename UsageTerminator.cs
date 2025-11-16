using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Duckov.Modding;
using Duckov.UI;
using ItemStatsSystem;
using Duckov.Utilities;
using DuckovCoreAPI;
using NodeCanvas.Tasks.Actions;
using System.Xml.Linq;
using Duckov.Economy;
using System.Security.Cryptography;

namespace UsageTerminator
{
    // 1. 【關鍵修正】把 ModSetting 初始化邏輯改為「協程重試」，解決競態條件
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // --- 設定 ---
        private KeyCode _terminatorKey = KeyCode.K;
        private const string SETTING_KEY_BIND = "TerminatorTriggerKey";
        private bool _isModSettingRegistered = false;
        // --- UI ---
        private Item _hoveredItem;
        private GameObject _usagePanel;
        private GameObject _canvasGo;
        private TextMeshProUGUI _hoverHintText; 

        private static Sprite _whitePixelSprite;

        // 舊的 _nameKeyToIdCache (Dictionary<string, int>) 是錯的
        // 必須改成能儲存 ItemNameKey 所有的 LedgerEntry 
        private Dictionary<string, List<LedgerEntry>> _itemKeyToEntriesCache = new Dictionary<string, List<LedgerEntry>>();
        private Dictionary<string, string> _nameKeyToDisplayCache = new Dictionary<string, string>();

        // ==================================================
        // 1. 生命週期
        // ==================================================

        protected override void OnAfterSetup()
        {
            Debug.Log("[配方終結者] v22.1 (咪咪修正版) 啟動...");
            StartCoroutine(InitializeModSettingAPI());
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
            StartCoroutine(BuildTranslationCache());
        }

        private void TryInitModSetting()
        {
            if (_isModSettingRegistered) return;
            if (string.IsNullOrEmpty(this.info.name)) return;

            try
            {
                if (ModSettingAPI.Init(this.info))
                {

                    // 1. 【關鍵修正】必須先讀取 GetSavedValue，否則 ModSettingAPI 不知道你上次存了什麼
                    if (ModSettingAPI.GetSavedValue<KeyCode>(SETTING_KEY_BIND, out var savedKey))
                        _terminatorKey = savedKey;

                    // 2. 呼叫 AddKeybinding 建立 UI，並傳入 "讀取後" 的 _terminatorKey
                    ModSettingAPI.AddKeybinding(
                        SETTING_KEY_BIND,
                        "查詢配方熱鍵 (Check Recipe Key)",
                        _terminatorKey, // 傳入讀取後的值 (可能是 K，也可能是玩家改過的 F1)
                        KeyCode.K,      // 遊戲預設值 (如果設定檔重置)
                        OnKeyBindingChanged
                    );

                    _isModSettingRegistered = true;
                }
            }
            catch { }
        }

        protected override void OnBeforeDeactivate()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            CleanupUI();
            StopAllCoroutines();
            _isModSettingRegistered = false; // 重置旗標
        }

        IEnumerator InitializeModSettingAPI()
        {
            if (_isModSettingRegistered) yield break;

            bool initSuccess = false; 

            while (!_isModSettingRegistered)
            {
               if (string.IsNullOrEmpty(info.name))
                {
                    Debug.LogWarning("[UsageTerminator] base.info 尚未就緒，等待 1 個 Frame...");
                    yield return null; // 
                    continue; // 
                }

                try
                {
                    if (ModSettingAPI.Init(this.info))
                    {
                        Debug.Log("[配方終結者] ModSettingAPI.Init() 成功！");

                        // --- 舊 TryInitModSetting() 的邏輯 ---

                        // 1. (v22.1) 讀取按鍵
                        if (ModSettingAPI.GetSavedValue<KeyCode>(SETTING_KEY_BIND, out var savedKey))
                            _terminatorKey = savedKey;

                        // 2. 註冊 UI
                        ModSettingAPI.AddKeybinding(
                            SETTING_KEY_BIND,
                            "查詢配方熱鍵 (Check Recipe Key)",
                            _terminatorKey, // 傳入讀取後的值
                            KeyCode.K,      // 遊戲預設值
                            OnKeyBindingChanged
                        );

                        _isModSettingRegistered = true; // 成功，跳出迴圈
                        initSuccess = true; 
                    }
                    else
                    {
                        Debug.LogWarning("[配方終結者] ModSettingAPI.Init() 回傳 false，2 秒後重試...");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[配方終結者] ModSettingAPI 尚未就緒 (Exception: {e.Message})，2 秒後重試...");
                }

                if (!initSuccess)
                {
                yield return new WaitForSeconds(2.0f);
                }
            }
        }
        private void OnKeyBindingChanged(KeyCode newKey)
        {
            _terminatorKey = newKey;
            if (_hoverHintText != null)
                _hoverHintText.text = $"按 [{_terminatorKey}] 查看配方 (杜芬舒斯)";
        }

        void Update()
        {
            // 刪掉 Update 裡的 TryInitModSetting()，因為協程會搞定
            // if (!_isModSettingRegistered) TryInitModSetting(); 

            // 必須等 Init 跑完才能偵測
            if (!_isModSettingRegistered) return;

            // ... (Update 剩下的邏輯都一樣) ...
            bool isGameTooltipActive = false;
            if (ItemHoveringUI.Instance != null && ItemHoveringUI.Instance.LayoutParent != null)
            {
                isGameTooltipActive = ItemHoveringUI.Instance.LayoutParent.gameObject.activeInHierarchy;
            }

            if (!isGameTooltipActive)
            {
                _hoveredItem = null;
                if (_usagePanel != null) DestroySourcePanel();
            }

            if (Input.GetKeyDown(_terminatorKey))
            {
                if (_usagePanel != null && _usagePanel.activeInHierarchy)
                {
                    DestroySourcePanel();
                }
                else if (_hoveredItem != null && isGameTooltipActive)
                {
                    if (!DuckovCoreAPI.ModBehaviour.IsRecipeReady())
                    {
                        DuckovCoreAPI.ModBehaviour.ShowWarning("鴨嘴獸核心 API 資料分析中...");
                        return;
                    }
                    ShowUsagePanel(_hoveredItem);
                }
            }

            if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)) && _usagePanel != null)
            {
                DestroySourcePanel();
            }
        }

        // ==================================================
        // 2. 核心邏輯 (JEI)
        // ==================================================

        private IEnumerator BuildTranslationCache()
        {
            _itemKeyToEntriesCache.Clear(); 
            _nameKeyToDisplayCache.Clear();
            while (!DuckovCoreAPI.ModBehaviour.IsDatabaseReady()) yield return new WaitForSeconds(0.5f);

            var masterLedger = DuckovCoreAPI.ModBehaviour.GetMasterLedgerCopy();
            if (masterLedger == null) yield break;
            foreach (var entry in masterLedger.Values)
            {
                if (string.IsNullOrEmpty(entry.ItemNameKey)) continue;

                // [ v22.1 修正] 建立 ItemNameKey -> List<LedgerEntry> 的快取
                if (!_itemKeyToEntriesCache.ContainsKey(entry.ItemNameKey))
                    _itemKeyToEntriesCache[entry.ItemNameKey] = new List<LedgerEntry>();
                _itemKeyToEntriesCache[entry.ItemNameKey].Add(entry);

                // [ v22.1 修正] 建立 ItemNameKey -> DisplayName 的快取 (抓第一個 ID 的就行)
                if (!_nameKeyToDisplayCache.ContainsKey(entry.ItemNameKey))
                {
                    var meta = ItemAssetsCollection.GetMetaData(entry.TypeID);
                    if (meta.id > 0 && !string.IsNullOrEmpty(meta.DisplayName))
                    {
                        _nameKeyToDisplayCache[entry.ItemNameKey] = meta.DisplayName;
                    }
                }
            }
        }

        private string GetLocalizedName(string key)
        {
            if (_nameKeyToDisplayCache.TryGetValue(key, out string displayName)) return displayName;

            if (_itemKeyToEntriesCache.TryGetValue(key, out List<LedgerEntry> entries) && entries.Count > 0)
            {
                int typeId = entries[0].TypeID; // 抓列表裡的第一個 ID
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.id > 0 && !string.IsNullOrEmpty(meta.DisplayName))
                {
                    _nameKeyToDisplayCache[key] = meta.DisplayName;
                    return meta.DisplayName;
                }
            }
            return key;
        }

        // --- 抓耙仔函數 (升級版) ---
        private List<string> GetModSources(string itemNameKey)
        {
            List<string> sources = new List<string>();
            try
            {
                // 1. 從我們新的快取，抓出 "所有" 叫這個 Key 的物品
                if (_itemKeyToEntriesCache.TryGetValue(itemNameKey, out List<LedgerEntry> entries))
                {
                    // 2. 遍歷列表，把 "不重複" 的 Mod 名稱 (BronzeID) 加進去
                    foreach (var entry in entries)
                    {
                        if (!string.IsNullOrEmpty(entry.BronzeID) && !sources.Contains(entry.BronzeID))
                        {
                            sources.Add(entry.BronzeID);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[配方終結者] 抓 Mod 來源失敗 (Key: {itemNameKey}): {e.Message}");
            }
            return sources; // 回傳列表 (可能包含 0, 1, 或多個 Mod 名稱)
        }

        private void ShowUsagePanel(Item item)
        {
            if (item == null) return;
            DestroySourcePanel();
            _usagePanel = CreateUsagePanelUI();  

            if (_usagePanel == null) return;
            TextMeshProUGUI contentText = _usagePanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (contentText != null)
            {
                contentText.text = "<color=yellow>正在檢索完整配方表...</color>";
                StartCoroutine(ShowUsagesCoroutine(contentText, item));
            }
        }

        private IEnumerator ShowUsagesCoroutine(TextMeshProUGUI targetText, Item item)
        {
            if (_nameKeyToDisplayCache.Count == 0) yield return StartCoroutine(BuildTranslationCache());
            StringBuilder sb = new StringBuilder();
            string targetKey = item.DisplayNameRaw;
            string targetName = GetLocalizedName(targetKey);

            sb.AppendLine($"<size=130%><color=#00FF00>【杜芬舒斯・配方終結者】</color></size>");
            sb.AppendLine($"<size=110%>目標物品：<b><color=#00FFFF>{targetName}</color></b></size>");
            sb.AppendLine($"<color=#888888>(ID: {item.TypeID})</color>");
            sb.AppendLine("========================================");
            var recipeLedger = DuckovCoreAPI.ModBehaviour.GetRecipeLedgerCopy();
            List<DuckovCoreAPI.RecipeEntry> craftRecipes = new List<DuckovCoreAPI.RecipeEntry>();
            List<DuckovCoreAPI.RecipeEntry> usageRecipes = new List<DuckovCoreAPI.RecipeEntry>();
            foreach (var recipe in recipeLedger.Values)
            {
                foreach (var res in recipe.Result)
                    if (res.ItemNameKey == targetKey)
                    {
                        craftRecipes.Add(recipe);
                        break;
                    }

                foreach (var ingredient in recipe.Cost)
                    if (ingredient.ItemNameKey == targetKey)
                    {
                        usageRecipes.Add(recipe);
                        break;
                    }

            }

            if (craftRecipes.Count > 0)
            {
                sb.AppendLine($"<size=120%><color=#FFA500>▼ 如何製作此物品 ({craftRecipes.Count} 種)</color></size>");
                foreach (var recipe in craftRecipes)
                {
                    sb.Append("材料：");
                    List<string> ingredients = new List<string>();
                    // 用 HashSet 避免重複顯示模組名稱
                    HashSet<string> modSources = new HashSet<string>();

                    foreach (var cost in recipe.Cost)
                    {
                        ingredients.Add($"{GetLocalizedName(cost.ItemNameKey)} <color=#FFCC00>x{cost.Count}</color>");

                        // [ v22.1 修正] 
                        List<string> modSourcesList = GetModSources(cost.ItemNameKey);
                        if (modSourcesList.Count > 0)
                        {
                            foreach (var src in modSourcesList) modSources.Add(src);
                        }
                    }
                    sb.AppendLine(string.Join(", ", ingredients));

                    // 在材料清單下面，顯示所有不重複的模組名稱
                    if (modSources.Count > 0)
                    {
                        // 你可以自己調整字體大小(75%)和顏色(#AAAAAA)
                        sb.AppendLine($"<size=75%><color=#AAAAAA><i>來源模組: {string.Join(", ", modSources)}</i></color></size>");
                    }

                    if (recipe.Tags != null && recipe.Tags.Count > 0)
                        sb.AppendLine($"<size=80%><i>(工作台: {string.Join(", ", recipe.Tags)})</i></size>");
                    sb.AppendLine("----------------------------------------");
                }
                sb.AppendLine("");
            }

            if (usageRecipes.Count > 0)
            {
                sb.AppendLine($"<size=120%><color=#00FF00>▼ 此物品的用途 ({usageRecipes.Count} 種)</color></size>");
                foreach (var recipe in usageRecipes)
                {
                    List<string> resultItems = new List<string>();
                    HashSet<string> modSources = new HashSet<string>(); // 用 HashSet 避免重複

                    foreach (var res in recipe.Result)
                    {
                        string name = GetLocalizedName(res.ItemNameKey);
                        string countStr = $"x{res.Count}";
                        resultItems.Add($"{name} <color=#00FF00>{countStr}</color>"); // 1. 組合 物品字串

                        List<string> modSourcesList = GetModSources(res.ItemNameKey);
                        if (modSourcesList.Count > 0)
                        {
                            foreach (var src in modSourcesList) modSources.Add(src);
                        }
                    }

                    // 4. 先印出製作目標
                    sb.AppendLine($"<b>製作目標：{string.Join(", ", resultItems)}</b>");

                    // 5. 如果有抓到模組，才印出下一行
                    if (modSources.Count > 0)
                    {
                        // 這裡的格式跟「如何製作」的格式統一
                        sb.AppendLine($"<size=75%><color=#AAAAAA><i>來源模組: {string.Join(", ", modSources)}</i></color></size>");
                    }

                    sb.AppendLine("完整配方：");
                    foreach (var cost in recipe.Cost)
                    {
                        string name = GetLocalizedName(cost.ItemNameKey);
                        string countStr = $"x{cost.Count}";

                        List<string> modSourcesList = GetModSources(cost.ItemNameKey);
                        string modSourceString = "";
                        if (modSourcesList.Count > 0)
                        {
                            // 如果有多個來源，用 "/" 隔開 (例如 "ModA/ModB")
                            modSourceString = $" <size=75%><color=#AAAAAA><i>({string.Join("/", modSourcesList)})</i></color></size>";
                        }

                        if (cost.ItemNameKey == targetKey)
                            // 把 modSourceString 加到行尾
                            sb.AppendLine($"  > <color=#00FFFF>{name} {countStr} (你查的這個)</color>{modSourceString}");
                        else
                            // 把 modSourceString 加到行尾
                            sb.AppendLine($"  - {name} {countStr}{modSourceString}");
                    }
                    if (recipe.Tags != null && recipe.Tags.Count > 0)
                        sb.AppendLine($"<size=80%><i>(工作台: {string.Join(", ", recipe.Tags)})</i></size>");
                    sb.AppendLine("----------------------------------------");
                }
            }

            if (craftRecipes.Count == 0 && usageRecipes.Count == 0)
                sb.AppendLine("\n<color=#FF4444>這東西完全沒用！</color>");
            sb.AppendLine("\n<size=60%><color=#666666>Powered by DuckovCoreAPI</color></size>");

            targetText.text = sb.ToString();
        }

        private Sprite GetPixelSprite()
        {
            if (_whitePixelSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero);
            }
            return _whitePixelSprite;
        }

        // ==================================================
        // 3. UI 結構 
        // ==================================================

        private GameObject CreateUsagePanelUI()
        {
            try
            {
                _canvasGo = new GameObject("UsageTerminatorCanvas");
                Canvas canvas = _canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                _canvasGo.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(_canvasGo);

                // 1. 面板 (Panel)
                GameObject panelObj = new GameObject("UsagePanel");
                panelObj.transform.SetParent(_canvasGo.transform, false);
                RectTransform rect = panelObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(600f, 600f);

                Image bg = panelObj.AddComponent<Image>();
                bg.sprite = GetPixelSprite();
                // 使用像素圖
                bg.color = new Color(0.12f, 0.1f, 0.15f, 0.95f);
                // 邪惡紫

                Outline outline = panelObj.AddComponent<Outline>();
                outline.effectColor = new Color(0.4f, 1.0f, 0.2f, 0.8f); // 綠框 
                outline.effectDistance = new Vector2(2f, -2f);

                // 2. Scroll View 
                GameObject scrollObj = new GameObject("SourceScrollView");
                scrollObj.transform.SetParent(panelObj.transform, false);
                ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
                RectTransform scrollRectTrans = scrollObj.GetComponent<RectTransform>();
                scrollRectTrans.anchorMin = Vector2.zero;
                scrollRectTrans.anchorMax = Vector2.one;
                scrollRectTrans.offsetMin = new Vector2(15f, 15f);
                scrollRectTrans.offsetMax = new Vector2(-30f, -15f);

                scrollRect.scrollSensitivity = 15f;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;

                // 3. Viewport 
                GameObject viewport = new GameObject("Viewport");
                viewport.transform.SetParent(scrollObj.transform, false);
                RectTransform viewRect = viewport.AddComponent<RectTransform>();
                viewRect.anchorMin = Vector2.zero;
                viewRect.anchorMax = Vector2.one;
                viewRect.pivot = new Vector2(0f, 1f);
                // 左上
                viewRect.offsetMin = Vector2.zero;
                viewRect.offsetMax = new Vector2(0f, 0f);

                viewport.AddComponent<Mask>().showMaskGraphic = false;
                viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
                scrollRect.viewport = viewRect;

                // 4. Content 
                GameObject content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                RectTransform contentRect = content.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f); // 中上 
                contentRect.sizeDelta = new Vector2(0f, 300f);
                // 高度隨便設，Fitter 會蓋掉

                ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 10, 10);
                vlg.spacing = 10f;
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;

                scrollRect.content = contentRect;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;

                // 5. Text 
                GameObject textObj = new GameObject("SourceText");
                textObj.transform.SetParent(content.transform, false);
                TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();

                var uistyle = GameplayDataSettings.UIStyle;
                if (uistyle != null && uistyle.TemplateTextUGUI != null)
                {
                    tmp.font = uistyle.TemplateTextUGUI.font;
                }

                tmp.fontSize = 18f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.richText = true;
                tmp.enableWordWrapping = true;

                textObj.AddComponent<LayoutElement>();
                // 關鍵

                // 6. Scrollbar 
                Scrollbar scrollbar = CreateScrollbar(panelObj.transform);
                if (scrollbar != null)
                {
                    scrollRect.verticalScrollbar = scrollbar;
                    scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                }

                _usagePanel = panelObj;
                return panelObj;
            }
            catch (Exception e)
            {
                DuckovCoreAPI.ModBehaviour.ShowError($"UI Error: {e.Message}");
                return null;
            }
        }

        private Scrollbar CreateScrollbar(Transform parent)
        {
            try
            {
                GameObject sbObj = new GameObject("ScrollbarVertical");
                sbObj.transform.SetParent(parent, false);

                Image bg = sbObj.AddComponent<Image>();
                bg.sprite = GetPixelSprite();
                bg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

                RectTransform sbRect = sbObj.GetComponent<RectTransform>();
                sbRect.anchorMin = new Vector2(1f, 0f);
                sbRect.anchorMax = new Vector2(1f, 1f);
                sbRect.pivot = new Vector2(1f, 1f);
                sbRect.sizeDelta = new Vector2(15f, 0f);
                sbRect.anchoredPosition = new Vector2(-5f, 0f);

                GameObject slidingArea = new GameObject("SlidingArea");
                slidingArea.transform.SetParent(sbObj.transform, false);
                RectTransform slidingRect = slidingArea.AddComponent<RectTransform>();
                slidingRect.anchorMin = Vector2.zero;
                slidingRect.anchorMax = Vector2.one;
                slidingRect.offsetMin = new Vector2(2f, 2f);
                slidingRect.offsetMax = new Vector2(-2f, -2f);

                GameObject handleObj = new GameObject("Handle");
                handleObj.transform.SetParent(slidingArea.transform, false);
                Image handleImg = handleObj.AddComponent<Image>();
                handleImg.sprite = GetPixelSprite();
                handleImg.color = new Color(0.4f, 1.0f, 0.2f, 0.8f);

                RectTransform handleRect = handleObj.GetComponent<RectTransform>();
                handleRect.sizeDelta = Vector2.zero;

                Scrollbar sb = sbObj.AddComponent<Scrollbar>();
                sb.handleRect = handleRect;
                sb.direction = Scrollbar.Direction.BottomToTop;
                sb.targetGraphic = handleImg;

                return sb;
            }
            catch
            {
                return null;
            }
        }

        private void OnSetupMeta(ItemHoveringUI uiInstance, ItemMetaData data)
        {
            _hoveredItem = null;
            if (_hoverHintText != null) _hoverHintText.gameObject.SetActive(false);
        }

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            _hoveredItem = item;
            if (item != null && uiInstance != null && uiInstance.LayoutParent != null)
            {
                if (_hoverHintText == null || !_hoverHintText.gameObject) InitializeHoverHintText();
                if (_hoverHintText != null)
                {
                    _hoverHintText.gameObject.SetActive(true);
                    if (_hoverHintText.transform.parent != uiInstance.LayoutParent)
                        _hoverHintText.transform.SetParent(uiInstance.LayoutParent, false);
                    _hoverHintText.transform.SetAsLastSibling();
                    _hoverHintText.text = $"<color=cyan>按 [{_terminatorKey}] 查看配方 (杜芬舒斯)</color>";
                }
            }
        }

        private void InitializeHoverHintText()
        {
            var uistyle = GameplayDataSettings.UIStyle;
            var template = (uistyle != null) ? uistyle.TemplateTextUGUI : null;
            if (template != null && (_hoverHintText == null))
            {
                _hoverHintText = Instantiate(template);
                _hoverHintText.fontSize = 16f;
                _hoverHintText.alignment = TextAlignmentOptions.Left;
                _hoverHintText.richText = true;
                _hoverHintText.gameObject.SetActive(false);
                DontDestroyOnLoad(_hoverHintText.gameObject);
            }
        }

        private void CleanupUI()
        {
            if (_hoverHintText != null) Destroy(_hoverHintText.gameObject);
            if (_usagePanel != null) Destroy(_usagePanel);
            if (_canvasGo != null) Destroy(_canvasGo);
        }

        // [關鍵修復] 關閉 UI 時，強制清空變數
        private void DestroySourcePanel()
        {
            if (_usagePanel != null) Destroy(_usagePanel);
            if (_canvasGo != null) Destroy(_canvasGo);
            _usagePanel = null;
            _canvasGo = null;
            _hoveredItem = null;
        }
    }
}