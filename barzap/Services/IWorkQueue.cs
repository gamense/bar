using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace barzap.Services {

    public interface IWorkQueue {

        List<long> GetProcessTime();

        int Count();

        long Processed();

    }

}
