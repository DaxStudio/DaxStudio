using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public enum EditorHotkey
    {
        MoveLineUp,
        MoveLineDown,
        SelectWord
    }

    public class EditorHotkeyEvent
    {
        public EditorHotkeyEvent(EditorHotkey hotkey)
        {
            Hotkey = hotkey;
        }

        public EditorHotkey Hotkey { get; }
    }
}
