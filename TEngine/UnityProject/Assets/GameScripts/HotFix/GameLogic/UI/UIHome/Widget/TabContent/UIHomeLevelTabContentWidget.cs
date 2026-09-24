using Cysharp.Threading.Tasks;
using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;

namespace GameLogic.UI
{
    public class UIHomeLevelTabContentWidget : UIWidget
    {
        private XYButton  _playBtn;
        private XYButton _gameCenterBtn;
        private XYButton _shareBtn;
        private XYButton _settingBtn;
        private RTLTextMeshPro _playBtnName;
        private RTLTextMeshPro _levelNameText;
        private RTLTextMeshPro _gameCenterText;
        private RTLTextMeshPro _shareText;
        private RTLTextMeshPro _settingText;
        private RectTransform _levelTextRoot;

        protected override void OnCreate()
        {
            base.OnCreate();
            _playBtn = CreateWidget<XYButton>("m_btnStartLevel");
            _gameCenterBtn = CreateWidget<XYButton>("rightActivity/gameCenter");
            _shareBtn = CreateWidget<XYButton>("rightActivity/share");
            _settingBtn = CreateWidget<XYButton>("rightActivity/setting");
            _playBtnName = this.FindChildComponent<RTLTextMeshPro>("m_btnStartLevel/m_btnName");
            _levelNameText = FindChildComponent<RTLTextMeshPro>("m_btnStartLevel/titleBg/Titile");
            _gameCenterText = FindChildComponent<RTLTextMeshPro>("rightActivity/gameCenter/root/m_text");
            _shareText = FindChildComponent<RTLTextMeshPro>("rightActivity/share/root/m_text");
            _settingText = FindChildComponent<RTLTextMeshPro>("rightActivity/setting/root/m_text");
            _levelTextRoot = FindChildComponent<RectTransform>("m_btnStartLevel/titleBg");
            _playBtn.OnAddListener(OnStartLevel);
            _gameCenterBtn.OnAddListener(OnGameCenterBtn);
            _shareBtn.OnAddListener(OnShare);
            _settingBtn.OnAddListener(OnSetting);

        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            RefreshLevelInfo();
        }

        internal void RefreshLevelInfo()
        {
            RefreshText();
            EnableBtn(true);
        }

        protected override void OnSetVisible(bool visible)
        {
            base.OnSetVisible(visible);
            // Tab 复用时不会再次调用 OnRefresh，切回关卡页需要主动刷新。
            if (visible && IsPrepare) RefreshLevelInfo();
        }

        internal override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            RefreshText();
        }

        private void RefreshText()
        {
            _playBtnName.text = LocalizationHelper.GetLocalText(LanguageKey.start_game_btn);
            SetLevelName();
            _gameCenterText.text = LocalizationHelper.GetLocalText(LanguageKey.home_social);
            _shareText.text = LocalizationHelper.GetLocalText(LanguageKey.home_share);
            _settingText.text = LocalizationHelper.GetLocalText(LanguageKey.home_setting);
        }


        private void EnableBtn(bool enable)
        {
            _playBtn.Enable = enable;
        }

        private void OnStartLevel()
        {
            BiMgr.HomeAction("start", GetHomeLevelId());
            // 已通关所有关卡：弹全通关提示，不再进入游戏
            if (GameManager.Instance.IsAllLevelCompleted)
            {
                GameModule.UI.ShowUIAsync<UIFinishCompleteAllLevel>();
                return;
            }

            EnableBtn(false);
            GameManager.Instance.StartGame().Forget();
        }

        private void OnGameCenterBtn()
        {
            BiMgr.HomeAction("game_club", GetHomeLevelId());
            SDK.OpenGameClub();
        }
        
        private void OnShare()
        {
            BiMgr.HomeAction("share", GetHomeLevelId());
            BiMgr.ShareClicked("home");
            SDK.ShareAppMessage($"这个游戏还不错，一起来挑战吧！");
        }
        
        private void OnSetting()
        {
            BiMgr.HomeAction("setting", GetHomeLevelId());
            GameModule.UI.ShowUIAsync<UISetting>();
        }

        private int GetHomeLevelId()
        {
            var manager = GameManager.Instance;
            int levelId = manager.CacheManager?.CorePlayRestore?.SaveData?.currentLevelId ?? 1;
            if (manager.IsAllLevelCompleted) return manager.LevelConfig.MaxLevelId;
            return manager.LevelConfig?.GetLevelNameByLevelId(levelId) != null ? levelId : 1;
        }

        private void SetLevelName()
        {
            if (_levelNameText == null) return;
            string levelName = GetLevelName();
            _levelNameText.text = levelName;
            // 全通关或配置未就绪时，不显示不存在的下一关；开始按钮保留原来的全通关提示。
            _levelTextRoot.gameObject.SetActive(!string.IsNullOrEmpty(levelName));
        }

        /// <summary>获取点击“开始游戏”将进入的关卡名称，不依赖尚未加载或仍停留在上一关的玩法数据。</summary>
        private string GetLevelName()
        {
            var manager = GameManager.Instance;
            var config = manager?.LevelConfig;
            if (config == null || manager.IsAllLevelCompleted) return string.Empty;

            var restore = manager.CacheManager?.CorePlayRestore;
            int savedLevelId = restore?.SaveData?.currentLevelId ?? 1;
            int levelId = savedLevelId;
            // 与 StartCorePlay 相同：关卡 ID 先通过关卡表映射，失效时回退到第 1 关。
            if (config.GetLevelNameByLevelId(levelId) == null) levelId = 1;

            // 已开局的关卡优先沿用存档快照，配置更新后也能与实际续玩的字一致。
            var levelData = levelId == savedLevelId ? restore?.GetCachedLevelData() : null;
            levelData ??= config.GetLevelDataByLevelId(levelId);
            if (levelData == null || !levelData.IsValid()) return string.Empty;

            return string.Format(LocalizationHelper.GetLocalText(LanguageKey.level_title),
                levelId, levelData.baseCharacter);
        }
    }
}
