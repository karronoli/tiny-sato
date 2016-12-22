using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinySato;
using System.Drawing;
using System.Linq;

namespace UnitTestProject
{
    [TestClass]
    public class UnitTest1
    {
        protected string printer_name = "T408v";
        protected Printer sato;

        const double inch2mm = 25.4;
        const double dpi = 203;
        const double dot2mm = inch2mm / dpi; // 25.4(mm) / 203(dot) -> 0.125(mm/dot)
        const double mm2dot = 1 / dot2mm; // 8(dot/mm)

        [TestInitialize]
        public void SetUp()
        {
            sato = new Printer(printer_name);
            sato.SetDensity(3, DensitySpec.A);
            sato.SetSpeed(4);
        }

        [TestCleanup]
        public void TearDown()
        {
            sato.Dispose();
        }

        protected int getJobCount()
        {
            var server = new System.Printing.LocalPrintServer();
            var queue = server.GetPrintQueue(printer_name);
            return queue.NumberOfJobs;
        }

        protected double getBarcode128mm(string barcode, double line_width, bool no_quiet_zone = true)
        {
            var line_width_mm = line_width * dot2mm;
            var quiet_zone = Math.Max(0.54, line_width_mm * 10);
            // 	refer to JIS X 0504:2003
            return 11 * line_width_mm // start
                + 11 * barcode.Length * line_width_mm // data
                + 11 * line_width_mm // check
                + 13 * line_width_mm // stop
                + (no_quiet_zone ? 0 : 2 * quiet_zone);
        }

        [TestMethod]
        public void MultiJob()
        {
            var before = getJobCount();
            var a = new Printer(printer_name, true);
            var b = new Printer(printer_name);
            // random operations
            b.Send(); // send <A><Z>, job +1
            a.SetCalendar(DateTime.Now); // To add job, need a operation at least. 
            a.Dispose();
            b.Close();
            Assert.AreEqual(before + 2, getJobCount());
        }

        [TestMethod]
        public void JAN13()
        {
            var before = getJobCount();
            var barcode = "1234567890128";

            sato.SetSensorType(SensorType.Transparent);
            sato.SetPaperSize((int)(80 * mm2dot), (int)(104 * mm2dot));

            sato.MoveToX(80);
            sato.MoveToY(80);
            sato.Barcode.AddJAN13(3, 70, barcode);

            sato.SetPageNumber(1);
            sato.Send();

            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }

        [TestMethod]
        public void GapLabelPrinting()
        {
            var before = getJobCount();
            var barcode = "A-12-345";
            int width = (int)(104 * mm2dot), height = (int)(80 * mm2dot);

            sato.SetSensorType(SensorType.Transparent);
            sato.SetPaperSize(height, width);
            sato.SetPageNumber(1);

            sato.MoveToX(480);
            sato.MoveToY(400);
            sato.Barcode.AddCODE128(1, 100, barcode);

            using (var font = new Font("Consolas", 90))
            using (var bitmap = new Bitmap(width, font.Height))
            using (var g = Graphics.FromImage(bitmap))
            using (var sf = new StringFormat())
            {
                // Draw by black on white background.
                var box = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                g.FillRectangle(Brushes.White, box);

                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(barcode, font, Brushes.Black, box, sf);

                sato.MoveToX(1);
                sato.MoveToY(1);
                sato.AddBitmap(bitmap);
            }
            sato.Send();

            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }

        [TestMethod]
        public void EyeMarkPrinting()
        {
            var before = getJobCount();
            var barcode = "1234567890ABCDEF";
            var paper_width_mm = 50.0;
            var paper_height_mm = 20.0;
            var paper_gap_mm = 2.0;
            var paper_offset_x_mm = 1.0;
            var paper_offset_y_mm = 0.8;

            sato.SetSensorType(SensorType.Reflection);
            sato.SetGapSizeBetweenLabels(
                (int)Math.Round(paper_gap_mm * mm2dot));
            sato.SetPaperSize(
                (int)Math.Round(paper_height_mm * mm2dot),
                (int)Math.Round(paper_width_mm * mm2dot));
            sato.SetStartPosition(
                (int)Math.Round(paper_offset_x_mm * mm2dot),
                (int)Math.Round(paper_offset_y_mm * mm2dot));

            sato.MoveToX(80);
            sato.MoveToY(80);
            sato.Barcode.AddCODE128(1, 50, barcode);

            sato.Send(1);

            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }

        [TestMethod]
        public void Modulus16()
        {
            var base_number = 16;
            var barcode = "A1234A";
            int check_digit_index = base_number -
                barcode.Select(
                  symbol => Barcode.CodabarSymbols.IndexOf(symbol)).
                    Sum() % base_number;
            Assert.AreEqual('6', Barcode.CodabarSymbols[check_digit_index]);
        }

        [TestMethod]
        public void IgnoreSensorPrinting()
        {
            var before = getJobCount();
            using (var sato = new Printer(printer_name, true))
            {
                sato.SetSensorType(SensorType.Ignore);
                sato.SetPaperSize(944, 101);
                sato.SetPageNumber(1);
                sato.MoveToX(1);
                sato.MoveToY(1);
                sato.Barcode.AddCODE128(1, 30, "HELLO");
            }
            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }
    }
}
