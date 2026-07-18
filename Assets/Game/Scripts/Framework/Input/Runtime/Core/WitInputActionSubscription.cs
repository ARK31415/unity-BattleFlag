using System;
using UnityEngine.InputSystem;

namespace Wit.Framework.Input
{
    public sealed class WitInputActionSubscription : IDisposable
    {
        private readonly InputAction _action;
        private readonly Action<InputAction.CallbackContext> _performed;
        private readonly Action<InputAction.CallbackContext> _canceled;
        private bool _isDisposed;

        public WitInputActionSubscription(
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
