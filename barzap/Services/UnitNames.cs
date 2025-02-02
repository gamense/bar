using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace barzap.Services {

    public class UnitNames {

        private readonly Dictionary<long, string> _UnitNames = new();

        public UnitNames() {

        }

        public string? GetUnitName(long unitDefID) {
            return _UnitNames.GetValueOrDefault(unitDefID);
        }

        public void SetUnitName(long unitDefID, string name) {
            _UnitNames[unitDefID] = name;
        }

    }
}
