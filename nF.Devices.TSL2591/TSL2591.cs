using System;
using Windows.Devices.I2c;

namespace nF.Devices.TSL2591
{
    public sealed class TSL2591 : IDisposable
    {
        private const int HARDWARE_BASE_ADDRESS = 0x29;

        private const byte TSL2591_DEVICE_ID_VALUE = 0x50;
        private const byte TSL2591_DEVICE_RESET_VALUE = 0x80;
        private const byte TSL2591_COMMAND = 0x80;
        private const byte TSL2591_NORMAL_OP = 0x20;

        private const byte TSL2591_ENABLE_RW = TSL2591_COMMAND | TSL2591_NORMAL_OP | 0x00;
        private const byte TSL2591_CONTROL_RW = TSL2591_COMMAND | TSL2591_NORMAL_OP | 0x01;
        private const byte TSL2591_AILTL_RW = TSL2591_COMMAND | TSL2591_NORMAL_OP | 0x04;

        private const byte TSL2591_ID_R = TSL2591_COMMAND | TSL2591_NORMAL_OP | 0x12;
        private const byte TSL2591_C0DATAL_R = TSL2591_COMMAND | TSL2591_NORMAL_OP | 0x14;

        private const double DEFAULT_LUX_PER_COUNT = 408.0;
        private const double TSL2591_LUX_COEFB = (1.64);
        private const double TSL2591_LUX_COEFC = (0.59);
        private const double TSL2591_LUX_COEFD = (0.86);

        public enum Gain
        {
            Low = 0,
            Medium = 1,
            High = 2,
            Max = 3
        }

        public enum IntegrationTime
        {
            MS100 = 0,
            MS200 = 1,
            MS300 = 2,
            MS400 = 3,
            MS500 = 4,
            MS600 = 5,
        }

        public struct Luminosity
        {
            public double Visible;
            public double IR;
            public double Lux;
            public Gain Gain;
        }

        private I2cDevice _i2cDevice;

        private TSL2591(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static TSL2591 CreateDevice(string i2cBus, int i2cAddress = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
        {
            try
            {
                // Setup our connection settings to the I2C bus
                I2cConnectionSettings i2cSettings = new I2cConnectionSettings(i2cAddress) { BusSpeed = busSpeed, SharingMode = sharingMode };

                // Get an instance of the i2CDevice.
                var i2cDevice = I2cDevice.FromId(i2cBus, i2cSettings);

                // Create an instance of our device.
                var instance = new TSL2591(i2cDevice);

                // Reset the device (POR) [Low Gain, 100ms Integration Time]
                instance._i2cDevice.Write(new byte[] { TSL2591_CONTROL_RW, TSL2591_DEVICE_RESET_VALUE });

                // Is this device actaully a TSL2591? Simple way to check that we are really working.
                var readBuffer = new byte[1];
                instance._i2cDevice.WriteRead(new byte[] { TSL2591_ID_R }, readBuffer);

                if (readBuffer[0] != TSL2591_DEVICE_ID_VALUE)
                {
                    throw new Exception("Device is not recogonized as a TSL2591");
                }

                // Return the instance to the caller
                return instance;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Luminosity GetLuminosity(Gain gain = Gain.Low, IntegrationTime time = IntegrationTime.MS100)
        {
            var luminosity = this.GetFullLuminosity(gain, time);

            var lux = CalculateLux(luminosity[0], luminosity[1], gain, time);
            return new Luminosity() { Visible = luminosity[0], IR = luminosity[1], Lux = lux, Gain = gain };
        }

        public Luminosity GetGainAdjustedLuminosity()
        {
            Gain gain = Gain.Low;
            IntegrationTime time = IntegrationTime.MS200;

            var luminosity = this.GetFullLuminosity(gain, time);

            if ((luminosity[0] * 9876) < ushort.MaxValue)
            {
                gain = Gain.Max;

                // Sensor saturated or too low of a signal (saturated is unlikely).  Switch to MAX and read again. 
                luminosity = this.GetFullLuminosity(gain, time);
                if (luminosity[0] == 0)
                {
                    // Unable to get a reading. The light is just too low.
                    return new Luminosity() { Gain = gain };
                }
            }
            else if ((luminosity[0] * 428) < ushort.MaxValue)
            {
                // We can safely get a signal from the HIGH gain sensor
                gain = Gain.High;
                luminosity = this.GetFullLuminosity(gain, time);
            }
            else if ((luminosity[0] * 25) < ushort.MaxValue)
            {
                // We can safely get a signal from the MEDIUM gain sensor
                gain = Gain.Medium;
                luminosity = this.GetFullLuminosity(gain, time);
            }


            // Now calculate the LUX based off the visible, ir and gain values.
            var lux = this.CalculateLux(luminosity[0], luminosity[1], gain, time);

            double x1 = luminosity[0];
            double x2 = luminosity[1];

            // Adjust the visible and ir values for the gain.  So we have a base RAW value
            switch (gain)
            {
                case Gain.Medium:
                    x1 = luminosity[0] / 25.0;
                    x2 = luminosity[1] / 25.0;
                    break;
                case Gain.High:
                    x1 = luminosity[0] / 428.0;
                    x2 = luminosity[1] / 428.0;
                    break;
                case Gain.Max:
                    x1 = luminosity[0] / 9876.0;
                    x2 = luminosity[1] / 9876.0;
                    break;
            }

            return new Luminosity() { Visible = x1, IR = x2, Lux = lux, Gain = gain };
        }

        private double CalculateLux(double visible, double ir, Gain gain, IntegrationTime time)
        {
            double t = 0.0;
            double g = 0.0;

            switch (time)
            {
                case IntegrationTime.MS100:
                    t = 100.0;
                    break;
                case IntegrationTime.MS200:
                    t = 200.0;
                    break;
                case IntegrationTime.MS300:
                    t = 300.0;
                    break;
                case IntegrationTime.MS400:
                    t = 400.0;
                    break;
                case IntegrationTime.MS500:
                    t = 500.0;
                    break;
                case IntegrationTime.MS600:
                    t = 600.0;
                    break;
                default:
                    t = 100.0;
                    break;
            }

            switch (gain)
            {
                case Gain.Low:
                    g = 1.0;
                    break;
                case Gain.Medium:
                    g = 25.0;
                    break;
                case Gain.High:
                    g = 428.0;
                    break;
                case Gain.Max:
                    g = 9876.0;
                    break;
                default:
                    g = 1.0;
                    break;
            }

            var countsPerLux = (t * g) / DEFAULT_LUX_PER_COUNT;

            var lux1 = ((visible + ir) - (TSL2591_LUX_COEFB * ir)) / countsPerLux;
            var lux2 = ((TSL2591_LUX_COEFC * (visible + ir)) - (TSL2591_LUX_COEFD * ir)) / countsPerLux;

            return (lux1 > lux2 ? lux1 : lux2);
        }

        private ushort[] GetFullLuminosity(Gain gain, IntegrationTime time)
        {
            // We need to configure the device for the correct gain and integration time for this reading
            this._i2cDevice.Write(new byte[] { TSL2591_CONTROL_RW, (byte)((byte)gain << 4 | (byte)time) });
            try
            {
                // Enabled the device so that we can start a reading
                this._i2cDevice.Write(new byte[] { TSL2591_ENABLE_RW, 0x03 });
                try
                {
                    // We now need to wait for the integration time to pass so that we know we have a valid reading. The specification
                    // states that the MAX integration time per gain step is 108ms.
                    System.Threading.Thread.Sleep(120 * ((byte)time + 1));

                    var readBuffer = new byte[4];
                    this._i2cDevice.WriteRead(new byte[] { TSL2591_C0DATAL_R }, readBuffer);

                    var visible = (ushort)((readBuffer[1] << 8) | readBuffer[0]);
                    var ir = (ushort)((readBuffer[3] << 8) | readBuffer[2]);

                    return new ushort[] { (ushort)(visible - ir), ir };
                }
                finally
                {
                    this._i2cDevice.Write(new byte[] { TSL2591_ENABLE_RW, 0x00 });
                }
            }
            finally
            {
                this._i2cDevice.Write(new byte[] { TSL2591_CONTROL_RW, 0x00 });
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
