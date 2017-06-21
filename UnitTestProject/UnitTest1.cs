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

        const double inch2mm = 25.4;
        const double dpi = 203;
        const double dot2mm = inch2mm / dpi; // 25.4(mm) / 203(dot) -> 0.125(mm/dot)
        const double mm2dot = 1 / dot2mm; // 8(dot/mm)

        protected int getJobCount()
        {
            var server = new System.Printing.LocalPrintServer();
            var queue = server.GetPrintQueue(printer_name);
            return queue.NumberOfJobs;
        }

        protected int getLastJobPageCount()
        {
            var server = new System.Printing.LocalPrintServer();
            var queue = server.GetPrintQueue(printer_name);
            var last = queue.GetPrintJobInfoCollection().OrderBy(job => job.TimeJobSubmitted).Last();
            return last.NumberOfPages;
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
            a.Dispose();
            b.Close();
            b.Close();
            Assert.AreEqual(before + 2, getJobCount());
        }

        [TestMethod]
        public void JAN13()
        {
            var before = getJobCount();
            var barcode = "1234567890128";

            var sato = new Printer(printer_name);
            sato.SetSensorType(SensorType.Transparent);
            sato.SetPaperSize((int)(80 * mm2dot), (int)(104 * mm2dot));

            sato.MoveToX(80);
            sato.MoveToY(80);
            sato.Barcode.AddJAN13(3, 70, barcode);

            sato.SetPageNumber(1);
            sato.Send();
            sato.Dispose();

            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }

        [TestMethod]
        public void GapLabelPrinting()
        {
            var before = getJobCount();
            var barcode = "A-12-345";
            int width = (int)(104 * mm2dot), height = (int)(80 * mm2dot);

            var sato = new Printer(printer_name);
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
                sato.Graphic.AddBitmap(bitmap);
            }
            sato.Send();
            sato.Dispose();

            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }

        [TestMethod]
        public void EyeMarkPrinting()
        {
            var before = getJobCount();
            var barcode = "ABCDEF1234567890";
            var paper_width = 50.0 * mm2dot;
            var paper_height = 20.0 * mm2dot;

            var sato = new Printer(printer_name);
            sato.SetSensorType(SensorType.Reflection);
            sato.SetGapSizeBetweenLabels(
                (int)Math.Round(2.0 * mm2dot));
            sato.SetPaperSize(
                (int)Math.Round(paper_height),
                (int)Math.Round(paper_width));

            // reset start position
            sato.SetStartPosition(0, 0);
            sato.MoveToX(48);
            sato.MoveToY(24);
            sato.Barcode.AddCODE128(1, 48, barcode);

            // calibrate start position
            sato.SetStartPosition(24, 88);
            sato.Barcode.AddCODE128(1, 48, barcode, (Size s) =>
            {
                var x = (paper_width - s.Width) / 2.0;
                sato.MoveToX((int)Math.Round(x));
                sato.MoveToY(1);
            });

            // reset start position
            sato.SetStartPosition(0, 0);
            sato.Send(1);
            sato.Dispose();

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

        [TestMethod]
        public void GraphicsOpecode()
        {
            var before = getJobCount();
            using (var sato = new Printer(printer_name, true))
            {
                sato.SetSensorType(SensorType.Reflection);

                int height = (int)Math.Round(50.0 * mm2dot),
                    width = (int)Math.Round(88.0 * mm2dot),
                    sub_width = (int)Math.Round(85.0 * mm2dot),
                    sub_height = (int)Math.Round(50.0 * mm2dot);
                bool draw_rectangle_by_sbpl = true;
                sato.SetGapSizeBetweenLabels((int)Math.Round(2.0 * mm2dot));
                sato.SetPaperSize(height, width);
                sato.SetStartPosition((int)Math.Round(1.0 * mm2dot), 0);

                sato.MoveToX((int)Math.Round(5.0 * mm2dot));
                sato.MoveToY((int)Math.Round(5.0 * mm2dot));
                sato.Barcode.AddCODE128(1, (int)Math.Round(5.0 * mm2dot), "TEST");

                if (draw_rectangle_by_sbpl)
                {
                    sato.MoveToX((int)Math.Round(1.3 * mm2dot));
                    sato.MoveToY(1);
                    sato.Graphic.AddBox(
                        (int)Math.Round(0.5 * mm2dot),
                        (int)Math.Round(0.5 * mm2dot),
                        sub_width,
                        sub_height);
                }

                using (var font = new Font("Consolas", 30))
                using (var bitmap = new Bitmap(sub_width, sub_height))
                using (var g = Graphics.FromImage(bitmap))
                using (var sf = new StringFormat())
                {
                    var box = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    g.FillRectangle(Brushes.White, box);
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("ABCDEFGHIJKLMNOPQRSTUVWXYZ", font, Brushes.Black, box, sf);

                    if (!draw_rectangle_by_sbpl)
                    {
                        g.DrawRectangle(new Pen(Color.Black, 8),
                            new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    }

                    sato.MoveToX((int)Math.Round(1.3 * mm2dot));
                    sato.MoveToY(1);
                    sato.Graphic.AddGraphic(bitmap);
                }
                sato.SetPageNumber(3);
                sato.AddStream();

                sato.MoveToX((int)Math.Round(10.0 * mm2dot));
                sato.MoveToY((int)Math.Round(10.0 * mm2dot));
                sato.Barcode.AddCODE128(2, (int)Math.Round(10.0 * mm2dot), "TEST");
                sato.SetPageNumber(2);
                sato.Send();

                // empty page
                sato.Send(1);
            }
            var after = getJobCount();
            Assert.AreEqual(before + 1, after, "Job count");
            Assert.AreEqual(3, getLastJobPageCount(), "Variation of page");
        }
    }
}
