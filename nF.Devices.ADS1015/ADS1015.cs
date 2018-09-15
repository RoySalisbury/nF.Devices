using System;
using Windows.Devices.I2c;

namespace nF.Devices.ADS1015
{
    public sealed class ADS1015 : IDisposable
    {
        private const byte BASE_ADDRESS = 0x48;    // 1001 000 (ADDR = GND)

        private const byte ADS1015_CONVERSIONDELAY = (1);
        private const byte ADS1115_CONVERSIONDELAY = (8);

        private const byte ADS1015_REG_POINTER_MASK = (0x03);
        private const byte ADS1015_REG_POINTER_CONVERT = (0x00);
        private const byte ADS1015_REG_POINTER_CONFIG = (0x01);
        private const byte ADS1015_REG_POINTER_LOWTHRESH = (0x02);
        private const byte ADS1015_REG_POINTER_HITHRESH = (0x03);

        private const ushort ADS1015_REG_CONFIG_OS_MASK = (0x8000);
        private const ushort ADS1015_REG_CONFIG_OS_SINGLE = (0x8000);  // Write: Set to start a single-conversion
        private const ushort ADS1015_REG_CONFIG_OS_BUSY = (0x0000);  // Read: Bit = 0 when conversion is in progress
        private const ushort ADS1015_REG_CONFIG_OS_NOTBUSY = (0x8000);  // Read: Bit = 1 when device is not performing a conversion

        private const ushort ADS1015_REG_CONFIG_MUX_MASK = (0x7000);
        private const ushort ADS1015_REG_CONFIG_MUX_DIFF_0_1 = (0x0000);  // Differential P = AIN0, N = AIN1 = (default)
        private const ushort ADS1015_REG_CONFIG_MUX_DIFF_0_3 = (0x1000);  // Differential P = AIN0, N = AIN3
        private const ushort ADS1015_REG_CONFIG_MUX_DIFF_1_3 = (0x2000);  // Differential P = AIN1, N = AIN3
        private const ushort ADS1015_REG_CONFIG_MUX_DIFF_2_3 = (0x3000);  // Differential P = AIN2, N = AIN3
        private const ushort ADS1015_REG_CONFIG_MUX_SINGLE_0 = (0x4000);  // Single-ended AIN0
        private const ushort ADS1015_REG_CONFIG_MUX_SINGLE_1 = (0x5000);  // Single-ended AIN1
        private const ushort ADS1015_REG_CONFIG_MUX_SINGLE_2 = (0x6000);  // Single-ended AIN2
        private const ushort ADS1015_REG_CONFIG_MUX_SINGLE_3 = (0x7000);  // Single-ended AIN3

        private const ushort ADS1015_REG_CONFIG_PGA_MASK = (0x0E00);
        private const ushort ADS1015_REG_CONFIG_PGA_6_144V = (0x0000);  // +/-6.144V range = Gain 2/3
        private const ushort ADS1015_REG_CONFIG_PGA_4_096V = (0x0200);  // +/-4.096V range = Gain 1
        private const ushort ADS1015_REG_CONFIG_PGA_2_048V = (0x0400);  // +/-2.048V range = Gain 2 = (default)
        private const ushort ADS1015_REG_CONFIG_PGA_1_024V = (0x0600);  // +/-1.024V range = Gain 4
        private const ushort ADS1015_REG_CONFIG_PGA_0_512V = (0x0800);  // +/-0.512V range = Gain 8
        private const ushort ADS1015_REG_CONFIG_PGA_0_256V = (0x0A00);  // +/-0.256V range = Gain 16

        private const ushort ADS1015_REG_CONFIG_MODE_MASK = (0x0100);
        private const ushort ADS1015_REG_CONFIG_MODE_CONTIN = (0x0000);  // Continuous conversion mode
        private const ushort ADS1015_REG_CONFIG_MODE_SINGLE = (0x0100);  // Power-down single-shot mode = (default)

        private const ushort ADS1015_REG_CONFIG_DR_MASK = (0x00E0);
        private const ushort ADS1015_REG_CONFIG_DR_128SPS = (0x0000);  // 128 samples per second
        private const ushort ADS1015_REG_CONFIG_DR_250SPS = (0x0020);  // 250 samples per second
        private const ushort ADS1015_REG_CONFIG_DR_490SPS = (0x0040);  // 490 samples per second
        private const ushort ADS1015_REG_CONFIG_DR_920SPS = (0x0060);  // 920 samples per second
        private const ushort ADS1015_REG_CONFIG_DR_1600SPS = (0x0080);  // 1600 samples per second = (default)
        private const ushort ADS1015_REG_CONFIG_DR_2400SPS = (0x00A0);  // 2400 samples per second
        private const ushort ADS1015_REG_CONFIG_DR_3300SPS = (0x00C0);  // 3300 samples per second

        private const ushort ADS1015_REG_CONFIG_CMODE_MASK = (0x0010);
        private const ushort ADS1015_REG_CONFIG_CMODE_TRAD = (0x0000);  // Traditional comparator with hysteresis = (default)
        private const ushort ADS1015_REG_CONFIG_CMODE_WINDOW = (0x0010);  // Window comparator

        private const ushort ADS1015_REG_CONFIG_CPOL_MASK = (0x0008);
        private const ushort ADS1015_REG_CONFIG_CPOL_ACTVLOW = (0x0000);  // ALERT/RDY pin is low when active = (default)
        private const ushort ADS1015_REG_CONFIG_CPOL_ACTVHI = (0x0008);  // ALERT/RDY pin is high when active

        private const ushort ADS1015_REG_CONFIG_CLAT_MASK = (0x0004);  // Determines if ALERT/RDY pin latches once asserted
        private const ushort ADS1015_REG_CONFIG_CLAT_NONLAT = (0x0000);  // Non-latching comparator = (default)
        private const ushort ADS1015_REG_CONFIG_CLAT_LATCH = (0x0004);  // Latching comparator

        private const ushort ADS1015_REG_CONFIG_CQUE_MASK = (0x0003);
        private const ushort ADS1015_REG_CONFIG_CQUE_1CONV = (0x0000);  // Assert ALERT/RDY after one conversions
        private const ushort ADS1015_REG_CONFIG_CQUE_2CONV = (0x0001);  // Assert ALERT/RDY after two conversions
        private const ushort ADS1015_REG_CONFIG_CQUE_4CONV = (0x0002);  // Assert ALERT/RDY after four conversions
        private const ushort ADS1015_REG_CONFIG_CQUE_NONE = (0x0003);  // Disable the comparator and put ALERT/RDY in high state = (default)

        public enum Gain
        {
            GAIN_TWOTHIRDS = ADS1015_REG_CONFIG_PGA_6_144V,
            GAIN_ONE = ADS1015_REG_CONFIG_PGA_4_096V,
            GAIN_TWO = ADS1015_REG_CONFIG_PGA_2_048V,
            GAIN_FOUR = ADS1015_REG_CONFIG_PGA_1_024V,
            GAIN_EIGHT = ADS1015_REG_CONFIG_PGA_0_512V,
            GAIN_SIXTEEN = ADS1015_REG_CONFIG_PGA_0_256V
        }

        private byte conversionDelay;
        private byte bitShift;

        private I2cDevice _i2cDevice;

        private ADS1015(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;

            // These are specifc to the ADS1015
            this.conversionDelay = ADS1015_CONVERSIONDELAY;
            this.bitShift = 4;
        }

        public static ADS1015 CreateDevice(string i2cBus, byte hardwareAddress = BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
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
                var instance = new ADS1015(i2cDevice);
                return instance;
            }
            catch (Exception)
            {
                i2cDevice?.Dispose();
                throw;
            }
        }

        public ushort readADC_SingleEnded(byte channel, Gain gain = Gain.GAIN_TWOTHIRDS)
        {
            if (channel > 3)
            {
                return 0;
            }

            // Start with default values
            ushort config = ADS1015_REG_CONFIG_CQUE_NONE | // Disable the comparator (default val)
                         ADS1015_REG_CONFIG_CLAT_NONLAT | // Non-latching (default val)
                         ADS1015_REG_CONFIG_CPOL_ACTVLOW | // Alert/Rdy active low   (default val)
                         ADS1015_REG_CONFIG_CMODE_TRAD | // Traditional comparator (default val)
                         ADS1015_REG_CONFIG_DR_1600SPS | // 1600 samples per second (default)
                         ADS1015_REG_CONFIG_MODE_SINGLE;   // Single-shot mode (default)

            // Set PGA/voltage range
            config |= (ushort)gain;

            // Set single-ended input channel
            switch (channel)
            {
                case (0):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_0;
                    break;
                case (1):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_1;
                    break;
                case (2):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_2;
                    break;
                case (3):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_3;
                    break;
            }

            // Set 'start single-conversion' bit
            config |= ADS1015_REG_CONFIG_OS_SINGLE;

            // Write config register to the ADC
            writeRegister(ADS1015_REG_POINTER_CONFIG, config);

            // Wait for the conversion to complete
            System.Threading.Thread.Sleep(conversionDelay);

            // Read the conversion results
            // Shift 12-bit results right 4 bits for the ADS1015
            return (ushort)(readRegister(ADS1015_REG_POINTER_CONVERT) >> bitShift);
        }

        public short readADC_Differential_0_1(Gain gain = Gain.GAIN_TWOTHIRDS)
        {
            // Start with default values
            ushort config = ADS1015_REG_CONFIG_CQUE_NONE | // Disable the comparator (default val)
                              ADS1015_REG_CONFIG_CLAT_NONLAT | // Non-latching (default val)
                              ADS1015_REG_CONFIG_CPOL_ACTVLOW | // Alert/Rdy active low   (default val)
                              ADS1015_REG_CONFIG_CMODE_TRAD | // Traditional comparator (default val)
                              ADS1015_REG_CONFIG_DR_1600SPS | // 1600 samples per second (default)
                              ADS1015_REG_CONFIG_MODE_SINGLE;   // Single-shot mode (default)

            // Set PGA/voltage range
            config |= (ushort)gain;

            // Set channels
            config |= ADS1015_REG_CONFIG_MUX_DIFF_0_1;          // AIN0 = P, AIN1 = N

            // Set 'start single-conversion' bit
            config |= ADS1015_REG_CONFIG_OS_SINGLE;

            // Write config register to the ADC
            writeRegister(ADS1015_REG_POINTER_CONFIG, config);

            // Wait for the conversion to complete
            System.Threading.Thread.Sleep(conversionDelay);

            // Read the conversion results
            ushort res = (ushort)(readRegister(ADS1015_REG_POINTER_CONVERT) >> bitShift);
            if (bitShift == 0)
            {
                return (short)res;
            }
            else
            {
                // Shift 12-bit results right 4 bits for the ADS1015,
                // making sure we keep the sign bit intact
                if (res > 0x07FF)
                {
                    // negative number - extend the sign to 16th bit
                    res |= 0xF000;
                }
                return (short)res;
            }
        }

        public short readADC_Differential_2_3(Gain gain = Gain.GAIN_TWOTHIRDS)
        {
            // Start with default values
            ushort config = ADS1015_REG_CONFIG_CQUE_NONE | // Disable the comparator (default val)
                              ADS1015_REG_CONFIG_CLAT_NONLAT | // Non-latching (default val)
                              ADS1015_REG_CONFIG_CPOL_ACTVLOW | // Alert/Rdy active low   (default val)
                              ADS1015_REG_CONFIG_CMODE_TRAD | // Traditional comparator (default val)
                              ADS1015_REG_CONFIG_DR_1600SPS | // 1600 samples per second (default)
                              ADS1015_REG_CONFIG_MODE_SINGLE;   // Single-shot mode (default)

            // Set PGA/voltage range
            config |= (ushort)gain;

            // Set channels
            config |= ADS1015_REG_CONFIG_MUX_DIFF_2_3;          // AIN2 = P, AIN3 = N

            // Set 'start single-conversion' bit
            config |= ADS1015_REG_CONFIG_OS_SINGLE;

            // Write config register to the ADC
            writeRegister(ADS1015_REG_POINTER_CONFIG, config);

            // Wait for the conversion to complete
            System.Threading.Thread.Sleep(conversionDelay);

            // Read the conversion results
            ushort res = (ushort)(readRegister(ADS1015_REG_POINTER_CONVERT) >> bitShift);
            if (bitShift == 0)
            {
                return (short)res;
            }
            else
            {
                // Shift 12-bit results right 4 bits for the ADS1015,
                // making sure we keep the sign bit intact
                if (res > 0x07FF)
                {
                    // negative number - extend the sign to 16th bit
                    res |= 0xF000;
                }
                return (short)res;
            }
        }

        public void startComparator_SingleEnded(byte channel, int threshold, Gain gain = Gain.GAIN_TWOTHIRDS)
        {
            // Start with default values
            ushort config = ADS1015_REG_CONFIG_CQUE_1CONV | // Comparator enabled and asserts on 1 match
                            ADS1015_REG_CONFIG_CLAT_LATCH | // Latching mode
                            ADS1015_REG_CONFIG_CPOL_ACTVLOW | // Alert/Rdy active low   (default val)
                            ADS1015_REG_CONFIG_CMODE_TRAD | // Traditional comparator (default val)
                            ADS1015_REG_CONFIG_DR_1600SPS | // 1600 samples per second (default)
                            ADS1015_REG_CONFIG_MODE_CONTIN | // Continuous conversion mode
                            ADS1015_REG_CONFIG_MODE_CONTIN;   // Continuous conversion mode

            // Set PGA/voltage range
            config |= (ushort)gain;

            // Set single-ended input channel
            switch (channel)
            {
                case (0):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_0;
                    break;
                case (1):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_1;
                    break;
                case (2):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_2;
                    break;
                case (3):
                    config |= ADS1015_REG_CONFIG_MUX_SINGLE_3;
                    break;
            }

            // Set the high threshold register
            // Shift 12-bit results left 4 bits for the ADS1015
            writeRegister(ADS1015_REG_POINTER_HITHRESH, (ushort)(threshold << bitShift));

            // Write config register to the ADC
            writeRegister(ADS1015_REG_POINTER_CONFIG, config);
        }

        public short getLastConversionResults()
        {
            // Wait for the conversion to complete
            System.Threading.Thread.Sleep(conversionDelay);

            // Read the conversion results
            ushort res = (ushort)(readRegister(ADS1015_REG_POINTER_CONVERT) >> bitShift);
            if (bitShift == 0)
            {
                return (short)res;
            }
            else
            {
                // Shift 12-bit results right 4 bits for the ADS1015,
                // making sure we keep the sign bit intact
                if (res > 0x07FF)
                {
                    // negative number - extend the sign to 16th bit
                    res |= 0xF000;
                }
                return (short)res;
            }
        }

        private ushort readRegister(byte reg)
        {
            byte[] readBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { reg }, readBuffer);

            return (ushort)((readBuffer[0] << 8) | readBuffer[1]);
        }

        private void writeRegister(byte reg, ushort value)
        {
            this._i2cDevice.Write(new byte[] { reg, (byte)(value >> 8), (byte)(value & 0xFF) });
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
