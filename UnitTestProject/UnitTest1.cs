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

        [TestMethod]
        public void GapLabelPrinting()
        {
            var before = getJobCount();
            var sato = new Printer(printer_name);
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
                sato.MoveToX(0);
                sato.MoveToY(0);
                sato.AddBitmap(bitmap);
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
            var sato = new Printer(printer_name);
            sato.SetPaperSize(500, 200);
            sato.SetSensorType(SensorType.Transparent);
            sato.SetPageNumber(2);
            sato.MoveToX(0);
            sato.MoveToY(0);
            sato.Add("~A1");
            sato.AddBarCode128(1, 30, "HELLO");
            sato.Send();
            sato.Dispose();
            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
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
                sato.MoveToX(0);
                sato.MoveToY(0);
                sato.AddBarCode128(1, 30, "HELLO");
            }
            var after = getJobCount();
            Assert.AreEqual(before + 1, after);
        }
    }
}
