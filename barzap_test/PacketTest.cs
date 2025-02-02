using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using barzap.Models;

namespace barzap_test {

    [TestClass]
    public sealed class PacketTest {


        [TestMethod]
        public void ReadFields() {
            Packet p = new();
            p.Op = "tt";
            p.Data = "a1234b12.34";
            p.DataSize = 10;

            Assert.AreEqual(1234L, p.ReadLong("a"));
            Assert.AreEqual(12.34m, p.ReadDecimal("b"));
        }

        [TestMethod]
        public void Fields() {
            Packet p = new();
            p.Op = "tt";
            p.Data = "a1b2cc3d\"hi\"e4abcdefghijkl5mno\"abc123\"";

            Assert.AreEqual("1", p.GetField("a"));
            Assert.AreEqual("2", p.GetField("b"));
            Assert.AreEqual("3", p.GetField("cc"));
            Assert.AreEqual("hi", p.GetField("d"));
            Assert.AreEqual("4", p.GetField("e"));
            Assert.AreEqual("5", p.GetField("abcdefghijkl"));
            Assert.AreEqual("abc123", p.GetField("mno"));
        }

        [TestMethod]
        public void FieldsDecimal() {
            Packet p = new();
            p.Op = "tt";
            p.Data = "a1.234";

            Assert.AreEqual("1.234", p.GetField("a"));
        }

        [TestMethod]
        public void FieldsQuotedString() {
            Packet p = new();
            p.Op = "tt";
            // abc"hi \"guy\""
            p.Data = "abc\"hi \\\"guy\\\"\"";

            Assert.AreEqual("hi \"guy\"", p.GetField("abc"));
            Assert.IsNull(p.GetField("nothere"));
        }


    }
}
