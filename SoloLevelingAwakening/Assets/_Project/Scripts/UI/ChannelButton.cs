using SLA.Core;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>
    /// A UI button's action: raises a void channel, or an int channel with a fixed value. Lets windows send
    /// commands (buy item 2, end the day, resume) without referencing the systems that handle them.
    /// </summary>
    public class ChannelButton : MonoBehaviour
    {
        [SerializeField] private VoidEventChannel _voidChannel;
        [SerializeField] private IntEventChannel _intChannel;
        [SerializeField] private int _value;

        /// <summary>Wired to the button's onClick.</summary>
        public void Press()
        {
            if (_voidChannel != null)
            {
                _voidChannel.Raise();
            }
            if (_intChannel != null)
            {
                _intChannel.Raise(_value);
            }
        }
    }
}
