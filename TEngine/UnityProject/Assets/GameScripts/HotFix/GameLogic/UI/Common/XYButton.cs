using UnityEngine.UI;

namespace GameLogic
{
    public class XYButton : UIWidget
    {
        private Button _button;
        
        public bool Interactable { get => _button.interactable; set => _button.interactable = value; }

        public bool Enable { get => _button.enabled; set => _button.enabled = value; }

        protected override void BindMemberProperty()
        {
            base.BindMemberProperty();
            _button = transform.GetComponent<Button>();
        }
        
        public void OnAddListener(UnityEngine.Events.UnityAction call)
        {
            _button.onClick.AddListener(() =>
            {
                UIWindow parentWindow = GetParentWindow();
                if (parentWindow != null && parentWindow.IsAnimating)
                {
                    return;
                }
                call?.Invoke();
            });
        }

        /// <summary>
        /// 向上查找所属的 UIWindow。
        /// </summary>
        private UIWindow GetParentWindow()
        {
            UIBase current = Parent;
            while (current != null)
            {
                if (current is UIWindow window)
                {
                    return window;
                }
                current = current.Parent;
            }
            return null;
        }
    }
}