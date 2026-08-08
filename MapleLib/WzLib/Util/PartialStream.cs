using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Util
{
    /// <summary>
    /// https://raw.githubusercontent.com/Elem8100/MapleNecrocer/refs/heads/master/WzComparerR2.WzLib/Utilities/PartialStream.cs
    /// </summary>
    public class PartialStream : Stream
    {
        public PartialStream(Stream baseStream, long offset, long length, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(baseStream);
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");
            }
            if (length > long.MaxValue - offset)
                throw new ArgumentOutOfRangeException(nameof(length), "Offset plus length exceeds the stream address range.");
            this.baseStream = baseStream;
            this.offset = offset;
            this.length = length;
            this.end = offset + length;
            this.leaveOpen = leaveOpen;
        }

        private Stream baseStream;
        private long offset;
        private long length;
        private long end;
        private bool leaveOpen;
        private bool disposed;

        public Stream BaseStream
        {
            get { return baseStream; }
        }

        public override bool CanRead
        {
            get { return !disposed && baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return !disposed && baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return !disposed && baseStream.CanWrite; }
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            baseStream.Flush();
        }

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return this.length;
            }
        }

        public virtual long Offset
        {
            get { return this.offset; }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                long absolute = baseStream.Position;
                if (absolute < offset || absolute > end)
                    throw new IOException("Base stream position is outside the partial stream range.");
                return absolute - offset;
            }
            set
            {
                ThrowIfDisposed();
                ValidateLogicalPosition(value);
                baseStream.Position = checked(value + this.offset);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            ValidateBufferArgs(buffer, offset, count);
            long curPos = this.Position;
            int boundedCount = (int)Math.Min((long)count, this.length - curPos);
            return boundedCount == 0 ? 0 : baseStream.Read(buffer, offset, boundedCount);
        }

        public override int Read(Span<byte> buffer)
        {
            ThrowIfDisposed();
            long curPos = this.Position;
            int boundedCount = (int)Math.Min((long)buffer.Length, this.length - curPos);
            return boundedCount == 0 ? 0 : baseStream.Read(buffer[..boundedCount]);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            long curPos = this.Position;
            int boundedCount = (int)Math.Min((long)buffer.Length, this.length - curPos);
            return boundedCount == 0
                ? ValueTask.FromResult(0)
                : baseStream.ReadAsync(buffer[..boundedCount], cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateBufferArgs(buffer, offset, count);
            long curPos = this.Position;
            int boundedCount = (int)Math.Min((long)count, this.length - curPos);
            return boundedCount == 0
                ? Task.FromResult(0)
                : baseStream.ReadAsync(buffer, offset, boundedCount, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            long logicalPosition;
            try
            {
                logicalPosition = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => checked(this.Position + offset),
                    SeekOrigin.End => checked(this.length + offset),
                    _ => throw new ArgumentOutOfRangeException(nameof(origin))
                };
            }
            catch (OverflowException ex)
            {
                throw new IOException("Attempt to seek outside the stream range.", ex);
            }

            if (logicalPosition < 0 || logicalPosition > this.length)
                throw new IOException("Attempt to seek outside the partial stream range.");
            long absolutePosition = checked(this.offset + logicalPosition);
            return baseStream.Seek(absolutePosition, SeekOrigin.Begin) - this.offset;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Cannot set the length of PartialStream.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            ValidateBufferArgs(buffer, offset, count);
            EnsureWithinRange(count);
            baseStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            EnsureWithinRange(buffer.Length);
            baseStream.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureWithinRange(buffer.Length);
            return baseStream.WriteAsync(buffer, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateBufferArgs(buffer, offset, count);
            EnsureWithinRange(count);
            return baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Close()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (disposed)
                    return;
                disposed = true;
                if (!this.leaveOpen)
                {
                    baseStream.Dispose();
                }
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            ValidateBufferArgs(buffer, offset, count);
            long curPos = this.Position;
            int boundedCount = (int)Math.Min((long)count, this.length - curPos);
            return baseStream.BeginRead(buffer, offset, boundedCount, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            ValidateBufferArgs(buffer, offset, count);
            EnsureWithinRange(count);
            return baseStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            return baseStream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            baseStream.EndWrite(asyncResult);
        }

        public override int ReadByte()
        {
            ThrowIfDisposed();
            long curPos = this.Position;
            if (curPos < this.length)
                return baseStream.ReadByte();
            else
                return -1;
        }

        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            EnsureWithinRange(1);
            baseStream.WriteByte(value);
        }

        public override bool CanTimeout
        {
            get
            {
                return !disposed && baseStream.CanTimeout;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                ThrowIfDisposed();
                return baseStream.ReadTimeout;
            }
            set
            {
                ThrowIfDisposed();
                baseStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                ThrowIfDisposed();
                return baseStream.WriteTimeout;
            }
            set
            {
                ThrowIfDisposed();
                baseStream.WriteTimeout = value;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PartialStream));
        }

        private void ValidateLogicalPosition(long value)
        {
            if (value < 0 || value > length)
                throw new ArgumentOutOfRangeException(nameof(value), "Position is outside the partial stream range.");
        }

        private void EnsureWithinRange(int count)
        {
            long current = Position;
            if (count < 0 || (long)count > length - current)
                throw new IOException("Cannot access outside the partial stream range.");
        }

        private static void ValidateBufferArgs(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
                throw new ArgumentOutOfRangeException();
        }
    }
}
