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

        private RectTransform _contentTop;
        private RectTransform _contentBottom;
        private RectTransform _contentLeft;
        private RectTransform _contentRight;

        protected override void OnCreate()
        {
            base.OnCreate();
            _slotCenter = FindChildComponent<RectTransform>("SlotLayout");
            _slotLeft = FindChildComponent<RectTransform>("SlotLayout/Left");
            _slotRight = FindChildComponent<RectTransform>("SlotLayout/Right");
            _slotTop = FindChildComponent<RectTransform>("SlotLayout/Top");
            _slotBottom = FindChildComponent<RectTransform>("SlotLayout/Bottom");
            
            _contentTop = FindChildComponent<RectTransform>("ContentLayout/Up");
            _contentBottom = FindChildComponent<RectTransform>("ContentLayout/Down");
            _contentLeft = FindChildComponent<RectTransform>("ContentLayout/Left");
            _contentRight = FindChildComponent<RectTransform>("ContentLayout/Right");
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
        /// 执行 Layout：将 UI 坐标转为世界坐标，通过事件发送 SlotViewLayoutData 与 ContentViewLayoutData。
        /// Content 四点定义 CharacterRoot 的可用区域：上/下限制高，左/右限制宽。
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

            // Character 渲染区可用宽高：四角点缺省退回 _slotCenter（宽高为 0，不缩放）
            Vector3 cTop = UIPointToWorld(_contentTop != null ? _contentTop.position : Vector3.zero);
            Vector3 cBottom = UIPointToWorld(_contentBottom != null ? _contentBottom.position : Vector3.zero);
            Vector3 cLeft = UIPointToWorld(_contentLeft != null ? _contentLeft.position : Vector3.zero);
            Vector3 cRight = UIPointToWorld(_contentRight != null ? _contentRight.position : Vector3.zero);

            ContentViewLayoutData contentLayout = new ContentViewLayoutData
            {
                Top = cTop,
                Bottom = cBottom,
                Left = cLeft,
                Right = cRight,
                AvailableWidth = Mathf.Max(0f, cRight.x - cLeft.x),
                AvailableHeight = Mathf.Max(0f, cTop.y - cBottom.y),
            };

            GameEvent.Send(EventDefine.Event_CharacterLayoutUpdate, contentLayout);
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