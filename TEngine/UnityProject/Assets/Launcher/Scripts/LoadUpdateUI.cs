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
            m_textUpdateDesc.text = param?.ToString();
            RefreshProgress(0f);
        }

        internal void RefreshProgress(float progress)
        {
            m_bar.gameObject.SetActive(true);
            m_fill.sizeDelta = new Vector2(986 * progress , m_fill.sizeDelta.y);
        }

        internal void RefreshVersion(string version)
        {
            m_textVersion.text = version;
        }

        internal void RefreshAppid(string appid)
        {
            m_textLabelAppid.text = appid;
        }
    }
}