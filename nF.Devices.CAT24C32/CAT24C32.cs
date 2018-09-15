using System;
using Windows.Devices.I2c;

namespace nF.Devices.CAT24C32
{
    public sealed class CAT24C32 : IDisposable
    {
        private const byte BASE_ADDRESS = 0x50;
        private const byte MAX_PAGES = 128;
        private const byte BYTES_PER_PAGE = 32;

        private const int MAX_BUFFER = MAX_PAGES * BYTES_PER_PAGE;

        private readonly I2cDevice _i2cDevice;

        private CAT24C32(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static CAT24C32 CreateDevice(string i2cBus, byte hardwareAddress = BASE_ADDRESS, bool powerOnReset = true, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
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
                var instance = new CAT24C32(i2cDevice);
                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        public byte[] ReadEEPROM(int bytesToRead)
        {
            var buffer = new byte[bytesToRead > MAX_BUFFER ? MAX_BUFFER : bytesToRead];
            this._i2cDevice.WriteRead(new byte[] { 0, 0 }, buffer);

            return buffer;
        }

        public void WriteEEPROM(byte[] data)
        {
            var buffer = new byte[MAX_BUFFER];
            Array.Copy(data, buffer, (data.Length > MAX_BUFFER) ? MAX_BUFFER : data.Length);

            for (byte i = 0; i < MAX_PAGES; i++)
            {
                var address = BitConverter.GetBytes((short)(i * 32));

                // TODO: Need to figure out the equilivent code for these Linq expressions.
                //var dataToWrite = address
                //    .Reverse()
                //    .Concat(buffer.Skip(i * BYTES_PER_PAGE).Take(BYTES_PER_PAGE).ToArray())
                //    .ToArray();

                //this._i2cDevice.Write(dataToWrite);
            }
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
