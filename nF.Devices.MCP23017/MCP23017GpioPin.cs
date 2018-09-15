using System;
using System.Text;
using Windows.Devices.Gpio;

namespace nF.Devices.MCP23017
{
    public sealed class MCP23017GpioPin : IGpioPin, IDisposable
    {
        private readonly MCP23017GpioController _gpioController;
        private GpioPinDriveMode _driveMode;

        internal MCP23017GpioPin(MCP23017GpioController gpioController, int pinNumber, GpioSharingMode sharingMode)
        {
            _gpioController = gpioController;
            PinNumber = pinNumber;
            SharingMode = sharingMode;
        }

        public TimeSpan DebounceTimeout { get; set; } = TimeSpan.FromTicks(0);

        public int PinNumber { get; private set; }

        public GpioSharingMode SharingMode { get; private set; }

        public event GpioPinValueChangedEventHandler ValueChanged;

        internal void DoValueChangedEvent(GpioPinValueChangedEventArgs e)
        {
            this.ValueChanged?.Invoke(this, e);
        }

        public GpioPinDriveMode GetDriveMode()
        {
            return _driveMode;
        }

        public void SetDriveMode(GpioPinDriveMode value)
        {
            if (this.IsDriveModeSupported(value) == false)
            {
                throw new Exception("DriveMode not supported");
            }

            this._gpioController.SetPinPullup((byte)this.PinNumber, value == GpioPinDriveMode.InputPullUp);
            this._gpioController.SetPinDirection((byte)this.PinNumber, value != GpioPinDriveMode.Output);
            this._driveMode = value;
        }

        public bool IsDriveModeSupported(GpioPinDriveMode driveMode)
        {
            if ((driveMode == GpioPinDriveMode.Input) || (driveMode == GpioPinDriveMode.InputPullUp) || (driveMode == GpioPinDriveMode.Output))
            {
                return true;
            }
            return false;
        }

        public GpioPinValue Read()
        {
            return this._gpioController.Read((byte)this.PinNumber) ? GpioPinValue.High : GpioPinValue.Low;
        }

        public void Write(GpioPinValue value)
        {
            this._gpioController.Write((byte)this.PinNumber, value == GpioPinValue.High);
        }

        #region IDisposable Support
        private bool _disposed = false; // To detect redundant calls

        internal bool IsDisposed => _disposed;

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Set this back to power on default
                    SetDriveMode(GpioPinDriveMode.Input);
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
