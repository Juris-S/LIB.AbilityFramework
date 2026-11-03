using System.Text;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// IMGUI overlay listing every <see cref="AbilitySystem"/>: abilities, running state (execution ToString()),
    /// cooldowns, owner tags, blocked tags and the buffered press. Toggle with <see cref="toggleKey"/>.
    /// </summary>
    public sealed class AbilityDebugOverlay : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private bool visible = true;

        private readonly StringBuilder _text = new StringBuilder();
        private AbilitySystem[] _systems = System.Array.Empty<AbilitySystem>();
        private float _nextRefresh;
        private Vector2 _scroll;

        private void OnGUI()
        {
            // IMGUI key events work with both the legacy and new Input System backends.
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == toggleKey)
            {
                visible = !visible;
                e.Use();
            }
            if (!visible) return;

            if (Time.unscaledTime >= _nextRefresh)
            {
                _systems = FindObjectsByType<AbilitySystem>();
                _nextRefresh = Time.unscaledTime + 1f;
            }

            if (e.type == EventType.Layout) BuildText();

            GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_text.ToString());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void BuildText()
        {
            _text.Clear();
            _text.Append("<b>Abilities</b>  (").Append(toggleKey).Append(" to hide)\n");
            foreach (var system in _systems)
            {
                var controller = system != null ? system.Controller : null;
                if (controller == null) continue;

                _text.Append("\n<b>").Append(system.name).Append("</b>\n");
                AppendTags("  Tags: ", controller.Tags);
                AppendTags("  Blocking: ", controller.BlockedAbilityTags);
                if (controller.HasBufferedPress)
                    _text.Append("  Buffered: slot ").Append(controller.BufferedPress.Slot)
                        .Append(" (").Append(controller.BufferRemaining.ToString("0.000")).Append("s)\n");

                foreach (var ability in controller.Abilities)
                {
                    _text.Append("  [").Append(ability.Slot).Append("] ").Append(ability.Definition.AbilityId);
                    if (ability.IsRunning) _text.Append("  <color=lime>RUNNING</color> ").Append(ability.Execution);
                    float cooldown = ability.CooldownRemaining;
                    if (cooldown > 0f) _text.Append("  <color=orange>CD ").Append(cooldown.ToString("0.0")).Append("s</color>");
                    if (ability.MaxCharges > 1)
                    {
                        _text.Append("  charges ").Append(ability.Charges).Append('/').Append(ability.MaxCharges);
                        float recharge = ability.RechargeRemaining;
                        if (recharge > 0f) _text.Append(" (+1 in ").Append(recharge.ToString("0.0")).Append("s)");
                    }
                    _text.Append('\n');
                }
            }
        }

        private void AppendTags(string label, TagContainer tags)
        {
            _text.Append(label);
            bool any = false;
            foreach (var tag in tags.ExplicitTags)
            {
                if (any) _text.Append(", ");
                _text.Append(tag.Name);
                any = true;
            }
            _text.Append(any ? "\n" : "-\n");
        }
    }
}
