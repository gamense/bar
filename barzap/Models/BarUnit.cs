using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace barzap.Models {

    public class BarUnit {

        public long ID { get; set; }

        public long DefID { get; set; }

        public long TeamID { get; set; }

        public long MetalCost { get; set; }

        public long EnergyCost { get; set; }

        public static BarUnit Parse(Packet packet) {
            if (packet.Op != "mk" && packet.Op != "rm") {
                throw new Exception($"expected op code of mk or rm, got {packet.Op}");
            }

            BarUnit unit = new();

            unit.ID = packet.ReadLong("i");
            unit.DefID = packet.ReadLong("u");
            unit.TeamID = packet.ReadLong("t");
            unit.MetalCost = packet.ReadLong("m");
            unit.EnergyCost = packet.ReadLong("e");

            return unit;
        }

    }
}
