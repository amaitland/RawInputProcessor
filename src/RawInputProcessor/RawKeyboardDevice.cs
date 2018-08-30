using RawInputProcessor.Enums;
using RawInputProcessor.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RawInputProcessor
{
    public sealed class RawKeyboardDevice
    {
        public string Name { get; private set; }
        public RawDeviceType Type { get; private set; }
        public IntPtr Handle { get; private set; }
        public string Description { get; private set; }

        internal RawKeyboardDevice(string name, RawDeviceType type, IntPtr handle, string description)
        {
            Handle = handle;
            Type = type;
            Name = name;
            Description = description;
        }

        public override string ToString()
        {
            return string.Format("Device\n Name: {0}\n Type: {1}\n Handle: {2}\n Name: {3}\n",
                Name,
                Type,
                Handle.ToInt64().ToString("X"),
                Description);
        }

        public static IDictionary<IntPtr, RawKeyboardDevice> GetDevices()
        {
            var deviceList = new Dictionary<IntPtr, RawKeyboardDevice>();

            var rawKeyboardDevice = new RawKeyboardDevice("Global Keyboard", RawDeviceType.Keyboard, IntPtr.Zero,
                "Fake Keyboard. Some keys (ZOOM, MUTE, VOLUMEUP, VOLUMEDOWN) are sent to rawinput with a handle of zero.");
            deviceList.Add(rawKeyboardDevice.Handle, rawKeyboardDevice);
            uint devices = 0u;
            int size = Marshal.SizeOf(typeof(RawInputDeviceList));
            if (Win32Methods.GetRawInputDeviceList(IntPtr.Zero, ref devices, (uint)size) != 0u)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            IntPtr pRawInputDeviceList = Marshal.AllocHGlobal((int)(size * devices));
            try
            {
                Win32Methods.GetRawInputDeviceList(pRawInputDeviceList, ref devices, (uint)size);
                int index = 0;
                while (index < devices)
                {
                    RawKeyboardDevice device = GetDevice(pRawInputDeviceList, size, index);
                    if (device != null && !deviceList.ContainsKey(device.Handle))
                    {
                        deviceList.Add(device.Handle, device);
                    }
                    index++;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pRawInputDeviceList);
            }

            return deviceList;
        }

        private static RawKeyboardDevice GetDevice(IntPtr pRawInputDeviceList, int dwSize, int index)
        {
            uint size = 0u;
            // On Window 8 64bit when compiling against .Net > 3.5 using .ToInt32 you will generate an arithmetic overflow. Leave as it is for 32bit/64bit applications
            var rawInputDeviceList = (RawInputDeviceList)Marshal.PtrToStructure(new IntPtr(pRawInputDeviceList.ToInt64() + dwSize * index), typeof(RawInputDeviceList));
            Win32Methods.GetRawInputDeviceInfo(rawInputDeviceList.hDevice, RawInputDeviceInfo.RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size <= 0u)
            {
                return null;
            }
            IntPtr intPtr = Marshal.AllocHGlobal((int)size);
            try
            {
                Win32Methods.GetRawInputDeviceInfo(rawInputDeviceList.hDevice, RawInputDeviceInfo.RIDI_DEVICENAME, intPtr, ref size);
                string device = Marshal.PtrToStringAnsi(intPtr);
                if (rawInputDeviceList.dwType == DeviceType.RimTypekeyboard ||
                    rawInputDeviceList.dwType == DeviceType.RimTypeHid)
                {
                    string deviceDescription = Win32Methods.GetDeviceDescription(device);
                    return new RawKeyboardDevice(Marshal.PtrToStringAnsi(intPtr),
                        (RawDeviceType)rawInputDeviceList.dwType, rawInputDeviceList.hDevice, deviceDescription);
                }
            }
            finally
            {
                if (intPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(intPtr);
                }
            }
            return null;
        }
    }
}