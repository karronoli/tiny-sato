using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject
{
    [TestClass]
    public class UnitTest1
    {
        protected TinySato.TinySato sato;
        [TestInitialize]
        public void SetUp()
        {
            this.sato = new TinySato.TinySato("T408v");
        }

        [TestCleanup]
        public void TearDown()
        {
            this.sato.Close();
        }

        [TestMethod]
        public void TestMethod1()
        {
            sato.SetPaperSize(424, 400);

            // バーコードの編集
            // 横位置(100ﾄﾞｯﾄ),縦位置(284ﾄﾞｯﾄ);
            sato.MoveToY(284);
            sato.MoveToX(100);
            // バーコード種類(CODE39),バー幅拡大率(2倍),バー天地寸法(80ﾄﾞｯﾄ)
            sato.AddBarCodeRatio12(TinySato.Barcodes.CODE39, 2, 80, "SATO123");

            // 枚数を設定(1枚)します
            sato.Add("Q1");
            sato.Send();
        }
    }
}
