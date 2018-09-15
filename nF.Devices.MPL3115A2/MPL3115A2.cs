using System;
using Windows.Devices.I2c;

namespace nF.Devices.MPL3115A2
{
    public sealed class MPL3115A2 : IDisposable
    {
        private const byte BASE_ADDRESS = 0x60;

        private const byte MPL3115A2_REGISTER_STATUS = 0x00;
        private const byte MPL3115A2_REGISTER_STATUS_TDR = 0x02;
        private const byte MPL3115A2_REGISTER_STATUS_PDR = 0x04;
        private const byte MPL3115A2_REGISTER_STATUS_PTDR = 0x08;

        private const byte MPL3115A2_REGISTER_PRESSURE_MSB = 0x01;
        private const byte MPL3115A2_REGISTER_PRESSURE_CSB = 0x02;
        private const byte MPL3115A2_REGISTER_PRESSURE_LSB = 0x03;

        private const byte MPL3115A2_REGISTER_TEMP_MSB = 0x04;
        private const byte MPL3115A2_REGISTER_TEMP_LSB = 0x05;

        private const byte MPL3115A2_REGISTER_DR_STATUS = 0x06;

        private const byte MPL3115A2_OUT_P_DELTA_MSB = 0x07;
        private const byte MPL3115A2_OUT_P_DELTA_CSB = 0x08;
        private const byte MPL3115A2_OUT_P_DELTA_LSB = 0x09;

        private const byte MPL3115A2_OUT_T_DELTA_MSB = 0x0A;
        private const byte MPL3115A2_OUT_T_DELTA_LSB = 0x0B;

        private const byte MPL3115A2_WHOAMI = 0x0C;

        private const byte MPL3115A2_PT_DATA_CFG = 0x13;
        private const byte MPL3115A2_PT_DATA_CFG_TDEFE = 0x01;
        private const byte MPL3115A2_PT_DATA_CFG_PDEFE = 0x02;
        private const byte MPL3115A2_PT_DATA_CFG_DREM = 0x04;

        private const byte MPL3115A2_CTRL_REG1 = 0x26;
        private const byte MPL3115A2_CTRL_REG1_SBYB_STANDBY = 0x00;
        private const byte MPL3115A2_CTRL_REG1_SBYB_ACTIVE = 0x01;

        private const byte MPL3115A2_CTRL_REG1_OST = 0x02;
        private const byte MPL3115A2_CTRL_REG1_RST = 0x04;
        private const byte MPL3115A2_CTRL_REG1_OS1 = 0x00;
        private const byte MPL3115A2_CTRL_REG1_OS2 = 0x08;
        private const byte MPL3115A2_CTRL_REG1_OS4 = 0x10;
        private const byte MPL3115A2_CTRL_REG1_OS8 = 0x18;
        private const byte MPL3115A2_CTRL_REG1_OS16 = 0x20;
        private const byte MPL3115A2_CTRL_REG1_OS32 = 0x28;
        private const byte MPL3115A2_CTRL_REG1_OS64 = 0x30;
        private const byte MPL3115A2_CTRL_REG1_OS128 = 0x38;
        private const byte MPL3115A2_CTRL_REG1_RAW = 0x40;
        private const byte MPL3115A2_CTRL_REG1_ALT = 0x80;
        private const byte MPL3115A2_CTRL_REG1_BAR = 0x00;
        private const byte MPL3115A2_CTRL_REG2 = 0x27;
        private const byte MPL3115A2_CTRL_REG3 = 0x28;
        private const byte MPL3115A2_CTRL_REG4 = 0x29;
        private const byte MPL3115A2_CTRL_REG5 = 0x2A;

        private const byte MPL3115A2_REGISTER_STARTCONVERSION = 0x12;

        private I2cDevice _i2cDevice;

        private MPL3115A2(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static MPL3115A2 CreateDevice(string i2cBus, byte hardwareAddress = BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
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
                var instance = new MPL3115A2(i2cDevice);

                var readBuffer = new byte[1];
                instance._i2cDevice.WriteRead(new byte[] { MPL3115A2_WHOAMI }, readBuffer);
                if (readBuffer[0] != 0xC4)
                {
                    // We need to dispose of the i2CDevice instance
                    instance._i2cDevice.Dispose();
                    return null;
                }

                instance._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_STANDBY });
                instance._i2cDevice.Write(new byte[] { MPL3115A2_PT_DATA_CFG, MPL3115A2_PT_DATA_CFG_TDEFE | MPL3115A2_PT_DATA_CFG_PDEFE | MPL3115A2_PT_DATA_CFG_DREM });

                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        public double GetPressure()
        {
            // Update which register we want to read
            this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_ACTIVE | MPL3115A2_CTRL_REG1_OST | MPL3115A2_CTRL_REG1_OS64 | MPL3115A2_CTRL_REG1_BAR });
            try
            {
                // When the status register changes, we have an updated sample. Based on our "oversample" rate above (MPL3115A2_CTRL_REG1_OS128) this should take 512ms.
                var i2cReadBuffer = new byte[1];
                while ((i2cReadBuffer[0] & MPL3115A2_REGISTER_STATUS_PDR) == 0)
                {
                    this._i2cDevice.WriteRead(new byte[] { MPL3115A2_REGISTER_STATUS }, i2cReadBuffer);
                    System.Threading.Thread.Sleep(10);
                }

                // Get the value.  
                i2cReadBuffer = new byte[3];
                this._i2cDevice.WriteRead(new byte[] { MPL3115A2_REGISTER_PRESSURE_MSB }, i2cReadBuffer);

                long result = (long)i2cReadBuffer[0] << 16;
                result |= ((long)i2cReadBuffer[1] << 8);
                result |= ((long)i2cReadBuffer[2]);

                return result / 64.0;
            }
            finally
            {
                this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_STANDBY });
            }
        }

        public double GetAltitude()
        {
            // Update which register we want to read
            this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_ACTIVE | MPL3115A2_CTRL_REG1_OST | MPL3115A2_CTRL_REG1_OS64 | MPL3115A2_CTRL_REG1_ALT });
            try
            {
                // When the status register changes, we have an updated sample. Based on our "oversample" rate above (MPL3115A2_CTRL_REG1_OS128) this should take 512ms.
                var i2cReadBuffer = new byte[1];
                while ((i2cReadBuffer[0] & MPL3115A2_REGISTER_STATUS_PDR) == 0)
                {
                    this._i2cDevice.WriteRead(new byte[] { MPL3115A2_REGISTER_STATUS }, i2cReadBuffer);
                    System.Threading.Thread.Sleep(10);
                }

                // Get the value.  
                i2cReadBuffer = new byte[3];
                this._i2cDevice.WriteRead(new byte[] { MPL3115A2_REGISTER_PRESSURE_MSB }, i2cReadBuffer);

                int altitude = i2cReadBuffer[0] << 24;
                altitude |= (i2cReadBuffer[1] << 16);
                altitude |= (i2cReadBuffer[2] << 8);

                return altitude / 65536.0;
            }
            finally
            {
                this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_STANDBY });
            }
        }

        public double GetTemperature()
        {
            // Update which register we want to read
            this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_ACTIVE | MPL3115A2_CTRL_REG1_OST | MPL3115A2_CTRL_REG1_OS64 | MPL3115A2_CTRL_REG1_ALT });
            try
            {
                // When the status register changes, we have an updated sample. Based on our "oversample" rate above (MPL3115A2_CTRL_REG1_OS128) this should take 512ms.
                var i2cReadBuffer = new byte[1];
                while ((i2cReadBuffer[0] & MPL3115A2_REGISTER_STATUS_PDR) == 0)
                {
                    this._i2cDevice.WriteRead(new byte[] { MPL3115A2_REGISTER_STATUS }, i2cReadBuffer);
                    System.Threading.Thread.Sleep(10);
                }

                // Get the value.  
                i2cReadBuffer = new byte[2];
                this._i2cDevice.WriteRead(new byte[] { MPL3115A2_REGISTER_TEMP_MSB }, i2cReadBuffer);

                int result = i2cReadBuffer[0] << 8;
                result |= i2cReadBuffer[1];

                return result / 256.0;
            }
            finally
            {
                this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, MPL3115A2_CTRL_REG1_SBYB_STANDBY });
            }
        }

        private void EnableOneShotMode()
        {
            // Get the current settings of the register
            var readBuffer = new byte[1];
            this._i2cDevice.WriteRead(new byte[] { MPL3115A2_CTRL_REG1 }, readBuffer);

            // Clear the OST bit and write it back to the register
            readBuffer[0] = (byte)(readBuffer[0] & ~(1 << 1));
            this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, readBuffer[0] });

            // Get the current settings of the register (again, just to be safe)
            this._i2cDevice.WriteRead(new byte[] { MPL3115A2_CTRL_REG1 }, readBuffer);

            // Set the OST bit and write it back to the register
            readBuffer[0] = (byte)(readBuffer[0] | (1 << 1));
            this._i2cDevice.Write(new byte[] { MPL3115A2_CTRL_REG1, readBuffer[0] });
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
