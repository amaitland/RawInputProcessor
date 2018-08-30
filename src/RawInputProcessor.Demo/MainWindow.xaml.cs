using RawInputProcessor.Event;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;

namespace RawInputProcessor.Demo
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private KeyboardHwndSourceHook _keyboardHwndSourceHook;
        private int _deviceCount;
        private RawKeyEventArgs _event;
        private RawKeyboardDevice _device;

        private IDictionary<IntPtr, RawKeyboardDevice> _keyboardDevices;

        public MainWindow()
        {
            DataContext = this;

            InitializeComponent();

            ManagementObjectSearcher searcher =
                new ManagementObjectSearcher("root\\CIMV2",
                "SELECT * FROM Win32_Keyboard");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                var desc = queryObj["Description"];
            }
        }

        public int DeviceCount
        {
            get { return _deviceCount; }
            set
            {
                _deviceCount = value;
                OnPropertyChanged();
            }
        }

        public RawKeyEventArgs Event
        {
            get { return _event; }
            set
            {
                _event = value;
                OnPropertyChanged();
            }
        }

        public RawKeyboardDevice Device
        {
            get { return _device; }
            set
            {
                _device = value;
                OnPropertyChanged();
            }
        }

        private void OnKeyPressed(object sender, RawKeyEventArgs e)
        {
            Event = e;
            if (_keyboardDevices.TryGetValue(e.Device, out RawKeyboardDevice dev))
            {
                Device = dev;
            }
            if (e.Device == new IntPtr(0x1b21034f) && ShouldHandle.IsChecked == true)
            {
                e.Handled = true;
            }

            DeviceCount = _keyboardDevices.Count;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (!(PresentationSource.FromVisual(this) is HwndSource source))
            {
                throw new InvalidOperationException("Cannot find a valid HwndSource");
            }

            _keyboardHwndSourceHook = new KeyboardHwndSourceHook(source, true);
            _keyboardHwndSourceHook.KeyPressed += OnKeyPressed;
            _keyboardHwndSourceHook.InputDeviceChange += OnInputDeviceChange;

            _keyboardDevices = RawKeyboardDevice.GetDevices();

            DeviceCount = _keyboardDevices.Count;

            base.OnSourceInitialized(e);
        }

        private void OnInputDeviceChange(object sender, EventArgs e)
        {
            _keyboardDevices = RawKeyboardDevice.GetDevices();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}