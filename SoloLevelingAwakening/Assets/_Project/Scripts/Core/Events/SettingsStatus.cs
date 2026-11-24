namespace SLA.Core
{
    /// <summary>Comfort and UI settings (CMF presets, UI-15), for the pause menu.</summary>
    public readonly struct SettingsStatus
    {
        public readonly string ComfortPreset;
        public readonly string UiMode;

        public SettingsStatus(string comfortPreset, string uiMode)
        {
            ComfortPreset = comfortPreset;
            UiMode = uiMode;
        }
    }
}
