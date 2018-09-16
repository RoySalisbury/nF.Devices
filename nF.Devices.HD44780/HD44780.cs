using System;
using System.Text;
using Windows.Devices.Gpio;

namespace nF.Devices.HD44780
{
    public sealed class HD44780 : IDisposable
    {
        // commands
        private const int LCD_CLEARDISPLAY = 0x01;
        private const int LCD_RETURNHOME = 0x02;
        private const int LCD_ENTRYMODESET = 0x04;
        private const int LCDDisplayControl = 0x08;
        private const int LCD_CURSORSHIFT = 0x10;
        private const int LCD_FUNCTIONSET = 0x20;
        private const int LCD_SETCGRAMADDR = 0x40;
        private const int LCD_SETDDRAMADDR = 0x80;

        // flags for display entry mode
        private const int LCD_ENTRYRIGHT = 0x00;
        private const int LCD_ENTRYLEFT = 0x02;
        private const int LCD_ENTRYSHIFTINCREMENT = 0x01;
        private const int LCD_ENTRYSHIFTDECREMENT = 0x00;

        // flags for display on/off control
        private const int LCD_DISPLAYON = 0x04;
        private const int LCD_DISPLAYOFF = 0x00;
        private const int LCD_CURSORON = 0x02;
        private const int LCD_CURSOROFF = 0x00;
        private const int LCD_BLINKON = 0x01;
        private const int LCD_BLINKOFF = 0x00;

        // flags for display/cursor shift
        private const int LCD_DISPLAYMOVE = 0x08;
        private const int LCD_CURSORMOVE = 0x00;
        private const int LCD_MOVERIGHT = 0x04;
        private const int LCD_MOVELEFT = 0x00;

        // flags for function set
        private const int LCD_8BITMODE = 0x10;
        private const int LCD_4BITMODE = 0x00;
        private const int LCD_2LINE = 0x08;
        private const int LCD_1LINE = 0x00;
        public const int LCD_5x10DOTS = 0x04;
        public const int LCD_5x8DOTS = 0x00;

        private int _displayFunction;
        private int _displayControl;
        private int _displayMode;

        private int _cols;
        private int _rows;
        private int _currentRow;

        private string[] _buffer;

        //        public bool AutoScroll = false;

        private object _syncLock = new object();
        private GpioPin _rsPin;
        private GpioPin _rwPin;
        private GpioPin _enPin;
        private GpioPin[] _dataPins;

        private HD44780(int cols, int rows, int charSize = LCD_5x8DOTS)
        {
            this._cols = cols;
            this._rows = rows;

            this._buffer = new string[this._rows];

            this._displayFunction = charSize;
            if (this._rows > 1)
                this._displayFunction |= LCD_2LINE;
            else
                this._displayFunction |= LCD_1LINE;
        }

        public static HD44780 CreateDevice(GpioPin rsPin, GpioPin rwPin, GpioPin enPin, GpioPin d4Pin, GpioPin d5Pin, GpioPin d6Pin, GpioPin d7Pin, int cols, int rows, int charSize = LCD_5x8DOTS)
        {
            var instance = new HD44780(cols, rows, charSize);
            instance._displayFunction |= LCD_4BITMODE;

            instance._rsPin = rsPin;
            instance._rsPin.SetDriveMode(GpioPinDriveMode.Output);
            instance._rsPin.Write(GpioPinValue.Low);

            instance._rwPin = rwPin;
            instance._rwPin.SetDriveMode(GpioPinDriveMode.Output);
            instance._rwPin.Write(GpioPinValue.Low);

            instance._enPin = enPin;
            instance._enPin.SetDriveMode(GpioPinDriveMode.Output);
            instance._enPin.Write(GpioPinValue.Low);

            instance._dataPins = new GpioPin[] { d4Pin, d5Pin, d6Pin, d7Pin };
            for (int i = 0; i < instance._dataPins.Length; i++)
            {
                instance._dataPins[i].SetDriveMode(GpioPinDriveMode.Output);
                instance._dataPins[i].Write(GpioPinValue.Low);
            }

            System.Threading.Thread.Sleep(50);

            //put the LCD into 4 bit or 8 bit mode
            if ((instance._displayFunction & LCD_8BITMODE) != LCD_8BITMODE)
            {
                // we start in 8bit mode, try to set 4 bit mode
                instance.write4bits(0x03);
                System.Threading.Thread.Sleep(5);

                // second try
                instance.write4bits(0x03);
                System.Threading.Thread.Sleep(5);

                // third go!
                instance.write4bits(0x03);
                instance.delayMicroseconds(150);

                // finally, set to 4-bit interface
                instance.write4bits(0x02);
            }
            else
            {
                // Send function set command sequence
                instance.command((byte)(LCD_FUNCTIONSET | instance._displayFunction));
                System.Threading.Thread.Sleep(5);

                // second try
                instance.command((byte)(LCD_FUNCTIONSET | instance._displayFunction));
                instance.delayMicroseconds(150);

                // third go
                instance.command((byte)(LCD_FUNCTIONSET | instance._displayFunction));
            }

            instance.command((byte)(LCD_FUNCTIONSET | instance._displayFunction));
            instance._displayControl = LCD_DISPLAYON | LCD_CURSOROFF | LCD_BLINKOFF;
            instance.DisplayOn();

            instance.ClearDisplay();

            instance._displayMode = LCD_ENTRYLEFT | LCD_ENTRYSHIFTDECREMENT;
            instance.command((byte)(LCD_ENTRYMODESET | instance._displayMode));

            // This is just so we are actaully using an async/await operation in the method.
            return instance;
        }

        private void write4bits(byte value)
        {

            for (int i = 0; i < this._dataPins.Length; i++)
            {
                var x = ((value >> i) & 0x01) == 1;
                this._dataPins[i].Write(x ? GpioPinValue.High : GpioPinValue.Low);
            }

            pulseEnable();
        }

        private void pulseEnable()
        {
            this._enPin.Write(GpioPinValue.Low);
            this.delayMicroseconds(2);

            this._enPin.Write(GpioPinValue.High);
            this.delayMicroseconds(2);

            this._enPin.Write(GpioPinValue.Low);
            this.delayMicroseconds(100);
        }

        private void command(byte value)
        {
            send(value, false);
        }

        private void send(byte value, bool mode)
        {
            this._rsPin.Write(mode ? GpioPinValue.High : GpioPinValue.Low);

            byte B = (byte)((value >> 4) & 0x0F);
            this.write4bits(B);

            B = (byte)(value & 0x0F);
            this.write4bits(B);
        }

        private void writeByte(byte value)
        {
            send(value, true);
        }


        private void delayMicroseconds(int uS)
        {
            if (uS > 2000)
                throw new Exception("Invalid param, use Thread.Sleep for 2ms and more");

            if (uS < 100) //call takes more time than 100uS 
                return;

            System.Threading.Thread.Sleep(TimeSpan.FromTicks(uS * 10));

            //var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            //while ((stopWatch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency) < uS)
            //{
            //}
        }


        public void Write(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            foreach (byte c in data)
            {
                this.writeByte(c);
            }
        }

        public void WriteLine(string Text)
        {
            lock (this._syncLock)
            {
                if (this._currentRow >= this._rows)
                {
                    //let's do shift
                    for (int i = 1; i < _rows; i++)
                    {
                        this._buffer[i - 1] = this._buffer[i];
                        this.SetCursor(0, (byte)(i - 1));
                        this.Write(this._buffer[i - 1].Substring(0, this._cols));
                    }
                    this._currentRow = this._rows - 1;
                }
                this._buffer[_currentRow] = Text.PadRight(this._cols, ' ');
                this.SetCursor(0, (byte)_currentRow);
                var cuts = this._buffer[this._currentRow].Substring(0, this._cols);
                this.Write(cuts);
                this._currentRow++;
            }
        }

        public void DisplayOn()
        {
            this._displayControl |= LCD_DISPLAYON;
            this.command((byte)(LCDDisplayControl | this._displayControl));
        }

        public void DisplayOff()
        {
            this._displayControl &= ~LCD_DISPLAYON;
            this.command((byte)(LCDDisplayControl | this._displayControl));
        }

        public void ClearDisplay()
        {
            this.command(LCD_CLEARDISPLAY);
            System.Threading.Thread.Sleep(2);

            for (int i = 0; i < _rows; i++)
            {
                this._buffer[i] = "";
            }

            _currentRow = 0;

            this.HomeDisplay();
        }

        public void HomeDisplay()
        {
            this.command(LCD_RETURNHOME);
            System.Threading.Thread.Sleep(2);
        }

        public void SetCursor(byte col, byte row)
        {
            var row_offsets = new int[] { 0x00, 0x40, 0x14, 0x54 };
            this.command((byte)(LCD_SETDDRAMADDR | (col + row_offsets[row])));
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
