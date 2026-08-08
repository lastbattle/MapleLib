using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MapleLib.PacketLib
{
	public abstract class AbstractPacket
	{
		protected Stream _buffer;

		public byte[] ToArray()
		{
			if (_buffer is MemoryStream memoryStream)
				return memoryStream.ToArray();

			if (!_buffer.CanSeek || !_buffer.CanRead)
				throw new InvalidOperationException("The packet stream must be seekable and readable to materialize its bytes.");

			long oldPosition = _buffer.Position;
		try
		{
			_buffer.Position = 0;
			using var copy = new MemoryStream();
			_buffer.CopyTo(copy);
			return copy.ToArray();
		}
		finally
		{
			_buffer.Position = oldPosition;
		}
		}
	}
}
