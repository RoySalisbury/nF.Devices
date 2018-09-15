using System;
using Windows.Devices.I2c;

namespace nF.Devices.DS1307
{
    public sealed class DS1307 
    {
        // Real time clock I2C address
        private const byte HARDWARE_BASE_ADDRESS = 0x68;

        // Total size of the user RAM block
        private const byte DS1307_RAM_SIZE = 56;

        // Start / End addresses of the user RAM registers
        private const byte DS1307_RAM_START_ADDRESS = 0x08;
        private const byte DS1307_RAM_END_ADDRESS = 0x3f;

        // Start / End addresses of the date/time registers
        private const byte DS1307_RTC_START_ADDRESS = 0x00;
        private const byte DS1307_RTC_END_ADDRESS = 0x06;

        // Square wave frequency generator register address
        private const byte DS1307_SQUARE_WAVE_CTRL_REGISTER_ADDRESS = 0x07;

        // Defines the frequency of the signal on the SQW interrupt pin on the clock when enabled
        public enum SquareWaveFrequency { SQW_1Hz, SQW_4kHz, SQW_8kHz, SQW_32kHz, SQW_Off };

        // Defines the logic level on the SQW pin when the frequency is disabled
        public enum SquareWaveDisabledOutputControl { Zero, One };

        private I2cDevice _i2cDevice;

        private DS1307(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static DS1307 CreateDevice(string i2cBus, int i2cAddress = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
        {
            try
            {
                // Setup our connection settings to the I2C bus
                I2cConnectionSettings i2cSettings = new I2cConnectionSettings(i2cAddress) { BusSpeed = busSpeed, SharingMode = sharingMode };

                // Get an instance of the i2CDevice.
                var i2cDevice = I2cDevice.FromId(i2cBus, i2cSettings);

                // Create an instance of our device.
                var instance = new DS1307(i2cDevice);

                // Set the defaults for our device

                // Return the instance to the caller
                return instance;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public DateTime GetDateTime()
        {
            byte[] clockData = new byte[7];
            this._i2cDevice.WriteRead(new byte[] { DS1307_RTC_START_ADDRESS }, clockData);

            return new DateTime(
              BcdToDec(clockData[6]) + 2000, // year
              BcdToDec(clockData[5]), // month
              BcdToDec(clockData[4]), // day
              BcdToDec(clockData[2] & 0x3f), // hours over 24 hours
              BcdToDec(clockData[1]), // minutes
              BcdToDec(clockData[0] & 0x7f) // seconds
            );
        }

        public void SetDateTime(DateTime value)
        {
            this._i2cDevice.Write(new byte[]
            {
              DS1307_RTC_START_ADDRESS,
              DecToBcd(value.Second),
              DecToBcd(value.Minute),
              DecToBcd(value.Hour),
              DecToBcd((int)value.DayOfWeek),
              DecToBcd(value.Day),
              DecToBcd(value.Month),
              DecToBcd(value.Year - 2000)
            });
        }

        public byte[] GetNonVolatileRam()
        {
            byte[] result = new byte[DS1307_RAM_SIZE];
            this._i2cDevice.WriteRead(new byte[] { DS1307_RAM_START_ADDRESS }, result);

            return result;
        }

        public void SetNonVolatileRam(byte[] value)
        {
            if ((value == null) || (value.Length != DS1307_RAM_SIZE))
            {
                throw new ArgumentOutOfRangeException("Invalid buffer length");
            }

            // Allocate a new buffer large enough to include the RAM start address byte and the payload
            var transactionBuffer = new byte[sizeof(byte) /*Address byte*/ + DS1307_RAM_SIZE];

            // Set the RAM start address
            transactionBuffer[0] = DS1307_RAM_START_ADDRESS;

            // Copy the user buffer after the address
            value.CopyTo(transactionBuffer, 1);

            // Write to the clock's RAM
            this._i2cDevice.Write(transactionBuffer);
        }

        public void SetSquareWave(SquareWaveFrequency squareWaveFrequency, SquareWaveDisabledOutputControl outputControl = SquareWaveDisabledOutputControl.Zero)
        {
            byte register = (byte)outputControl;

            register <<= 3;   // bit 7 defines the square wave output level when disabled
            // bit 6 & 5 are unused

            if (squareWaveFrequency != SquareWaveFrequency.SQW_Off)
            {
                register |= 1;
            }

            register <<= 4; // bit 4 defines if the oscillator generating the square wave frequency is on or off.
            // bit 3 & 2 are unused

            register |= (byte)squareWaveFrequency; // bit 1 & 0 define the frequency of the square wave
            this._i2cDevice.Write(new byte[] { DS1307_SQUARE_WAVE_CTRL_REGISTER_ADDRESS, register });
        }

        private static int BcdToDec(int value)
        {
            return ((value / 16 * 10) + (value % 16));
        }

        private static byte DecToBcd(int value)
        {
            return (byte)((value / 10 * 16) + (value % 10));
        }
    }
}
