Usage Terminator (配方終結者)

這是一款為《逃離鴨科夫》設計的 Mod，靈感來自於 Minecraft 著名的 Mod - Just Enough Items (JEI)。

「配方終結者」允許玩家在遊戲中即時查詢任何物品的製作配方，以及該物品可用於哪些其他製作項目。

🌟 核心功能

即時配方查詢：滑鼠懸停在任何物品上時，按下熱鍵（預設為 K）即可彈出查詢視窗。

如何製作 (Crafting)：清楚顯示製作「目標物品」需要哪些材料。

物品用途 (Usage)：反向查詢「目標物品」可以被用來製作哪些其他物品。

Mod 兼容性：得益於 DuckovCoreAPI，本 Mod 自動支援所有其他 Mod 新增的物品與配方。

按鍵自訂：可於遊戲的 ModSetting 選單中，自由更改觸發熱鍵。

🎮 如何使用 (玩家)

確認你已經安裝了本 Mod 以及 必要前置 裡的 DuckovCoreAPI。

進入遊戲，打開你的背包或倉庫。

將滑鼠游標移動到任何你想查詢的物品上。

你會在提示框底部看到額外提示：「按 [K] 查看配方 (杜芬舒斯)」。

按下熱鍵（預設 K），配方終結者 UI 就會登場！

⚠️ 必要前置 (超級重要！)

本 Mod 是一個純粹的「前端 UI 模組」。

它 100% 依賴 DuckovCoreAPI (鴨嘴獸核心 API) 才能運作。

所有的物品掃描、配方索引、來源追蹤...等繁重工作，全都是由 DuckovCoreAPI 在背景完成的。本 Mod 只負責將 API 提供的資料「顯示」在漂亮的 UI 上。

在安裝本 Mod 之前，請務必、絕對、一定要先安裝 DuckovCoreAPI！

🔧 如何編譯 (開發者)

這是一個基於 C# 和 Unity 引擎的 Mod 專案。

1. 取得原始碼

git clone https://github.com/as5611198/UsageTerminator.git


2. 設置專案引用

你需要手動引用《逃離鴨科夫》遊戲本體及核心 API 的 .dll 檔案。

請在你的 .csproj 專案檔中，或是在 Visual Studio 的「參考」中，加入以下必要的 DLL：

遊戲本體 & Unity

Assembly-CSharp.dll (遊戲主要邏輯)

UnityEngine.Core.dll

UnityEngine.UI.dll

UnityEngine.ImageConversion.dll

Unity.TextMeshPro.dll (用於 UI 文字)

遊戲 Mod API

Duckov.Modding.dll (Mod 載入器)

Duckov.UI.dll (Mod UI 框架)

核心依賴

DuckovCoreAPI.dll (本 Mod 的靈魂！)

3. 編譯

設置好引用後，使用 Visual Studio 或 dotnet build 指令進行編譯，即可產生 UsageTerminator.dll。

🚀 如何貢獻

我們非常歡迎你一起加入！無論是回報 Bug、優化 UI、或是想挑戰實作新功能（例如那個超讚的「配方點擊跳轉」！），都歡迎你：

Fork 本專案

建立你的功能分支 (git checkout -b feature/AmazingFeature)

提交你的變更 (git commit -m 'Add some AmazingFeature')

推送至分支 (git push origin feature/AmazingFeature)

開啟一個 Pull Request

也歡迎隨時在 [Issues] 頁面中展開討論！

📜 授權條款

本專案採用 MIT License 授權。簡單來說，你可以自由使用、修改、散佈，但請附上原始的版權聲明。

❤️ 致謝

DuckovCoreAPI (鴨嘴獸核心 API) - 沒有它，就沒有這一切。

Mezz - 創造了 Minecraft JEI，為我們帶來了最初的靈感。

GlassCabbage - 感謝你製作的 [物品來源Mod名稱]，提供了 UI 設計的「致敬」來源！

所有《逃離鴨科夫》的 Mod 玩家與開發者社群！
