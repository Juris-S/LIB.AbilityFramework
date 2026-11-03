using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Juris.Abilities.InputSystem
{
    public enum AbilityInputKind
    {
        /// <summary>Press gives Pressed, release gives Released.</summary>
        Activate,
        Confirm,
        Cancel,
    }

    /// <summary>Maps Input System actions to <see cref="AbilityCommand"/>s on an <see cref="AbilitySystem"/>.</summary>
    public sealed class AbilityInputBinding : MonoBehaviour
    {
        [Serializable]
        private struct Binding
        {
            public InputActionReference action;
            public int slot;
            public AbilityInputKind kind;
        }

        [SerializeField] private AbilitySystem abilitySystem;
        [Tooltip("Aim ray origin and forward, usually the camera.")]
        [SerializeField] private Transform aimSource;
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        private Action<InputAction.CallbackContext>[] _onStarted;
        private Action<InputAction.CallbackContext>[] _onCanceled;

        private void OnEnable()
        {
            _onStarted = new Action<InputAction.CallbackContext>[bindings.Length];
            _onCanceled = new Action<InputAction.CallbackContext>[bindings.Length];

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.action == null) continue;
                var action = binding.action.action;

                var pressPhase = binding.kind switch
                {
                    AbilityInputKind.Confirm => AbilityInputPhase.Confirm,
                    AbilityInputKind.Cancel => AbilityInputPhase.Cancel,
                    _ => AbilityInputPhase.Pressed,
                };
                _onStarted[i] = _ => Send(binding.slot, pressPhase);
                action.started += _onStarted[i];

                if (binding.kind == AbilityInputKind.Activate)
                {
                    _onCanceled[i] = _ => Send(binding.slot, AbilityInputPhase.Released);
                    action.canceled += _onCanceled[i];
                }

                action.Enable();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].action == null) continue;
                var action = bindings[i].action.action;
                if (_onStarted[i] != null) action.started -= _onStarted[i];
                if (_onCanceled[i] != null) action.canceled -= _onCanceled[i];
            }
        }

        private void Send(int slot, AbilityInputPhase phase)
        {
            var controller = abilitySystem != null ? abilitySystem.Controller : null;
            if (controller == null) return;
            var aim = aimSource != null ? new Ray(aimSource.position, aimSource.forward) : default;
            controller.Submit(new AbilityCommand(slot, phase, aim));
        }
    }
}
