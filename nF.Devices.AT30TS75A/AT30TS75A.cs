using System;
using Windows.Devices.I2c;

namespace nF.Devices.AT30TS75A
{
    public sealed class AT30TS75A : IDisposable
    {
        private const byte BASE_ADDRESS = 0x48;
        private const byte CMD_ACCESS_TH = 0xA1;
        private const byte CMD_ACCESS_TL = 0xA2;
        private const byte CMD_ACCESS_CONFIG = 0xAC;

        private const byte CMD_START_CONVERT = 0x51;
        private const byte CMD_STOP_CONVERT = 0x22;
        private const byte CMD_READ_TEMP = 0xAA;

        private readonly I2cDevice _i2cDevice = null;

        private AT30TS75A(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static AT30TS75A CreateDevice(string i2cBus, byte hardwareAddress = BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
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
                var instance = new AT30TS75A(i2cDevice);

                // Configure the device (normal operation, 12 bit percision)
                instance._i2cDevice.Write(new byte[] { 0x01, 0b01100010 });

                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        private byte ReadConfig()
        {
            var buffer = new byte[1];
            this._i2cDevice.WriteRead(new byte[] { 0x01 }, buffer);
            return buffer[0];
        }

        public double ReadTemperature()
        {
            var i2cReadBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { 0x00 }, i2cReadBuffer);

            var digitalTemp = ((i2cReadBuffer[0]) << 4) | (i2cReadBuffer[1] >> 4);
            // Temperature data can be + or -, if it should be negative,
            // convert 12 bit to 16 bit and use the 2s compliment.
            if (digitalTemp > 0x7FF)
            {
                digitalTemp |= 0xF000;
            }

            var digitalTempC = (digitalTemp * 0.0625);
            var digitalTempF = digitalTempC * 9.0 / 5.0 + 32.0;

            return digitalTempF;
        }

        #region IDisposable Support
        private bool _disposed = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
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
