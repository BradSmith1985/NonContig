using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace NonContig {

	/// <summary>
	/// Provides a <see cref="Stream"/> implementation that uses a 
	/// non-contiguous collection of bytes as its backing store.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is essentially just a wrapper around <see cref="NcByteCollection"/> 
	/// which exposes stream functionality. If initialised using an existing 
	/// instance, its overheads are negligible.
	/// </para>
	/// <para>
	/// Although this class implements <see cref="IDisposable"/>, it does not 
	/// use any unmanaged resources and does not require explicit closure.
	/// </para>
	/// </remarks>
	public class NcByteStream : Stream {

		NcByteCollection _data;
		long _position;
		readonly object syncLock = new object();

		/// <summary>
		/// This property is always true.
		/// </summary>
		public override bool CanRead => true;
		/// <summary>
		/// This property is always true.
		/// </summary>
		public override bool CanSeek => true;
		/// <summary>
		/// This property is always true.
		/// </summary>
		public override bool CanWrite => true;
		/// <summary>
		/// Gets the length of the stream.
		/// </summary>
		public override long Length {
			get {
				lock (syncLock) {
					return _data.LongCount;
				}
			}
		}
		/// <summary>
		/// Gets the current position in the stream.
		/// </summary>
		public override long Position {
			get {
				lock (syncLock) {
					return _position;
				}
			}
			set {
				lock (syncLock) {
					_position = value;
				}
			}
		}
		/// <summary>
		/// Gets the <see cref="NcByteCollection"/> that is used as a backing 
		/// store for the stream.
		/// </summary>
		public NcByteCollection Data => _data;

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteStream"/> class 
		/// that represents an empty, writable stream.
		/// </summary>
		public NcByteStream() {
			_data = new NcByteCollection();
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteStream"/> class 
		/// using the specified block size.
		/// </summary>
		public NcByteStream(int blockSize) {
			_data = new NcByteCollection(blockSize);
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteStream"/> class 
		/// using the specified <see cref="NcByteCollection"/> as a backing 
		/// store.
		/// </summary>
		/// <param name="data"></param>
		/// <remarks>
		/// An instance initialised with this constructor will modify the 
		/// original data. If this is not intended, use <see cref="NcByteCollection.Clone"/> 
		/// to make a copy of the data first.
		/// </remarks>
		public NcByteStream(NcByteCollection data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			_data = data;
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteStream"/> class 
		/// initially populated with the specified array of bytes.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="blockSize"></param>
		/// <remarks>
		/// An instance initialised with this constructor will not modify the 
		/// original data.
		/// </remarks>
		public NcByteStream(byte[] data, int blockSize = NcByteCollection.DEFAULT_BLOCK_SIZE) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			_data = new NcByteCollection(data, blockSize);
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteStream"/> class 
		/// initially populated with the specified sequence of bytes.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="blockSize"></param>
		/// <remarks>
		/// An instance initialised with this constructor will not modify the 
		/// original data.
		/// </remarks>
		public NcByteStream(IEnumerable<byte> data, int blockSize = NcByteCollection.DEFAULT_BLOCK_SIZE) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			_data = new NcByteCollection(data, blockSize);
		}

		/// <summary>
		/// This method has no effect.
		/// </summary>
		public override void Flush() {
			// nothing required here
		}

		/// <summary>
		/// Reads data from the stream into the specified buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public override int Read(byte[] buffer, int offset, int count) {
			int bytesRead = _data.Copy(Position, buffer, offset, count);
			Position += bytesRead;
			return bytesRead;
		}

		/// <summary>
		/// Changes the position of the stream.
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="origin"></param>
		/// <returns></returns>
		public override long Seek(long offset, SeekOrigin origin) {
			switch (origin) {
				case SeekOrigin.Current:
					Position += offset;
					break;
				case SeekOrigin.End:
					Position = (_data.LongCount - 1) - offset;
					break;
				default:
					Position = offset;
					break;
			}

			return Position;
		}

		/// <summary>
		/// Changes the length of the stream.
		/// </summary>
		/// <param name="value"></param>
		/// <remarks>
		/// If <paramref name="value"/> is less than the current length of the 
		/// stream, the data is truncated; otherwise, additional (undefined) 
		/// elements are added to the end of the collection.
		/// </remarks>
		public override void SetLength(long value) {
			lock (syncLock) {
				long diff = _data.LongCount - value;

				if (diff > 0) {
					// truncate end
					_data.RemoveRange(value, diff);
				}
				else if (diff < 0) {
					// pad end
					_data.Grow(-diff);
				}
			}
		}

		/// <summary>
		/// Writes data to the stream from the specified buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <remarks>
		/// If <see cref="Position"/> is beyond the end of the stream when writing 
		/// commences, the collection grows automatically. Any data between the old 
		/// and new ends of the collection is undefined.
		/// </remarks>
		public override void Write(byte[] buffer, int offset, int count) {
			long diff = (Position + count) - _data.Count;
			if (diff > 0) _data.Grow(diff);
			_data.Copy(buffer, offset, Position, count);
			Position += count;
		}

		/// <summary>
		/// Returns an array containing the stream data, regardless of the 
		/// <see cref="Position"/> property.
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// This method is provided for compatibility with <see cref="MemoryStream"/> only. 
		/// Use the <see cref="Data"/> property for a non-contiguous representation of the data.
		/// </remarks>
		public byte[] ToArray() {
			byte[] dest = new byte[_data.Count];
			_data.Copy(0, dest, 0, dest.Length);
			return dest;
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one
		/// byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns></returns>
		public override int ReadByte() {
			if (Position < _data.LongCount)
				return _data[Position++];
			else
				return -1;
		}

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position
		/// within the stream by one byte.
		/// </summary>
		/// <param name="value"></param>
		public override void WriteByte(byte value) {
			lock (syncLock) {
				long diff = (Position + 1) - _data.LongCount;

				if (diff > 0) {
					// pad end
					_data.Grow(diff);
				}
			}

			_data[Position++] = value;
		}
	}
}
