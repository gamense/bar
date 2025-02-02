using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace barzap.Models {

    public class BarMatch {

        /// <summary>
        ///     a team is not a "side", it's an army, usually controlled by 1 player
        /// </summary>
        public long TeamID { get; set; }

        /// <summary>
        ///     total metal cost of all units in the team
        /// </summary>
        public long TotalMetal { get; set; }

        /// <summary>
        ///     total energy cost of all units in the team
        /// </summary>
        public long TotalEnergy { get; set; }

        public BarEco Metal { get; set; } = new();

        public BarEco Energy { get; set; } = new();

    }


}
