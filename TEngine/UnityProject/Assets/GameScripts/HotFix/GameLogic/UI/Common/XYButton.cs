using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 全局 UI 交互锁。动画等异步流程通过 Acquire 获取令牌，令牌释放后自动恢复交互。
    /// 支持嵌套持有，避免多个流程并行时被其中一个提前解锁。
    /// </summary>
    public static class UIInteractionLock
    {
        private static int _lockCount;

        public static bool IsLocked => _lockCount > 0;

        public static System.IDisposable Acquire()
        {
            _lockCount++;
            return new LockHandle();
        }

        private sealed class LockHandle : System.IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _lockCount = System.Math.Max(0, _lockCount - 1);
            }
        }
    }

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
                if (UIInteractionLock.IsLocked)
                {
                    return;
                }

                UIWindow parentWindow = GetParentWindow();
                if (parentWindow != null && parentWindow.IsAnimating)
                {
                    return;
                }
                AudioSystem.Instance.PlayAudio(AudioDefine.btnClick_SFX , 1f);
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
