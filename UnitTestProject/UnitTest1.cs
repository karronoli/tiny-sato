using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinySato;

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
            sato.SetPaperSize(944, 101);
            sato.SetPageNumber(1);
            sato.MoveToX(0);
            sato.MoveToY(0);
            sato.AddBarCode128(1, 30, "HELLO");
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
