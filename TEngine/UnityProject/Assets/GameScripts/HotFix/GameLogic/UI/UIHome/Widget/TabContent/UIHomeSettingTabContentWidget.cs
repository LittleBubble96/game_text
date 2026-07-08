using System.Collections.Generic;
using GameConfig;
using GameLogic.Localization;
using RTLTMPro;
using TMPro;
using UnityEngine.UI;

namespace GameLogic.UI
{
    public class UIHomeSettingTabContentWidget : UIHomeTabContentWidget
    {
        public const string SettingPrefabPath = "UIHome_SettingTabContent";
        
        private RTLTextMeshPro _titleTmp;
        private RTLTextMeshPro _settingMusicTmp;
        private RTLTextMeshPro _settingSfxTmp;
        private RTLTextMeshPro _settingNotificationTmp;
        private RTLTextMeshPro _settingLanguageTmp;

        private TMP_Dropdown _dropdown;
        private bool _hasInitDropdown;
        private bool _isRefreshingDropdown;

        protected override void OnCreate()
        {
            base.OnCreate();
            _titleTmp = FindChildComponent<RTLTextMeshPro>("Panel/TitleBar/Text");
            _settingMusicTmp = FindChildComponent<RTLTextMeshPro>("Panel/Music Txt");
            _settingSfxTmp = FindChildComponent<RTLTextMeshPro>("Panel/Sfx Txt");
            _settingNotificationTmp = FindChildComponent<RTLTextMeshPro>("Panel/Notification Txt");
            _settingLanguageTmp = FindChildComponent<RTLTextMeshPro>("Panel/Language Txt");
            _dropdown = FindChildComponent<TMP_Dropdown>("Panel/Dropdown");
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            RefreshText();
            RefreshLanguageDropdown();
        }

        internal override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            RefreshText();
            RefreshLanguageDropdown();
        }

        private void RefreshText()
        {
            _titleTmp.text = LocalizationHelper.GetLocalText(LanguageKey.setting_title);
            _settingMusicTmp.text = LocalizationHelper.GetLocalText(LanguageKey.setting_music);
            _settingSfxTmp.text = LocalizationHelper.GetLocalText(LanguageKey.setting_sfx);
            _settingNotificationTmp.text = LocalizationHelper.GetLocalText(LanguageKey.setting_notify);
            _settingLanguageTmp.text = LocalizationHelper.GetLocalText(LanguageKey.setting_language);
        }

        /// <summary>
        /// 刷新语言选择下拉框。
        /// 从 GameLocalizationManager.AvailableLanguages 读取可用语言列表，
        /// 使用本地化名称作为选项文本，并将当前语言设为选中项。
        /// </summary>
        private void RefreshLanguageDropdown()
        {
            if (_dropdown == null) return;

            var localization = GameLocalizationManager.Instance;
            var availableLanguages = localization.AvailableLanguages;
            if (availableLanguages == null || availableLanguages.Count == 0) return;

            // 防止 set value 触发的 onValueChanged 回调造成循环
            _isRefreshingDropdown = true;

            // 构建选项列表（使用本地化名称）
            var options = new List<TMP_Dropdown.OptionData>();
            int targetIndex = 0;

            for (int i = 0; i < availableLanguages.Count; i++)
            {
                var langCfg = availableLanguages[i];
                string displayText = LocalizationHelper.GetLocalText(langCfg.Name);
                options.Add(new TMP_Dropdown.OptionData(displayText));

                // 找到当前语言的索引
                if (langCfg.LanguageCode.ToString() == localization.CurrentLanguageCode)
                {
                    targetIndex = i;
                }
            }
            _dropdown.ClearOptions();
            _dropdown.AddOptions(options);
            _dropdown.value = targetIndex;

            _isRefreshingDropdown = false;

            // 只注册一次回调
            if (!_hasInitDropdown)
            {
                _hasInitDropdown = true;
                _dropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
            }
        }

        /// <summary>下拉框选中项变更回调，切换当前语言</summary>
        private void OnLanguageDropdownChanged(int index)
        {
            if (_isRefreshingDropdown) return;
            GameLocalizationManager.Instance.SetLanguageByIndex(index);
        }
    }
}