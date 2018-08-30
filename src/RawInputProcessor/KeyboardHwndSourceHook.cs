using RawInputProcessor.Event;
using RawInputProcessor.Win32;
using System;
using System.Windows.Interop;

namespace RawInputProcessor
{
    public class KeyboardHwndSourceHook : IDisposable
    {
        private RawKeyboardMessageProcessor _keyboardDriver;

        public KeyboardHwndSourceHook(HwndSource hwndSource, bool captureOnlyInForeground)
        {
            hwndSource.AddHook(Hook);

            _keyboardDriver = new RawKeyboardMessageProcessor(hwndSource.Handle, captureOnlyInForeground);
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            switch (msg)
            {
                case Win32Consts.WM_INPUT_DEVICE_CHANGE:
                {
                    InputDeviceChange?.Invoke(this, EventArgs.Empty);
                    break;
                }
                case Win32Consts.WM_INPUT:
                {
                    _keyboardDriver.ProcessRawInput(lparam);
                    break;
                }
            }

            return IntPtr.Zero;
        }

        public event EventHandler<EventArgs> InputDeviceChange;

        public event EventHandler<RawKeyEventArgs> KeyPressed
        {
            add { _keyboardDriver.KeyPressed += value; }
            remove { _keyboardDriver.KeyPressed -= value; }
        }

        public void Dispose()
        {
            InputDeviceChange = null;
            _keyboardDriver.Dispose();
        }

        public static string GetDeviceDianostics()
        {
            return Win32Methods.GetDeviceDiagnostics();
        }
    }
}