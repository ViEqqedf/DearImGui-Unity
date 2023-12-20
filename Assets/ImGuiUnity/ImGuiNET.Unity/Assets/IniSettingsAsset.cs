using UnityEngine;
using UnityEngine.Serialization;

namespace ImGuiNET.Unity
{
    // TODO: ability to save to asset, in player prefs with custom key, custom ini file, etc

    /// <summary>
    /// Used to store ImGui Ini settings in an asset instead of the default imgui.ini file
    /// </summary>
    [CreateAssetMenu(menuName = "Dear ImGui/Ini Settings")]
    public sealed class IniSettingsAsset : ScriptableObject {
        [TextArea(3, 20)]
        [SerializeField] private string data;

        public void Save(string data) {
            this.data = data;
        }

        public string Load() {
            return data;
        }
    }
}