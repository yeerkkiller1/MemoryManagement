using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemoryManagement
{
    public static class GCExts
    {
        public static void ForceFullGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
            GC.Collect();
        }

        public static bool IsAlive<T>(this WeakReference<T> reference)
            where T : class
        {
            T val;
            return reference.TryGetTarget(out val);
        }
    }
}
