using System;
using Windows.Devices.I2c;

namespace nF.Devices.SI1145
{
    public sealed class SI1145 : IDisposable
    {
        private const byte HARDWARE_BASE_ADDRESS = 0x60;

        /* COMMANDS */
        private const byte SI1145_PARAM_QUERY = 0x80;
        private const byte SI1145_PARAM_SET = 0xA0;
        private const byte SI1145_NOP = 0x0;
        private const byte SI1145_RESET = 0x01;
        private const byte SI1145_BUSADDR = 0x02;
        private const byte SI1145_PS_FORCE = 0x05;
        private const byte SI1145_ALS_FORCE = 0x06;
        private const byte SI1145_PSALS_FORCE = 0x07;
        private const byte SI1145_PS_PAUSE = 0x09;
        private const byte SI1145_ALS_PAUSE = 0x0A;
        private const byte SI1145_PSALS_PAUSE = 0xB;
        private const byte SI1145_PS_AUTO = 0x0D;
        private const byte SI1145_ALS_AUTO = 0x0E;
        private const byte SI1145_PSALS_AUTO = 0x0F;
        private const byte SI1145_GET_CAL = 0x12;

        /* Parameters */
        private const byte SI1145_PARAM_I2CADDR = 0x00;
        private const byte SI1145_PARAM_CHLIST = 0x01;
        private const byte SI1145_PARAM_CHLIST_ENUV = 0x80;
        private const byte SI1145_PARAM_CHLIST_ENAUX = 0x40;
        private const byte SI1145_PARAM_CHLIST_ENALSIR = 0x20;
        private const byte SI1145_PARAM_CHLIST_ENALSVIS = 0x10;
        private const byte SI1145_PARAM_CHLIST_ENPS1 = 0x01;
        private const byte SI1145_PARAM_CHLIST_ENPS2 = 0x02;
        private const byte SI1145_PARAM_CHLIST_ENPS3 = 0x04;

        private const byte SI1145_PARAM_PSLED12SEL = 0x02;
        private const byte SI1145_PARAM_PSLED12SEL_PS2NONE = 0x00;
        private const byte SI1145_PARAM_PSLED12SEL_PS2LED1 = 0x10;
        private const byte SI1145_PARAM_PSLED12SEL_PS2LED2 = 0x20;
        private const byte SI1145_PARAM_PSLED12SEL_PS2LED3 = 0x40;
        private const byte SI1145_PARAM_PSLED12SEL_PS1NONE = 0x00;
        private const byte SI1145_PARAM_PSLED12SEL_PS1LED1 = 0x01;
        private const byte SI1145_PARAM_PSLED12SEL_PS1LED2 = 0x02;
        private const byte SI1145_PARAM_PSLED12SEL_PS1LED3 = 0x04;

        private const byte SI1145_PARAM_PSLED3SEL = 0x03;
        private const byte SI1145_PARAM_PSENCODE = 0x05;
        private const byte SI1145_PARAM_ALSENCODE = 0x06;

        private const byte SI1145_PARAM_PS1ADCMUX = 0x07;
        private const byte SI1145_PARAM_PS2ADCMUX = 0x08;
        private const byte SI1145_PARAM_PS3ADCMUX = 0x09;
        private const byte SI1145_PARAM_PSADCOUNTER = 0x0A;
        private const byte SI1145_PARAM_PSADCGAIN = 0x0B;
        private const byte SI1145_PARAM_PSADCMISC = 0x0C;
        private const byte SI1145_PARAM_PSADCMISC_RANGE = 0x20;
        private const byte SI1145_PARAM_PSADCMISC_PSMODE = 0x04;

        private const byte SI1145_PARAM_ALSIRADCMUX = 0x0E;
        private const byte SI1145_PARAM_AUXADCMUX = 0x0F;

        private const byte SI1145_PARAM_ALSVISADCOUNTER = 0x10;
        private const byte SI1145_PARAM_ALSVISADCGAIN = 0x11;
        private const byte SI1145_PARAM_ALSVISADCMISC = 0x12;
        private const byte SI1145_PARAM_ALSVISADCMISC_VISRANGE = 0x20;

        private const byte SI1145_PARAM_ALSIRADCOUNTER = 0x1D;
        private const byte SI1145_PARAM_ALSIRADCGAIN = 0x1E;
        private const byte SI1145_PARAM_ALSIRADCMISC = 0x1F;
        private const byte SI1145_PARAM_ALSIRADCMISC_RANGE = 0x20;

        private const byte SI1145_PARAM_ADCCOUNTER_511CLK = 0x70;

        private const byte SI1145_PARAM_ADCMUX_SMALLIR = 0x00;
        private const byte SI1145_PARAM_ADCMUX_LARGEIR = 0x03;


        /* REGISTERS */
        private const byte SI1145_REG_PARTID = 0x00;
        private const byte SI1145_REG_REVID = 0x01;
        private const byte SI1145_REG_SEQID = 0x02;

        private const byte SI1145_REG_INTCFG = 0x03;
        private const byte SI1145_REG_INTCFG_INTOE = 0x01;
        private const byte SI1145_REG_INTCFG_INTMODE = 0x02;

        private const byte SI1145_REG_IRQEN = 0x04;
        private const byte SI1145_REG_IRQEN_ALSEVERYSAMPLE = 0x01;
        private const byte SI1145_REG_IRQEN_PS1EVERYSAMPLE = 0x04;
        private const byte SI1145_REG_IRQEN_PS2EVERYSAMPLE = 0x08;
        private const byte SI1145_REG_IRQEN_PS3EVERYSAMPLE = 0x10;

        private const byte SI1145_REG_IRQMODE1 = 0x05;
        private const byte SI1145_REG_IRQMODE2 = 0x06;

        private const byte SI1145_REG_HWKEY = 0x07;
        private const byte SI1145_REG_MEASRATE0 = 0x08;
        private const byte SI1145_REG_MEASRATE1 = 0x09;
        private const byte SI1145_REG_PSRATE = 0x0A;
        private const byte SI1145_REG_PSLED21 = 0x0F;
        private const byte SI1145_REG_PSLED3 = 0x10;
        private const byte SI1145_REG_UCOEFF0 = 0x13;
        private const byte SI1145_REG_UCOEFF1 = 0x14;
        private const byte SI1145_REG_UCOEFF2 = 0x15;
        private const byte SI1145_REG_UCOEFF3 = 0x16;
        private const byte SI1145_REG_PARAMWR = 0x17;
        private const byte SI1145_REG_COMMAND = 0x18;
        private const byte SI1145_REG_RESPONSE = 0x20;
        private const byte SI1145_REG_IRQSTAT = 0x21;
        private const byte SI1145_REG_IRQSTAT_ALS = 0x01;

        private const byte SI1145_REG_ALSVISDATA0 = 0x22;
        private const byte SI1145_REG_ALSVISDATA1 = 0x23;
        private const byte SI1145_REG_ALSIRDATA0 = 0x24;
        private const byte SI1145_REG_ALSIRDATA1 = 0x25;
        private const byte SI1145_REG_PS1DATA0 = 0x26;
        private const byte SI1145_REG_PS1DATA1 = 0x27;
        private const byte SI1145_REG_PS2DATA0 = 0x28;
        private const byte SI1145_REG_PS2DATA1 = 0x29;
        private const byte SI1145_REG_PS3DATA0 = 0x2A;
        private const byte SI1145_REG_PS3DATA1 = 0x2B;
        private const byte SI1145_REG_UVINDEX0 = 0x2C;
        private const byte SI1145_REG_UVINDEX1 = 0x2D;
        private const byte SI1145_REG_PARAMRD = 0x2E;
        private const byte SI1145_REG_CHIPSTAT = 0x30;

        private I2cDevice _i2cDevice;

        private SI1145(I2cDevice i2cDevice)
        {
            _i2cDevice = i2cDevice;
        }

        public static SI1145 CreateDevice(string i2cBus, int i2cAddress = HARDWARE_BASE_ADDRESS, I2cBusSpeed busSpeed = I2cBusSpeed.StandardMode, I2cSharingMode sharingMode = I2cSharingMode.Exclusive)
        {
            try
            {
                // Setup our connection settings to the I2C bus
                I2cConnectionSettings i2cSettings = new I2cConnectionSettings(i2cAddress) { BusSpeed = busSpeed, SharingMode = sharingMode };

                // Get an instance of the i2CDevice.
                var i2cDevice = I2cDevice.FromId(i2cBus, i2cSettings);

                // Create an instance of our device.
                var instance = new SI1145(i2cDevice);

                var readBuffer = new byte[1];
                instance._i2cDevice.WriteRead(new byte[] { SI1145_REG_PARTID }, readBuffer);

                if (readBuffer[0] != 0x45)
                {
                    throw new Exception("Device is not recogonized as a SI1145");
                }

                // Power on reset
                instance._i2cDevice.Write(new byte[] { SI1145_REG_MEASRATE0, 0 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_MEASRATE1, 0 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_IRQEN, 0 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_IRQMODE1, 0 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_IRQMODE2, 0 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_INTCFG, 0 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_IRQSTAT, 0xFF });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_COMMAND, SI1145_RESET });
                System.Threading.Thread.Sleep(10);

                instance._i2cDevice.Write(new byte[] { SI1145_REG_HWKEY, 0x17 });
                System.Threading.Thread.Sleep(10);

                // enable UVindex measurement coefficients!
                instance._i2cDevice.Write(new byte[] { SI1145_REG_UCOEFF0, 0x29 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_UCOEFF1, 0x89 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_UCOEFF2, 0x02 });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_UCOEFF3, 0x00 });

                // enable UV sensor
                instance.writeParam(SI1145_PARAM_CHLIST, SI1145_PARAM_CHLIST_ENUV | SI1145_PARAM_CHLIST_ENALSIR | SI1145_PARAM_CHLIST_ENALSVIS | SI1145_PARAM_CHLIST_ENPS1);

                // enable interrupt on every sample
                instance._i2cDevice.Write(new byte[] { SI1145_REG_INTCFG, SI1145_REG_INTCFG_INTOE });
                instance._i2cDevice.Write(new byte[] { SI1145_REG_IRQEN, SI1145_REG_IRQEN_ALSEVERYSAMPLE });

                /****************************** Prox Sense 1 */
                // program LED current
                instance._i2cDevice.Write(new byte[] { SI1145_REG_PSLED21, 0x03 }); // 20mA for LED 1 only
                instance.writeParam(SI1145_PARAM_PS1ADCMUX, SI1145_PARAM_ADCMUX_LARGEIR);

                // prox sensor #1 uses LED #1
                instance.writeParam(SI1145_PARAM_PSLED12SEL, SI1145_PARAM_PSLED12SEL_PS1LED1);

                // fastest clocks, clock div 1
                instance.writeParam(SI1145_PARAM_PSADCGAIN, 0);

                // take 511 clocks to measure
                instance.writeParam(SI1145_PARAM_PSADCOUNTER, SI1145_PARAM_ADCCOUNTER_511CLK);

                // in prox mode, high range
                instance.writeParam(SI1145_PARAM_PSADCMISC, SI1145_PARAM_PSADCMISC_RANGE | SI1145_PARAM_PSADCMISC_PSMODE);

                instance.writeParam(SI1145_PARAM_ALSIRADCMUX, SI1145_PARAM_ADCMUX_SMALLIR);

                // fastest clocks, clock div 1
                instance.writeParam(SI1145_PARAM_ALSIRADCGAIN, 0);

                // take 511 clocks to measure
                instance.writeParam(SI1145_PARAM_ALSIRADCOUNTER, SI1145_PARAM_ADCCOUNTER_511CLK);

                // in high range mode
                instance.writeParam(SI1145_PARAM_ALSIRADCMISC, SI1145_PARAM_ALSIRADCMISC_RANGE);

                // fastest clocks, clock div 1
                instance.writeParam(SI1145_PARAM_ALSVISADCGAIN, 0);

                // take 511 clocks to measure
                instance.writeParam(SI1145_PARAM_ALSVISADCOUNTER, SI1145_PARAM_ADCCOUNTER_511CLK);

                // in high range mode (not normal signal)
                instance.writeParam(SI1145_PARAM_ALSVISADCMISC, SI1145_PARAM_ALSVISADCMISC_VISRANGE);

                /************************/
                // measurement rate for auto
                instance._i2cDevice.Write(new byte[] { SI1145_REG_MEASRATE0, 0xFF }); // 255 * 31.25uS = 8ms

                // auto run
                instance._i2cDevice.Write(new byte[] { SI1145_REG_COMMAND, SI1145_PSALS_AUTO });

                // Return the instance to the caller
                return instance;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private byte writeParam(byte p, byte v)
        {
            this._i2cDevice.Write(new byte[] { SI1145_REG_PARAMWR, v });
            this._i2cDevice.Write(new byte[] { SI1145_REG_COMMAND, (byte)(p | SI1145_PARAM_SET) });

            var readBuffer = new byte[1];
            this._i2cDevice.WriteRead(new byte[] { SI1145_REG_PARAMRD }, readBuffer);

            return readBuffer[0];
        }

        public float ReadIR()
        {
            byte[] readBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { SI1145_REG_ALSIRDATA0 }, readBuffer);

            return ((readBuffer[1] << 8) | readBuffer[0]);
        }

        public float ReadVisible()
        {
            byte[] readBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { SI1145_REG_ALSVISDATA0 }, readBuffer);

            return ((readBuffer[1] << 8) | readBuffer[0]);
        }

        public float ReadUV()
        {
            byte[] readBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { SI1145_REG_UVINDEX0 }, readBuffer);

            return ((readBuffer[1] << 8) | readBuffer[0]) / 100.0F;
        }

        public float ReadProximity()
        {
            byte[] readBuffer = new byte[2];
            this._i2cDevice.WriteRead(new byte[] { SI1145_REG_PS1DATA0 }, readBuffer);

            return ((readBuffer[1] << 8) | readBuffer[0]);
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
