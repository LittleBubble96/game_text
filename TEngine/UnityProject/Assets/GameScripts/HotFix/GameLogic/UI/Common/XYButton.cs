using UnityEngine.UI;

namespace GameLogic
{
    public class XYButton : UIWidget
    {
        private Button _button;

        protected override void BindMemberProperty()
        {
            base.BindMemberProperty();
            _button = transform.GetComponent<Button>();
        }
        
        public void OnAddListener(UnityEngine.Events.UnityAction call)
        {
            _button.onClick.AddListener(call);
        }
    }
}