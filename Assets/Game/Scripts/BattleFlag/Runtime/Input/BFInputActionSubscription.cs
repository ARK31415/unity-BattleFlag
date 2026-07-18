using System;
using UnityEngine.InputSystem;

namespace BF.Game.Runtime.Input
{
    /// <summary>
    /// 输入动作订阅句柄。业务脚本持有该对象，并在禁用或销毁时 Dispose，
    /// 避免跨场景或对象销毁后继续响应输入回调。
    /// </summary>
    public sealed class BFInputActionSubscription : IDisposable
    {
        private readonly InputAction _action;
        private readonly Action<InputAction.CallbackContext> _performed;
        private readonly Action<InputAction.CallbackContext> _canceled;
        private bool _isDisposed;

        public BFInputActionSubscription(
            InputAction action,
            Action<InputAction.CallbackContext> performed,
            Action<InputAction.CallbackContext> canceled)
        {
            _action = action;
            _performed = performed;
            _canceled = canceled;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_performed != null) _action.performed -= _performed;
            if (_canceled != null) _action.canceled -= _canceled;
        }
    }
}
