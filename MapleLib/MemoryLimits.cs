
using System;
using System.IO;

namespace MapleLib
{
    public class MemoryLimits
    {
        /// <summary>
        /// Upper bound for attacker-controlled encoded WZ strings and the
        /// mutable encryption key material needed to decode them.
        /// </summary>
        public const int MAX_WZ_STRING_BYTES = 64 * 1024 * 1024;

        /// <summary>
        /// Null-terminated strings are metadata, not bulk payloads. Keeping a
        /// separate lower ceiling prevents a missing terminator in a large
        /// stream from consuming memory until the process is exhausted.
        /// </summary>
        public const int MAX_NULL_TERMINATED_STRING_BYTES = 1024 * 1024;

        /// <summary>
        /// WZ headers contain a short identifier and copyright string. This is
        /// intentionally generous while still bounding malformed FStart values.
        /// </summary>
        public const int MAX_WZ_HEADER_BYTES = 1024 * 1024;

        /// <summary>
        /// Upper bound for a single eagerly materialized media/raw payload.
        /// Larger WZ files remain supported; only one property allocation is
        /// constrained here.
        /// </summary>
        public const int MAX_WZ_PAYLOAD_BYTES = 256 * 1024 * 1024;

        /// <summary>
        /// Maximum packet body that the asynchronous Maple session parser will
        /// materialize from an untrusted length header.
        /// </summary>
        public const int MAX_PACKET_BYTES = 16 * 1024 * 1024;

        /// <summary>
        /// Upper bound for JSON manifests, indexes, and case maps read from an
        /// IMG filesystem before deserialization.
        /// </summary>
        public const long MAX_METADATA_JSON_BYTES = 16L * 1024 * 1024;

        public static void EnsureFileSize(string path, long maximumBytes, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (maximumBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));

            long length = new FileInfo(path).Length;
            if (length > maximumBytes)
                throw new InvalidDataException($"{description} exceeds the supported size limit.");
        }

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
