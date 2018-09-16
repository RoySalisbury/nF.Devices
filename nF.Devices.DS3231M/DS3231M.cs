using System;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace nF.Devices.DS3231M
{
    public sealed class DS3231M : IDisposable
    {
        private const byte BASE_ADDRESS = 0x68;

        // timekeeping registers
        private const byte DS3231_TIME_CAL_ADDR = 0x00;
        private const byte DS3231_ALARM1_ADDR = 0x07;
        private const byte DS3231_ALARM2_ADDR = 0x0B;
        private const byte DS3231_CONTROL_ADDR = 0x0E;
        private const byte DS3231_STATUS_ADDR = 0x0F;
        private const byte DS3231_AGING_OFFSET_ADDR = 0x10;
        private const byte DS3231_TEMPERATURE_ADDR = 0x11;

        // control register bits
        private const byte DS3231_A1IE = 0x1;
        private const byte DS3231_A2IE = 0x2;
        private const byte DS3231_INTCN = 0x4;

        // status register bits
        private const byte DS3231_A1F = 0x1;
        private const byte DS3231_A2F = 0x2;
        private const byte DS3231_OSF = 0x80;

        private readonly I2cDevice _i2cDevice;
        private GpioPin _alarmPin;

        private DS3231M(I2cDevice i2cDevice, GpioPin alarmPin = null) 
        {
            _i2cDevice = i2cDevice;
            _alarmPin = alarmPin;

            if (alarmPin != null)
            {
                this._alarmPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                this._alarmPin.DebounceTimeout = TimeSpan.FromTicks(5 * 10000);

                this._alarmPin.ValueChanged += AlarmPin_ValueChanged;
            }
        }

        public event GpioPinValueChangedEventHandler AlarmSignaled;

        private void AlarmPin_ValueChanged(object sender, GpioPinValueChangedEventArgs args)
        {
            this.AlarmSignaled?.Invoke(this, args);
        }

        public static DS3231M CreateDevice(string i2cBus, byte hardwareAddress = BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive, GpioPin alarmPin = null)
        {
            // Create the I2c connection settings instance.
            I2cConnectionSettings settings = new I2cConnectionSettings(hardwareAddress) { BusSpeed = busSpeed, SharingMode = sharingMode };

            // Create teh I2c device instance
            var i2cDevice = I2cDevice.FromId(i2cBus, settings);
            if (i2cDevice == null)
            {
                // No device was created
                throw new Exception("Unable to create I2c instance.");
            }

            try
            {
                var instance = new DS3231M(i2cDevice, alarmPin);
                instance._i2cDevice.Write(new byte[] { DS3231_CONTROL_ADDR, DS3231_INTCN });

                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        public DateTime GetDateTime()
        {
            var readBuffer = new byte[7];
            this._i2cDevice.WriteRead(new byte[] { DS3231_TIME_CAL_ADDR }, readBuffer);

            return new DateTime(
                (((readBuffer[5] & 0x80) >> 7) == 1) ? BcdToDec(readBuffer[6]) + 2000 : BcdToDec(readBuffer[6]) + 1900,
                BcdToDec(readBuffer[5] & 0x1F),
                BcdToDec(readBuffer[4]),
                BcdToDec(readBuffer[2]),
                BcdToDec(readBuffer[1]),
                BcdToDec(readBuffer[0])
                );
        }

        public void SetDateTime(DateTime value)
        {
            this._i2cDevice.Write(new byte[] { DS3231_TIME_CAL_ADDR,
                DecToBcd(value.Second),
                DecToBcd(value.Minute),
                DecToBcd(value.Hour),
                (byte)(((int)value.DayOfWeek + 7) % 7),
                DecToBcd(value.Day),
                (value.Year >= 2000) ? (byte)(DecToBcd(value.Month) + 0x80) : DecToBcd(value.Month),
                DecToBcd(value.Year % 100)
            });
        }

        public double GetTemperatureC()
        {
            var readBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { DS3231_TEMPERATURE_ADDR }, readBuffer);

            int msb = readBuffer[0];
            if ((msb & 0x80) != 0)
            {
                msb |= ~((1 << 8) - 1);
            }

            return 0.25 * (readBuffer[1] >> 6) + msb;
        }

        private static int BcdToDec(int value)
        {
            return ((value / 16 * 10) + (value % 16));
        }

        private static byte DecToBcd(int value)
        {
            return (byte)((value / 10 * 16) + (value % 10));
        }

        #region IDisposable Support
        private bool _disposed = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    this._i2cDevice?.Dispose();
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
