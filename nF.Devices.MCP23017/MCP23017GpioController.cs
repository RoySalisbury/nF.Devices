using System;
using System.Text;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace nF.Devices.MCP23017
{
    /// <summary>
    /// MCP23017 - i2c 16 input/output port expander
    /// 
    /// Use this chip from 2.7-5.5V (good for any 3.3V or 5V setup), and you can sink/source up to 20mA from any of the I/O pins.
    /// </summary>
    /// <remarks><![CDATA[
    /// MCP23008 (N) DIP18 pin layout:
    /// 
    ///   28 27 26 25 24 23 22 21 20 19 18 17 16 15
    ///   │  │  │  │  │  │  │  │  │  │  │  │  │  │
    /// ███████████████████████████████████████████
    /// ▀██████████████████████████████████████████
    ///   █████████████████████████████████████████
    /// ▄██████████████████████████████████████████
    /// ███████████████████████████████████████████
    ///   │  │  │  │  │  │  │  │  │  │  │  │  │  │
    ///   1  2  3  4  5  6  7  8  9  10 11 12 13 14
    ///  
    /// 1   GPBO                28 GPA7
    /// 2   GPB1                27 GPA6
    /// 3   GPB2                26 GPA5
    /// 4   GPB3                25 GPA4
    /// 5   GPB4                24 GPA3 
    /// 6   GPB5                23 GPA2 
    /// 7   GPB6                22 GPA1 
    /// 8   GPB7                21 GPA0
    /// 9   VDD  (3.3v)         20 INTA
    /// 10  VSS  (GND)          19 INTB
    /// 11  NC                  18 RESET
    /// 12  SCL                 17 A2   
    /// 13  SDA                 16 A1   
    /// 14  NC                  15 A0   
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
    public sealed class MCP23017GpioController : IGpioController
    {
        private const int HARDWARE_BASE_ADDRESS = 0x20;

        private const byte MCP23017_IODIRA = 0x00;
        private const byte MCP23017_IPOLA = 0x02;
        private const byte MCP23017_GPINTENA = 0x04;
        private const byte MCP23017_DEFVALA = 0x06;
        private const byte MCP23017_INTCONA = 0x08;
        private const byte MCP23017_IOCONA = 0x0A;
        private const byte MCP23017_GPPUA = 0x0C;
        private const byte MCP23017_INTFA = 0x0E;
        private const byte MCP23017_INTCAPA = 0x10;
        private const byte MCP23017_GPIOA = 0x12;
        private const byte MCP23017_OLATA = 0x14;

        private const byte MCP23017_IODIRB = 0x01;
        private const byte MCP23017_IPOLB = 0x03;
        private const byte MCP23017_GPINTENB = 0x05;
        private const byte MCP23017_DEFVALB = 0x07;
        private const byte MCP23017_INTCONB = 0x09;
        private const byte MCP23017_IOCONB = 0x0B;
        private const byte MCP23017_GPPUB = 0x0D;
        private const byte MCP23017_INTFB = 0x0F;
        private const byte MCP23017_INTCAPB = 0x11;
        private const byte MCP23017_GPIOB = 0x13;
        private const byte MCP23017_OLATB = 0x15;

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
        private IGpioPin _interruptPinA = null;
        private IGpioPin _interruptPinB = null;

        private readonly MCP23017GpioPin[] _gpioPin;

        private MCP23017GpioController(I2cDevice i2cDevice, IGpioPin interruptPinA = null, IGpioPin interruptPinB = null)
        {
            _i2cDevice = i2cDevice;
            _interruptPinA = interruptPinA;

            if (_interruptPinA != null)
            {
                _interruptPinA.SetDriveMode(GpioPinDriveMode.InputPullUp);
                _interruptPinA.DebounceTimeout = TimeSpan.FromTicks(0);
                _interruptPinA.ValueChanged += InterruptPinA_ValueChanged;
            }

            _interruptPinB = interruptPinB;

            if (_interruptPinB != null)
            {
                _interruptPinB.SetDriveMode(GpioPinDriveMode.InputPullUp);
                _interruptPinB.DebounceTimeout = TimeSpan.FromTicks(0);
                _interruptPinB.ValueChanged += InterruptPinB_ValueChanged;
            }

            this._gpioPin = new MCP23017GpioPin[PinCount];
        }

        public static MCP23017GpioController CreateDevice(string i2cBus, int address = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive, IGpioPin interruptPin = null)
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
                var instance = new MCP23017GpioController(i2cDevice, interruptPin);

                // Set the defaults for our device. These are power on defaults
                instance.WriteRegister(MCP23017_IODIRA, 0xFF);
                instance.WriteRegister(MCP23017_IPOLA, 0x00);
                instance.WriteRegister(MCP23017_GPINTENA, 0x00);
                instance.WriteRegister(MCP23017_DEFVALA, 0x00);
                instance.WriteRegister(MCP23017_INTCONA, 0x00);
                instance.WriteRegister(MCP23017_IOCONA, 0x00);
                instance.WriteRegister(MCP23017_GPPUA, 0x00);
                instance.WriteRegister(MCP23017_GPIOA, 0x00);
                instance.WriteRegister(MCP23017_OLATA, 0x00);

                instance.WriteRegister(MCP23017_IODIRB, 0xFF);
                instance.WriteRegister(MCP23017_IPOLB, 0x00);
                instance.WriteRegister(MCP23017_GPINTENB, 0x00);
                instance.WriteRegister(MCP23017_DEFVALB, 0x00);
                instance.WriteRegister(MCP23017_INTCONB, 0x00);
                instance.WriteRegister(MCP23017_IOCONB, 0x00);
                instance.WriteRegister(MCP23017_GPPUB, 0x00);
                instance.WriteRegister(MCP23017_GPIOB, 0x00);
                instance.WriteRegister(MCP23017_OLATB, 0x00);

                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        public int PinCount => 16;

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

            if ((pinNumber < 0) || (pinNumber > PinCount - 1))
            {
                openStatus = GpioOpenStatus.UnknownError;
                return false;
            }

            if ((_gpioPin[pinNumber] == null) || (this._gpioPin[pinNumber].IsDisposed))
            {
                this._gpioPin[pinNumber] = new MCP23017GpioPin(this, pinNumber, sharingMode);
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
            var p = (pinNumber < 8) ? pinNumber : pinNumber - 8;

            var address = (pinNumber < 8) ? MCP23017_IODIRA : MCP23017_IODIRB;
            this.WriteRegister(address, (byte)p, isInput);

            address = (pinNumber < 8) ? MCP23017_DEFVALA : MCP23017_DEFVALB;
            this.WriteRegister(address, (byte)p, isInput);

            // Clear out any pending interrupt before enabling/disabling the pin
            address = (pinNumber < 8) ? MCP23017_GPIOA : MCP23017_GPIOB;
            this.ReadRegister(address, (byte)p);

            address = (pinNumber < 8) ? MCP23017_GPINTENA : MCP23017_GPINTENB;
            this.WriteRegister(address, (byte)p, isInput);
        }

        internal void SetPinPullup(byte pinNumber, bool value)
        {
            var address = (pinNumber < 8) ? MCP23017_GPPUA : MCP23017_GPPUB;
            var p = (pinNumber < 8) ? pinNumber : pinNumber - 8;

            this.WriteRegister(address, (byte)p, value);
        }

        internal bool Read(byte pinNumber)
        {
            var address = (pinNumber < 8) ? MCP23017_GPIOA : MCP23017_GPIOB;
            var p = (pinNumber < 8) ? pinNumber : pinNumber - 8;

            return this.ReadRegister(address, (byte)p);
        }

        internal void Write(byte pinNumber, bool value)
        {
            var address = (pinNumber < 8) ? MCP23017_GPIOA : MCP23017_GPIOB;
            var p = (pinNumber < 8) ? pinNumber : pinNumber - 8;

            this.WriteRegister(address, (byte)p, value);
        }

        private void InterruptPinA_ValueChanged(object sender, GpioPinValueChangedEventArgs e)
        {
            var interruptFlags = this.ReadRegister(MCP23017_INTFA);
            for (byte i = 0; i < 8; i++)
            {
                var interruptFlag = (interruptFlags & (1 << i)) != 0;
                if (interruptFlag)
                {
                    var capture = ReadRegister(MCP23017_INTCAPA, i);
                    var value = this.ReadRegister(MCP23017_GPIOA, i);

                    // We can now fire the individual PORT event handler.
                    if (this._gpioPin[i] != null)
                    {
                        this._gpioPin[i].DoValueChangedEvent(new GpioPinValueChangedEventArgs(capture ? GpioPinEdge.FallingEdge : GpioPinEdge.RisingEdge));
                    }
                }
            }
        }

        private void InterruptPinB_ValueChanged(object sender, GpioPinValueChangedEventArgs e)
        {
            var interruptFlags = this.ReadRegister(MCP23017_INTFB);
            for (byte i = 0; i < 8; i++)
            {
                var interruptFlag = (interruptFlags & (1 << i)) != 0;
                if (interruptFlag)
                {
                    var capture = ReadRegister(MCP23017_INTCAPB, i);
                    var value = this.ReadRegister(MCP23017_GPIOB, i);

                    // We can now fire the individual PORT event handler.
                    if (this._gpioPin[i+8] != null)
                    {
                        this._gpioPin[i+8].DoValueChangedEvent(new GpioPinValueChangedEventArgs(capture ? GpioPinEdge.FallingEdge : GpioPinEdge.RisingEdge));
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
            }
            else
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
                    if (this._interruptPinA != null)
                    {
                        this._interruptPinA.ValueChanged -= InterruptPinA_ValueChanged;
                    }

                    if (this._interruptPinB != null)
                    {
                        this._interruptPinB.ValueChanged -= InterruptPinB_ValueChanged;
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
