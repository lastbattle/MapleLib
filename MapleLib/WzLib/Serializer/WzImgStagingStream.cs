using System;
using System.Buffers;
using System.IO;

namespace MapleLib.WzLib.Serializer
{
    // Bounded seekable staging for one owned IMG export. Larger output switches
    // once to the real file; property serialization is never replayed.
    internal sealed class WzImgStagingStream : Stream
    {
        private const int BufferSize = 1024 * 1024;
        private readonly FileStream destination;
        private Stream active;
        private MemoryStream memory;
        private byte[] buffer;
        private bool disposed;
        private bool materializationFailed;

        internal WzImgStagingStream(FileStream destination)
        {
            this.destination = destination;
            buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                memory = new MemoryStream(buffer, writable: true);
                memory.SetLength(0);
                active = memory;
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }
        }

        public override bool CanRead => !disposed && active.CanRead;
        public override bool CanSeek => !disposed && active.CanSeek;
        public override bool CanWrite => !disposed && active.CanWrite;
        public override long Length
        {
            get { ThrowIfDisposed(); return active.Length; }
        }
        public override long Position
        {
            get { ThrowIfDisposed(); return active.Position; }
            set
            {
                ThrowIfDisposed();
                if (memory != null && value > int.MaxValue)
                    Complete();
                active.Position = value;
            }
        }

        internal void Complete()
        {
            ThrowIfDisposed();
            if (memory == null)
                return;
            long position = memory.Position;
            try
            {
                destination.Position = 0;
                destination.Write(buffer.AsSpan(0, checked((int)memory.Length)));
                destination.Position = position;
            }
            catch
            {
                materializationFailed = true;
                throw;
            }
            active = destination;
            memory.Dispose();
            memory = null;
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = null;
        }

        public override void Write(ReadOnlySpan<byte> source)
        {
            ThrowIfDisposed();
            if (memory != null && source.Length > buffer.Length - memory.Position)
                Complete();
            active.Write(source);
        }
        public override void Write(byte[] source, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(source);
            Write(source.AsSpan(offset, count));
        }
        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            if (memory != null && memory.Position >= buffer.Length)
                Complete();
            active.WriteByte(value);
        }
        public override int Read(byte[] target, int offset, int count)
        {
            ThrowIfDisposed();
            return active.Read(target, offset, count);
        }
        public override int Read(Span<byte> target)
        {
            ThrowIfDisposed();
            return active.Read(target);
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            if (memory != null)
            {
                long start = origin switch
                {
                    SeekOrigin.Begin => 0,
                    SeekOrigin.Current => memory.Position,
                    SeekOrigin.End => memory.Length,
                    _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
                };
                if (offset > int.MaxValue - start)
                    Complete();
            }
            return active.Seek(offset, origin);
        }
        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            if (memory != null && value > buffer.Length)
                Complete();
            active.SetLength(value);
        }
        public override void Flush()
        {
            Complete();
            destination.Flush();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                try
                {
                    if (!materializationFailed)
                        Complete();
                }
                finally
                {
                    disposed = true;
                    memory?.Dispose();
                    memory = null;
                    if (buffer != null)
                        ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                }
            }
            base.Dispose(disposing);
        }
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    }
}
