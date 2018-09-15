using System;
using Windows.Devices.I2c;

namespace nF.Devices.MLX90614
{
    public sealed class MLX90614 : IDisposable
    {
        private const int HARDWARE_BASE_ADDRESS = 0x5A;

        // RAM
        private const byte MLX90614_RAWIR1 = 0x04;
        private const byte MLX90614_RAWIR2 = 0x05;
        private const byte MLX90614_TA = 0x06;
        private const byte MLX90614_TOBJ1 = 0x07;
        private const byte MLX90614_TOBJ2 = 0x08;

        // EEPROM
        private const byte MLX90614_TOMAX = 0x20;
        private const byte MLX90614_TOMIN = 0x21;
        private const byte MLX90614_PWMCTRL = 0x22;
        private const byte MLX90614_TARANGE = 0x23;
        private const byte MLX90614_EMISS = 0x24;
        private const byte MLX90614_CONFIG = 0x25;
        private const byte MLX90614_ADDR = 0x0E;
        private const byte MLX90614_ID1 = 0x3C;
        private const byte MLX90614_ID2 = 0x3D;
        private const byte MLX90614_ID3 = 0x3E;
        private const byte MLX90614_ID4 = 0x3F;

        private I2cDevice _i2cDevice;

        private MLX90614(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static MLX90614 CreateDevice(string i2cBus, int i2cAddress = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
        {
            try
            {
                // Setup our connection settings to the I2C bus
                I2cConnectionSettings i2cSettings = new I2cConnectionSettings(i2cAddress) { BusSpeed = busSpeed, SharingMode = sharingMode };

                // Get an instance of the i2CDevice.
                var i2cDevice = I2cDevice.FromId(i2cBus, i2cSettings);

                // Create an instance of our device.
                var instance = new MLX90614(i2cDevice);

                // Set the defaults for our device

                // Return the instance to the caller
                return instance;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public double GetAmbientTemperature()
        {
            return this.GetTemperature(MLX90614_TA);
        }

        public double GetObjectTemperature()
        {
            return this.GetTemperature(MLX90614_TOBJ1);
        }

        private double GetTemperature(byte register)
        {
            byte[] readBuffer = new byte[3];
            this._i2cDevice.WriteRead(new byte[] { register }, readBuffer);

            var x = (((readBuffer[1] & 0x7F) << 8) + readBuffer[0]) * 0.02; // Return value in Kelvin
            return x - 273.15; // Return value in C
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
