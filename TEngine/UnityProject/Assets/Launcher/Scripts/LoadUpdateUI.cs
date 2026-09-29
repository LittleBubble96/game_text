using UnityEngine;
using UnityEngine.UI;

namespace Launcher
{
    /// <summary>
    /// UI更新界面。
    /// </summary>
    public class LoadUpdateUI : UIBase
    {
        #region 脚本工具生成的代码

        private int _startupPercent = -1;
        private Image m_imgBg;
        private RectTransform m_fill;
        private RectTransform m_bar;
        private Text m_textUpdateDesc;
        private Text m_textVersion;
        private Text m_textLabelAppid;

        protected override void ScriptGenerator()
        {
            m_imgBg = FindChildComponent<Image>("m_imgBg");
            m_bar = FindChildComponent<RectTransform>("m_scrollBarProgress");
            m_fill = FindChildComponent<RectTransform>("m_scrollBarProgress/m_fill");
            m_textUpdateDesc = FindChildComponent<Text>("m_scrollBarProgress/m_textUpdateDesc");
            m_textVersion = FindChildComponent<Text>("m_textVersion");
            m_textLabelAppid = FindChildComponent<Text>("m_textLabelAppid");
        }

        #endregion

        protected override bool FullScreen => true;

        public override void OnInit(object param)
        {
            base.OnInit(param);
            _startupPercent = -1;
            m_bar.gameObject.SetActive(true);
            m_textUpdateDesc.text = param?.ToString();
            RefreshProgress(0f);
        }

        internal void RefreshProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            m_bar.gameObject.SetActive(true);
            // 用锚点适配进度条实际宽度，并保留两侧各 12 的边框。
            m_fill.anchorMin = new Vector2(0f, 0.5f);
            m_fill.anchorMax = new Vector2(progress, 0.5f);
            m_fill.anchoredPosition = new Vector2(12f, 0f);
            m_fill.sizeDelta = new Vector2(-24f * progress, m_fill.sizeDelta.y);
            m_fill.gameObject.SetActive(progress > 0f);
        }

        // 启动阶段直接更新现有 UI，避免每帧 ShowUI/OnInit 将进度重置为零。
        public void RefreshStartupProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            RefreshProgress(progress);
            int percent = Mathf.FloorToInt(progress * 100f);
            if (percent == _startupPercent) return;
            _startupPercent = percent;
            m_textUpdateDesc.text = string.Format(LoadText.Instance.Label_Load_Load_Progress, percent);
        }

        internal void RefreshVersion(string version)
        {
            m_textVersion.text = version;
        }

        internal void RefreshAppid(string appid)
        {
            m_textLabelAppid.text = appid;
        }

        public void HideBar()
        {
            m_bar.gameObject.SetActive(false);
        }
    }
}
