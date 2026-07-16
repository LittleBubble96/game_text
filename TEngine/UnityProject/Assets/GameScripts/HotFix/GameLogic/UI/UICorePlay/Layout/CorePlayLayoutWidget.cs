using GameLogic.GamePlay.CorePlay.View;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    public class CorePlayLayoutWidget : UIWidget
    {
        private RectTransform _slotCenter;
        private RectTransform _slotLeft;
        private RectTransform _slotRight;
        private RectTransform _slotTop;
        private RectTransform _slotBottom;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            _slotCenter = transform.GetComponent<RectTransform>();
            _slotLeft = FindChildComponent<RectTransform>("Left");
            _slotRight = FindChildComponent<RectTransform>("Right");
            _slotTop = FindChildComponent<RectTransform>("Top");
            _slotBottom = FindChildComponent<RectTransform>("Bottom");
            
        }

        private Canvas GetComponentInParent<Canvas>() where Canvas : Component
        {
            Transform t = transform;
            while (t != null)
            {
                Canvas c = t.GetComponent<Canvas>();
                if (c != null) return c;
                t = t.parent;
            }
            return null;
        }

        /// <summary>
        /// 执行 Layout：将 UI 坐标转为世界坐标，通过事件发送 SlotViewLayoutData
        /// </summary>
        public void Activate()
        {
            if (_slotCenter == null || GameModule.UI.UICamera == null) return;

            SlotViewLayoutData layoutData = new SlotViewLayoutData
            {
                Center = UIPointToWorld(_slotCenter.position),
                Left = UIPointToWorld(_slotLeft != null ? _slotLeft.position : _slotCenter.position),
                Right = UIPointToWorld(_slotRight != null ? _slotRight.position : _slotCenter.position),
                Top = UIPointToWorld(_slotTop != null ? _slotTop.position : _slotCenter.position),
                Bottom = UIPointToWorld(_slotBottom != null ? _slotBottom.position : _slotCenter.position),
            };

            GameEvent.Send(EventDefine.Event_SlotLayoutUpdate, layoutData);
        }

        /// <summary>
        /// 将 UI 屏幕坐标转换为世界坐标（z 保持为 0，只取 x、y）
        /// </summary>
        private Vector3 UIPointToWorld(Vector3 uiWorld)
        {
            Vector3 screenPos = GameModule.UI.UICamera.WorldToScreenPoint(uiWorld);
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            return new Vector3(worldPos.x, worldPos.y, 0);
        }
    }
}