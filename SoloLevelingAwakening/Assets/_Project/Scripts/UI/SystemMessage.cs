using SLA.Core;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>UI-1 colours. The message itself lives in SLA.Core so any system can raise one.</summary>
    public static class SystemPalette
    {
        public static readonly Color Info = new Color32(0x3F, 0xB8, 0xFF, 0xFF);
        public static readonly Color Reward = new Color32(0xFF, 0xC9, 0x4A, 0xFF);
        public static readonly Color Warning = new Color32(0xE5, 0x38, 0x3B, 0xFF);
        public static readonly Color Background = new Color32(0x0B, 0x1A, 0x2E, 0xCC);
        public static readonly Color Text = new Color32(0xDA, 0xDA, 0xDA, 0xFF);
        /// <summary>Art direction accent for shadows and magic.</summary>
        public static readonly Color Violet = new Color32(0x7B, 0x3F, 0xE4, 0xFF);

        public static Color For(SystemMessageKind kind)
        {
            switch (kind)
            {
                case SystemMessageKind.Reward: return Reward;
                case SystemMessageKind.Warning: return Warning;
                default: return Info;
            }
        }
    }
}
