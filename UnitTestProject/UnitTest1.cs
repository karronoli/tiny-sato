using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinySato;
using System.Drawing;

namespace UnitTestProject
{
    [TestClass]
    public class UnitTest1
    {
        protected string printer_name = "T408v";
        protected const bool DEBUG = false;

        const double inch2mm = 25.4;
        const double dpi = 203;
        const double dot2mm = inch2mm / dpi; // 25.4(mm) / 203(dot) -> 0.125(mm/dot)
        const double mm2dot = 1 / dot2mm; // 8(dot/mm)

        [TestInitialize]
        public void SetUp()
        {
        }

        [TestCleanup]
        public void TearDown()
        {
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
                + 11 * line_width_mm // check
                + 11 * barcode.Length * line_width_mm // data
                + 13 * line_width_mm // stop
                + (no_quiet_zone ? 0 : 2 * quiet_zone);
        }

        [TestMethod]
        public void GapLabelPrinting()
        {
            var before = getJobCount();
            var sato = new Printer(printer_name);
            sato.SetDensity(3, DensitySpec.A);
            sato.SetSpeed(4);
            sato.SetSensorType(SensorType.Transparent);
            // 408 印字有効エリア	最大　長さ400mm×幅104mm → 3200 x 832 dot(80dot = 1cm)
            sato.SetPaperSize(944, 640);
            sato.SetPageNumber(1);
            sato.MoveToX(100);
            sato.MoveToY(100);
            sato.AddBarCode128(1, 30, "HELLO");
            using (var bitmap = new Bitmap(944, 640))
            {
                using (var font = new Font("VL Gothic", 50))
                using (var g = Graphics.FromImage(bitmap))
                {
                    // Draw by black on white background.
                    var overall = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    g.FillRectangle(Brushes.White, overall);
                    g.DrawPie(Pens.Black, 60, 10, 80, 80, 30, 300);
                    var textbox = new Rectangle(10, 10, bitmap.Width, bitmap.Height);
                    var text = "SBPL😀From TinySato！";
                    using (var sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(text, font, Brushes.Black, textbox, sf);
                    }
                }
                sato.AddBitmap(bitmap);
                if (DEBUG) bitmap.Save("tinysato-gap.png");
            }
            sato.Send();
            sato.Close();
            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }

        [TestMethod]
        public void EyeMarkPrinting()
        {
            var before = getJobCount();
            var paper_gap_mm = 2.0;
            var page_number = 1;
            var sato = new Printer(printer_name);
            sato.SetPaperSize(500, 200);
            sato.AddBarCode128(1, 30, "HELLO");
            sato.SetDensity(3, DensitySpec.A);
            sato.SetSpeed(4);
            sato.SetGapSizeBetweenLabels((int)(paper_gap_mm * mm2dot));
            sato.SetSensorType(SensorType.Reflection);
            sato.SetPageNumber((uint)page_number);
            sato.Send();
            sato.Dispose();
            var after = getJobCount();
            Assert.AreEqual(before + page_number, after);
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
                sato.AddBarCode128(1, 30, "HELLO");
            }
            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }
    }
}
