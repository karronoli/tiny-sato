namespace TinySato
{
    using System;

    partial class Printer
    {
        protected int soft_offset_x = 0;
        protected int soft_offset_y = 0;

        public void MoveToX(int x)
        {
            var _x = x + soft_offset_x;
            if (!(1 <= _x && _x <= 9999))
                throw new TinySatoArgumentException("Specify 1-9999 dots.");
            Add(string.Format("H{0:D4}", _x));
        }

        public void MoveToY(int y)
        {
            var _y = y + soft_offset_y;
            if (!(1 <= _y && _y <= 9999))
                throw new TinySatoArgumentException("Specify 1-9999 dots.");
            Add(string.Format("V{0:D4}", _y));
        }

        public void SetGapSizeBetweenLabels(int y)
        {
            if (!(0 <= y && y <= 64))
                throw new TinySatoArgumentException("Specify 0-64 dots.");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("TG{0:D2}", y));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetSpeed(int speed)
        {
            if (!(1 <= speed && speed <= 5))
                throw new TinySatoArgumentException("Specify 1-5 speed");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("CS{0:D2}", speed));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetStartPosition(int x, int y)
        {
            if (!(Math.Abs(x) <= 999))
                throw new TinySatoArgumentException("Specify -999 <= x <= 999 dots.");
            if (!(Math.Abs(y) <= 999))
                throw new TinySatoArgumentException("Specify -999 <= y <= 999 dots.");
            Add(string.Format("A3V{0:+000;-000}H{1:+000;-000}", y, x));
        }

        public void SetStartPositionEx(int x, int y)
        {
            if (!(Math.Abs(x) <= 9999))
                throw new TinySatoArgumentException("Specify -9999 <= x <= 9999 dots.");
            if (!(Math.Abs(y) <= 9999))
                throw new TinySatoArgumentException("Specify -9999 <= y <= 9999 dots.");
            soft_offset_x = x;
            soft_offset_y = y;
        }

        public void SetPaperSize(int height, int width)
        {
            if (!(1 <= height && height <= 9999))
                throw new TinySatoArgumentException("Specify 1-9999 dots for height.");
            if (!(1 <= width && width <= 9999))
                throw new TinySatoArgumentException("Specify 1-9999 dots for width.");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("A1{0:D4}{1:D4}", height, width));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetCalendar(DateTime dt)
        {
            Add(string.Format("WT{0:D2}{1:D2}{2:D2}{3:D2}{4:D2}",
                dt.Year % 1000, dt.Month, dt.Day, dt.Hour, dt.Minute));
        }

        public void SetPageNumber(uint number_of_pages)
        {
            if (!(1 <= number_of_pages && number_of_pages <= 999999))
                throw new TinySatoArgumentException("Specify 1-999999 pages.");
            Add(string.Format("Q{0:D6}", number_of_pages));
        }
    }
}
