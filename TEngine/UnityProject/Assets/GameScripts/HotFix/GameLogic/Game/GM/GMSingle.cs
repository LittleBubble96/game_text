using System;
using UnityEngine;

namespace GameLogic
{
    public class GMSingle : MonoSingleton<GMSingle>
    {
        // Start is called before the first frame update
        float clickTime = 0f;
        CircleGestureDetector detector;

        protected override void OnInit()
        {
            base.OnInit();
            detector = new CircleGestureDetector();
#if UNITY_EDITOR
            GameModule.UI.ShowUIAsync<UIShowGM>();
#endif
        }
        
        public void Update()
        {
            if (Input.GetKeyUp(KeyCode.F1))
            {
                if (GameModule.UI.HasWindow<UIGM>())
                    GameModule.UI.HideUI<UIGM>();
                else
                    GameModule.UI.ShowUIAsync<UIGM>();
            }

            bool isCircle = detector.Update();
            bool isOn = GameModule.UI.HasWindow<UIShowGM>();
            if (isCircle && !isOn)
            {
                GameModule.UI.ShowUIAsync<UIShowGM>();
                detector.Reset();
            }

            if (isCircle && isOn)
            {
                GameModule.UI.HideUI<UIShowGM>();
                detector.Reset();
            }
        }
    }
}