using System;
using Windows.Devices.I2c;

namespace nF.Devices.HTU21DF
{
    public sealed class HTU21DF : IDisposable
    {
        private const byte HARDWARE_BASE_ADDRESS = 0x40;

        private const byte HTU21DF_READTEMP = 0xE3;
        private const byte HTU21DF_READHUM = 0xE5;
        private const byte HTU21DF_WRITEREG = 0xE6;
        private const byte HTU21DF_READREG = 0xE7;
        private const byte HTU21DF_RESET = 0xFE;

        private I2cDevice _i2cDevice;

        private HTU21DF(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static HTU21DF CreateDeviceAsync(string i2cBus, int i2cAddress = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
        {
            try
            {
                // Setup our connection settings to the I2C bus
                I2cConnectionSettings i2cSettings = new I2cConnectionSettings(i2cAddress) { BusSpeed = busSpeed, SharingMode = sharingMode };

                // Get an instance of the i2CDevice.
                var i2cDevice = I2cDevice.FromId(i2cBus, i2cSettings);

                // Create an instance of our device.
                var instance = new HTU21DF(i2cDevice);

                // Reset the device (POR)
                instance._i2cDevice.Write(new byte[] { HTU21DF_RESET });
                System.Threading.Thread.Sleep(15);

                var readBuffer = new byte[1];
                instance._i2cDevice.WriteRead(new byte[] { HTU21DF_READREG }, readBuffer);

                if (readBuffer[0] != 0x02)
                {
                    throw new Exception("Device is not recogonized as a HTU21DF");
                }

                // Return the instance to the caller
                return instance;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public double ReadTemperature()
        {
            this._i2cDevice.Write(new byte[] { HTU21DF_READTEMP });

            // Add a delay between the rquest and actual read. The spec says a MAX of 50 ms @ 14 bit resolution.
            System.Threading.Thread.Sleep(50);

            var readBuffer = new byte[3];
            this._i2cDevice.Read(readBuffer);

            return ((((readBuffer[0] << 8) | readBuffer[1]) / 65536.0) * 175.72) - 46.85;
        }

        public double ReadHumidity()
        {
            this._i2cDevice.Write(new byte[] { HTU21DF_READHUM });

            // Add a delay between the rquest and actual read. The spec says a MAX of 50 ms @ 14 bit resolution.
            System.Threading.Thread.Sleep(50);

            var readBuffer = new byte[3];
            this._i2cDevice.Read(readBuffer);

            return ((((readBuffer[0] << 8) | readBuffer[1]) / 65536.0) * 125.0) - 6;
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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
