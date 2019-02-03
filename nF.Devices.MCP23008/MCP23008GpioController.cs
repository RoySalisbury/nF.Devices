using System;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace nF.Devices.MCP23008
{
    /// <summary>
    /// MCP23008 - i2c 8 input/output port expander
    /// 
    /// Use this chip from 2.7-5.5V (good for any 3.3V or 5V setup), and you can sink/source up to 20mA from any of the I/O pins.
    /// </summary>
    /// <remarks><![CDATA[
    /// MCP23008 (N) DIP18 pin layout:
    /// 
    ///   18 17 16 15 14 13 12 11 10 
    ///   │  │  │  │  │  │  │  │  │  
    /// █████████████████████████████
    /// ▀████████████████████████████
    ///   ███████████████████████████
    /// ▄████████████████████████████
    /// █████████████████████████████
    ///   │  │  │  │  │  │  │  │  │  
    ///   1  2  3  4  5  6  7  8  9  
    ///  
    /// 1   SCL                 18   VDD   
    /// 2   SDA                 17   GP7
    /// 3   A2                  16   GP6
    /// 4   A2                  15   GP5
    /// 5   A0                  14   GP4 
    /// 6   RESET (+Pullup)     13   GP3 
    /// 7   NC                  12   GP2 
    /// 8   INT                 11   GP1
    /// 9   VSS                 10   GP0
    ///
    /// Hardware Address: [0 = GND, 1 = +Pullup)
    ///     A0/A1/A2
    ///     --------
    ///     0  0  0   = 0x20
    ///     1  0  0   = 0x21
    ///     0  1  0   = 0x22
    ///     1  1  0   = 0x23
    ///     0  0  1   = 0x24
    ///     1  0  1   = 0x25
    ///     0  1  1   = 0x26
    ///     1  1  1   = 0x27
    /// 
    /// ]]></remarks>    
    public sealed class MCP23008GpioController : IGpioController, IDisposable
    {
        private const int HARDWARE_BASE_ADDRESS = 0x20;

        private const byte MCP23008_IODIR = 0x00;
        private const byte MCP23008_IPOL = 0x01;
        private const byte MCP23008_GPINTEN = 0x02;
        private const byte MCP23008_DEFVAL = 0x03;
        private const byte MCP23008_INTCON = 0x04;
        private const byte MCP23008_IOCON = 0x05;
        private const byte MCP23008_GPPU = 0x06;
        private const byte MCP23008_INTF = 0x07;
        private const byte MCP23008_INTCAP = 0x08;
        private const byte MCP23008_GPIO = 0x09;
        private const byte MCP23008_OLAT = 0x0A;

        private enum IODirection
        {
            Output = 0,
            Input = 1
        }
        private enum Polarity
        {
            Normal = 0,
            Invert = 1,
        }
        private enum ConfigurationOption
        {
            /// <summary>
            /// Sequential Operation
            /// 0 = enabled(default)
            /// 1 = disabled
            /// </summary>
            SEQOP = 0x20,

            /// <summary>
            /// SDA Slew Rate
            /// 0 = enabled(default)
            /// 1 = disabled
            /// </summary>
            DISSLW = 0x10,

            /// <summary>
            /// Hardware Address Enable for MCP23S08 SPI version only
            /// 0 = disabled(default)
            /// 1 = enabled
            /// </summary>
            HAEN = 0x08,

            /// <summary>
            /// INT pin as open-drain
            /// 0 = Active driver output(default)
            /// 1 = Open-drain output
            /// </summary>
            ODR = 0x04,

            /// <summary>
            /// INT polarity
            /// 0 = Active-low(default)
            /// 1 = Active-high
            /// </summary>
            INTPOL = 0x02,
        }

        private I2cDevice _i2cDevice;
        private IGpioPin _interruptPin;

        private readonly MCP23008GpioPin[] _gpioPin;

        private MCP23008GpioController(I2cDevice i2cDevice, IGpioPin interruptPin = null)
        {
            _i2cDevice = i2cDevice;
            _interruptPin = interruptPin;

            if (_interruptPin != null)
            {
                _interruptPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                _interruptPin.DebounceTimeout = TimeSpan.FromTicks(0);
                _interruptPin.ValueChanged += InterruptPin_ValueChanged;
            }

            this._gpioPin = new MCP23008GpioPin[PinCount];
        }

        public static MCP23008GpioController CreateDevice(string i2cBus, int address = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive, IGpioPin interruptPin = null)
        {
            // Create the I2c connection settings instance.
            I2cConnectionSettings settings = new I2cConnectionSettings(address) { BusSpeed = busSpeed, SharingMode = sharingMode };

            // Create the I2c device instance
            var i2cDevice = I2cDevice.FromId(i2cBus, settings);
            if (i2cDevice == null)
            {
                // No device was created
                throw new Exception("Unable to create I2c instance.");
            }

            try
            {
                var instance = new MCP23008GpioController(i2cDevice, interruptPin);

                // Set the defaults for our device. These are power on defaults
                instance.WriteRegister(MCP23008_IODIR, 0xFF);
                instance.WriteRegister(MCP23008_IPOL, 0x00);
                instance.WriteRegister(MCP23008_GPINTEN, 0x00);
                instance.WriteRegister(MCP23008_DEFVAL, 0x00);
                instance.WriteRegister(MCP23008_INTCON, 0x00);
                instance.WriteRegister(MCP23008_IOCON, 0x00);
                instance.WriteRegister(MCP23008_GPPU, 0x00);
                instance.WriteRegister(MCP23008_GPIO, 0x00);
                instance.WriteRegister(MCP23008_OLAT, 0x00);

                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        public int PinCount => 8;

        public IGpioPin OpenPin(int pinNumber)
        {
            return OpenPin(pinNumber, GpioSharingMode.Exclusive);
        }

        public IGpioPin OpenPin(int pinNumber, GpioSharingMode sharingMode)
        {
            if (TryOpenPin(pinNumber, sharingMode, out var gpioPin, out var openStatus))
            {
                return gpioPin;
            }

            throw new Exception("");
        }

        public bool TryOpenPin(int pinNumber, GpioSharingMode sharingMode, out IGpioPin pin, out GpioOpenStatus openStatus)
        {
            pin = null;

            if ((pinNumber < 0) || (pinNumber > PinCount-1))
            {
                openStatus = GpioOpenStatus.UnknownError;
                return false;
            }

            if ((_gpioPin[pinNumber] == null) || (this._gpioPin[pinNumber].IsDisposed))
            {
                this._gpioPin[pinNumber] = new MCP23008GpioPin(this, pinNumber, sharingMode);
            }
            else if ((sharingMode == GpioSharingMode.Exclusive) || (this._gpioPin[pinNumber].SharingMode == GpioSharingMode.Exclusive))
            {
                openStatus = GpioOpenStatus.SharingViolation;
                return false;
            }

            pin = this._gpioPin[pinNumber];
            openStatus = GpioOpenStatus.PinOpened;

            return true;
        }

        internal void SetPinDirection(byte pinNumber, bool isInput)
        {
            this.WriteRegister(MCP23008_IODIR, pinNumber, isInput);
            this.WriteRegister(MCP23008_DEFVAL, pinNumber, isInput);

            // Clear out any pending interrupt before enabling/disabling the pin
            this.ReadRegister(MCP23008_GPIO, pinNumber);
            this.WriteRegister(MCP23008_GPINTEN, pinNumber, isInput);
        }

        internal void SetPinPullup(byte pinNumber, bool value)
        {
            WriteRegister(MCP23008_GPPU, pinNumber, value);
        }

        internal bool Read(byte pinNumber)
        {
            return this.ReadRegister(MCP23008_GPIO, pinNumber);
        }

        internal void Write(byte pinNumber, bool value)
        {
            this.WriteRegister(MCP23008_GPIO, pinNumber, value);
        }

        private void InterruptPin_ValueChanged(object sender, GpioPinValueChangedEventArgs e)
        {
            var interruptFlags = this.ReadRegister(MCP23008_INTF);
            for (byte i = 0; i < 8; i++)
            {
                var interruptFlag = (interruptFlags & (1 << i)) != 0;
                if (interruptFlag)
                {
                    var capture = ReadRegister(MCP23008_INTCAP, i);
                    var value = this.ReadRegister(MCP23008_GPIO, i);

                    // We can now fire the individual PORT event handler.
                    if (this._gpioPin[i] != null)
                    {
                        this._gpioPin[i].DoValueChangedEvent(new GpioPinValueChangedEventArgs(capture ? GpioPinEdge.FallingEdge : GpioPinEdge.RisingEdge));
                    }
                }
            }
        }

        private byte ReadRegister(byte register)
        {
            byte[] readBuffer = new byte[1];

            this._i2cDevice.WriteRead(new byte[] { register }, readBuffer);
            return readBuffer[0];
        }

        private bool ReadRegister(byte register, byte pinNumber)
        {
            var currentValue = this.ReadRegister(register);
            return (currentValue & (1 << pinNumber)) != 0;
        }

        private void WriteRegister(byte register, byte value)
        {
            this._i2cDevice.Write(new byte[] { register, value });
        }

        private void WriteRegister(byte register, byte pinNumber, bool enable)
        {
            // First read the current value
            byte currentValue = this.ReadRegister(register);

            // Set the pins contents to the new value
            if (enable)
            {
                currentValue = (byte)(currentValue | (1 << pinNumber));
            } else
            {
                currentValue = (byte)(currentValue & ~(1 << pinNumber));
            }

            // Write the new value
            this.WriteRegister(register, currentValue);
        }

        #region IDisposable Support
        private bool _disposed = false;

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (this._interruptPin != null)
                    {
                        this._interruptPin.ValueChanged -= InterruptPin_ValueChanged;
                    }

                    for (int i = 0; i < PinCount - 1; i++)
                    {
                        this._gpioPin[i]?.Dispose();
                        this._gpioPin[i] = null;
                    }

                    this._i2cDevice?.Dispose();
                    this._i2cDevice = null;
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
