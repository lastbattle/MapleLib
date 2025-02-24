
namespace MapleLib
{
    public class MemoryLimits
    {
        /// <summary>
        /// The stackalloc size for decoding strings, and arrays.
        /// - Avoiding L2 Cache Spillover: Keeping allocations well below the L1 cache size (divided by number of cores) prevents unnecessary cache misses.
        /// - Stack Size: While modern systems typically have large stack sizes, it's still prudent to use stack allocations conservatively to avoid stack overflow risks.
        ///            Allocations in the 32 KB to 64 KB range are large enough to be meaningful for many operations while small enough to minimize the risk of stack overflow or significant performance penalties.
        /// - However, we also need to consider that the stack is used for other purposes too, not just our stackalloc.
        /// a conservative yet effective approach would be to use a stackalloc size that's about 1/4 to 1/2 of the L1 data cache size per core. This leaves room for other stack usage while still benefiting from L1 cache performance.
        ///
        /// AMD Ryzen 9700x, L1 Cache: 80 KB / core
        /// AMD Ryzen 5800x, L1 Cache: 64 KB / core
        /// Intel 13/14th Raptor Lake: 80 KB per P-core (32 KB instructions + 48 KB data), 96 KB per E-core(64 KB instructions + 32 KB data)
        /// </summary>
        public const int STACKALLOC_SIZE_LIMIT_L1 = 10 * 1024;  // optimal size is half of CPU's L1 cache.
    }
}
