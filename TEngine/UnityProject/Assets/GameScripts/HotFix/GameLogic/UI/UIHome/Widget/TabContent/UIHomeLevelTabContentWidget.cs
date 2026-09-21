using Cysharp.Threading.Tasks;
using GameLogic.Localization;
using RTLTMPro;
using TEngine;

namespace GameLogic.UI
{
    public class UIHomeLevelTabContentWidget : UIHomeTabContentWidget
    {
        public const string LevelPrefabPath = "UIHome_LevelTabContent";

        private XYButton  _playBtn;
        private RTLTextMeshPro _playBtnName;
        private RTLTextMeshPro _levelNameText;

        protected override void OnCreate()
        {
            base.OnCreate();
            _playBtn = CreateWidget<XYButton>("m_btnStartLevel");
            _playBtnName = this.FindChildComponent<RTLTextMeshPro>("m_btnStartLevel/m_btnName");
            _levelNameText = FindChildComponent<RTLTextMeshPro>("m_btnStartLevel/Titile");

            _playBtn.OnAddListener(OnStartLevel);
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
        }


        private void EnableBtn(bool enable)
        {
            _playBtn.Enable = enable;
        }

        private void OnStartLevel()
        {
            // 已通关所有关卡：弹全通关提示，不再进入游戏
            if (GameManager.Instance.IsAllLevelCompleted)
            {
                GameModule.UI.ShowUIAsync<UIFinishCompleteAllLevel>();
                return;
            }

            EnableBtn(false);
            GameManager.Instance.StartGame().Forget();
        }

        private void SetLevelName()
        {
            if (_levelNameText == null) return;
            string levelName = GetLevelName();
            _levelNameText.text = levelName;
            // 全通关或配置未就绪时，不显示不存在的下一关；开始按钮保留原来的全通关提示。
            _levelNameText.gameObject.SetActive(!string.IsNullOrEmpty(levelName));
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
