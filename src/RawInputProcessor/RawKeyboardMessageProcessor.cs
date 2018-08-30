using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using RawInputProcessor.Enums;
using RawInputProcessor.Event;
using RawInputProcessor.Win32;

namespace RawInputProcessor
{
    public sealed class RawKeyboardMessageProcessor : IDisposable
    {
        //https://docs.microsoft.com/en-us/windows-hardware/drivers/install/guid-devinterface-hid
        private static readonly Guid DeviceInterfaceHid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        
        private IntPtr _devNotifyHandle;

        public event EventHandler<RawKeyEventArgs> KeyPressed;

        public RawKeyboardMessageProcessor(IntPtr hwnd, bool captureOnlyInForeground)
        {
            RawInputDevice[] array =
            {
                new RawInputDevice
                {
                    UsagePage = HidUsagePage.GENERIC,
                    Usage = HidUsage.Keyboard,
                    Flags = (captureOnlyInForeground ? RawInputDeviceFlags.NONE : RawInputDeviceFlags.INPUTSINK) | RawInputDeviceFlags.DEVNOTIFY,
                    Target = hwnd
                }
            };

            //Register to recieve WM_INPUT messages
            //https://docs.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-registerrawinputdevices
            if (!Win32Methods.RegisterRawInputDevices(array, (uint)array.Length, (uint)Marshal.SizeOf(array[0])))
            {
                throw new ApplicationException("Failed to register raw input device(s).", new Win32Exception());
            }

            //Register for keyboard device change notifications
            //https://docs.microsoft.com/en-gb/windows/desktop/inputdev/wm-input-device-change
            _devNotifyHandle = RegisterForDeviceNotifications(hwnd);
        }

        ~RawKeyboardMessageProcessor()
        {
            Dispose();
        }

        public void Dispose()
        {
            KeyPressed = null;

            GC.SuppressFinalize(this);
            if (_devNotifyHandle != IntPtr.Zero)
            {
                Win32Methods.UnregisterDeviceNotification(_devNotifyHandle);
                _devNotifyHandle = IntPtr.Zero;
            }
        }

        private static IntPtr RegisterForDeviceNotifications(IntPtr parent)
        {
            IntPtr notifyHandle = IntPtr.Zero;
            BroadcastDeviceInterface broadcastDeviceInterface = default(BroadcastDeviceInterface);
            broadcastDeviceInterface.dbcc_size = Marshal.SizeOf(broadcastDeviceInterface);
            broadcastDeviceInterface.BroadcastDeviceType = BroadcastDeviceType.DBT_DEVTYP_DEVICEINTERFACE;
            broadcastDeviceInterface.dbcc_classguid = DeviceInterfaceHid;
            IntPtr interfacePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BroadcastDeviceInterface)));
            try
            {
                Marshal.StructureToPtr(broadcastDeviceInterface, interfacePtr, false);
                notifyHandle = Win32Methods.RegisterDeviceNotification(parent, interfacePtr,
                    DeviceNotification.DEVICE_NOTIFY_WINDOW_HANDLE);
            }
            catch (Exception ex)
            {
                Debug.Print("Registration for device notifications Failed. Error: {0}", Marshal.GetLastWin32Error());
                Debug.Print(ex.StackTrace);
            }
            finally
            {
                Marshal.FreeHGlobal(interfacePtr);
            }

            if (notifyHandle == IntPtr.Zero)
            {
                Debug.Print("Registration for device notifications Failed. Error: {0}", Marshal.GetLastWin32Error());
            }
            return notifyHandle;
        }               

        public bool ProcessRawInput(IntPtr hdevice)
        {
            int size = 0;
            Win32Methods.GetRawInputData(hdevice, DataCommand.RID_INPUT, IntPtr.Zero, ref size, Marshal.SizeOf(typeof(RawInputHeader)));
            InputData rawBuffer;
            if (Win32Methods.GetRawInputData(hdevice, DataCommand.RID_INPUT, out rawBuffer, ref size, Marshal.SizeOf(typeof(RawInputHeader))) != size)
            {
                Debug.WriteLine("Error getting the rawinput buffer");
                return false;
            }

            int vKey = rawBuffer.data.keyboard.VKey;
            int makecode = rawBuffer.data.keyboard.Makecode;
            int flags = rawBuffer.data.keyboard.Flags;
            if (vKey == Win32Consts.KEYBOARD_OVERRUN_MAKE_CODE)
            {
                return false;
            }

            var isE0BitSet = ((flags & Win32Consts.RI_KEY_E0) != 0);
            bool isBreakBitSet = (flags & Win32Consts.RI_KEY_BREAK) != 0;

            uint message = rawBuffer.data.keyboard.Message;
            Key key = KeyInterop.KeyFromVirtualKey(AdjustVirtualKey(rawBuffer, vKey, isE0BitSet, makecode));
            EventHandler<RawKeyEventArgs> keyPressed = KeyPressed;
            if (keyPressed != null)
            {
                var rawInputEventArgs = new RawKeyEventArgs(rawBuffer.header.hDevice, isBreakBitSet ? KeyPressState.Up : KeyPressState.Down, message, key, vKey);
                keyPressed(this, rawInputEventArgs);
                if (rawInputEventArgs.Handled)
                {
                    //Remove the message
                    MSG msg;
                    Win32Methods.PeekMessage(out msg, IntPtr.Zero, Win32Consts.WM_KEYDOWN, Win32Consts.WM_KEYUP, Win32Consts.PM_REMOVE);
                }
                return rawInputEventArgs.Handled;
            }
            return false;
        }

        private static int AdjustVirtualKey(InputData rawBuffer, int virtualKey, bool isE0BitSet, int makeCode)
        {
            var adjustedKey = virtualKey;

            if (rawBuffer.header.hDevice == IntPtr.Zero)
            {
                // When hDevice is 0 and the vkey is VK_CONTROL indicates the ZOOM key
                if (rawBuffer.data.keyboard.VKey == Win32Consts.VK_CONTROL)
                {
                    adjustedKey = Win32Consts.VK_ZOOM;
                }
            }
            else
            {
                switch (virtualKey)
                {
                    // Right-hand CTRL and ALT have their e0 bit set 
                    case Win32Consts.VK_CONTROL:
                        adjustedKey = isE0BitSet ? Win32Consts.VK_RCONTROL : Win32Consts.VK_LCONTROL;
                        break;
                    case Win32Consts.VK_MENU:
                        adjustedKey = isE0BitSet ? Win32Consts.VK_RMENU : Win32Consts.VK_LMENU;
                        break;
                    case Win32Consts.VK_SHIFT:
                        adjustedKey = makeCode == Win32Consts.SC_SHIFT_R ? Win32Consts.VK_RSHIFT : Win32Consts.VK_LSHIFT;
                        break;
                    default:
                        adjustedKey = virtualKey;
                        break;
                }
            }

            return adjustedKey;
        }
    }
}