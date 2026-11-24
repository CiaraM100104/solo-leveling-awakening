using UnityEngine;

namespace SLA.Core
{
    /// <summary>UI-1: the colour of a System window carries its meaning.</summary>
    public enum SystemMessageKind
    {
        Info,
        Reward,
        Warning,
    }

    /// <summary>One System notification (UI-2). Text is plain; the UI adds the brackets.</summary>
    public readonly struct SystemMessage
    {
        public readonly SystemMessageKind Kind;
        public readonly string Text;

        public SystemMessage(SystemMessageKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        /// <summary>UI-2: the System speaks in short bracketed lines.</summary>
        public static string Bracket(string text) => "[" + text + "]";
    }

    /// <summary>Any system can raise a System message; the System window shows them in order.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/System Message Channel")]
    public class SystemMessageChannel : EventChannel<SystemMessage>
    {
    }
}
