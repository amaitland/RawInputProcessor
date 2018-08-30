using RawInputProcessor.Enums;
using System;
using System.Windows.Input;

namespace RawInputProcessor.Event
{
    public sealed class RawKeyEventArgs : EventArgs
    {
        public IntPtr Device { get; private set; }
        public KeyPressState KeyPressState { get; private set; }
        public uint Message { get; private set; }
        public Key Key { get; private set; }
        public int VirtualKey { get; private set; }
        public bool Handled { get; set; }

        internal RawKeyEventArgs(IntPtr device, KeyPressState keyPressState, uint message, Key key,
            int virtualKey)
        {
            Device = device;
            KeyPressState = keyPressState;
            Message = message;
            Key = key;
            VirtualKey = virtualKey;
        }
    }
}