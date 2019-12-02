using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace NcTest {

	public class NcByteCollection : IList<byte>, ICloneable {

		const int BLOCK_SIZE = 4096;

		NcByteBlock _first;
		NcByteBlock _last;

		private class NcByteBlock {

			public byte[] Buffer { get; set; }
			public int UsedCount { get; set; }
			public NcByteBlock Prev { get; set; }
			public NcByteBlock Next { get; set; }

			public NcByteBlock(int capacity = BLOCK_SIZE) {
				Buffer = new byte[capacity];
			}
		}

		public byte this[long index] {
			get {
				int offset;
				NcByteBlock block = BlockAt(index, out offset);

				if (block != null) {
					return block.Buffer[offset];
				}

				throw new IndexOutOfRangeException();
			}
			set {
				int offset;
				NcByteBlock block = BlockAt(index, out offset);

				if (block != null) {
					block.Buffer[offset] = value;
					return;
				}

				throw new IndexOutOfRangeException();
			}
		}

		public byte this[int index] {
			get {
				return this[(long)index];
			}
			set {
				this[(long)index] = value;
			}
		}

		public long LongCount {
			get {
				long count = 0;

				NcByteBlock current = _first;
				while (current != null) {
					count += current.UsedCount;
					current = current.Next;
				}

				return count;
			}
		}

		public int Count {
			get {
				return (int)LongCount;
			}
		}

		bool ICollection<byte>.IsReadOnly => false;

		public NcByteCollection() { }

		public NcByteCollection(NcByteCollection data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			
		}

		public NcByteCollection(byte[] data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			Add(data);			
		}

		public NcByteCollection(IEnumerable<byte> data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			Add(data);
		}

		private NcByteBlock BlockAt(long index, out int offset) {
			long count = 0;

			NcByteBlock current = _first;
			while (current != null) {
				if ((count + current.UsedCount) > index) {
					offset = (int)(index - count);
					return current;
				}

				count += current.UsedCount;
				current = current.Next;
			}

			offset = 0;
			return null;
		}

		public void Add(byte item) {
			if (_last == null) {
				// first block
				_first = _last = new NcByteBlock();
			}
			else if (_last.UsedCount >= _last.Buffer.Length) {
				// new block needed, becomes the new last node
				NcByteBlock next = new NcByteBlock();
				_last.Next = next;
				next.Prev = _last;
				_last = next;
			}

			_last.Buffer[_last.UsedCount] = item;
			_last.UsedCount++;
		}

		public void Add(IEnumerable<byte> data) {
			foreach (byte b in data) {
				Add(b);
			}
		}

		public void Add(byte[] data) {
			int i = 0;
			while (i < data.Length) {
				if (_last == null) {
					// first block (allow smaller block size)
					_first = _last = new NcByteBlock(Math.Min(data.Length, BLOCK_SIZE));
				}
				else if (_last.UsedCount >= _last.Buffer.Length) {
					// new block needed, becomes the new last node
					NcByteBlock next = new NcByteBlock();
					_last.Next = next;
					next.Prev = _last;
					_last = next;
				}

				int count = Math.Min(_last.Buffer.Length - _last.UsedCount, data.Length - i);
				Buffer.BlockCopy(data, i, _last.Buffer, _last.UsedCount, count);
				_last.UsedCount += count;
				i += count;
			}
		}

		public void Grow(long count) {
			long i = 0;
			while (i < count) {
				if (_last == null) {
					// first block (allow smaller block size)
					_first = _last = new NcByteBlock((int)Math.Min(count, BLOCK_SIZE));
				}
				else if (_last.UsedCount >= _last.Buffer.Length) {
					// new block needed, becomes the new last node
					NcByteBlock next = new NcByteBlock();
					_last.Next = next;
					next.Prev = _last;
					_last = next;
				}

				int size = (int)Math.Min(_last.Buffer.Length - _last.UsedCount, count - i);
				_last.UsedCount += size;
				i += size;
			}
		}

		public void Clear() {
			// removing all references to the nodes should make them eligible for GC
			_first = null;
			_last = null;
		}

		bool ICollection<byte>.Contains(byte item) {
			// simple linear search
			foreach (byte b in this) {
				if (b == item) return true;
			}

			return false;
		}

		public void CopyTo(byte[] array, int arrayIndex) {
			int i = arrayIndex;
			foreach (byte b in this) {
				if (i < array.Length)
					array[i++] = b;
				else
					break;
			}
		}

		public IEnumerator<byte> GetEnumerator() {
			NcByteBlock current = _first;
			while (current != null) {
				for (int i = 0; i < current.UsedCount; i++) {
					yield return current.Buffer[i];
				}

				current = current.Next;
			}
		}

		int IList<byte>.IndexOf(byte item) {
			return (int)IndexOf(item);
		}

		private long IndexOf(byte item) {
			long i = 0;
			foreach (byte b in this) {
				if (b == item) return i;
				i++;
			}

			return -1;
		}

		void IList<byte>.Insert(int index, byte item) {
			Insert((long)index, item);
		}

		public void Insert(long index, byte item) {
			int offset;
			NcByteBlock current = BlockAt(index, out offset);

			if (current == null) {
				if (index == 0) {
					// first block
					current = _first = _last = new NcByteBlock();
				}
				else if (index == LongCount) {
					// add onto end
					Add(item);
					return;
				}
				else {
					// can't insert at other indices
					throw new IndexOutOfRangeException();
				}
			}

			byte[] trailing = null;
			if (offset > 0) {
				// copy trailing bytes into a temporary array
				trailing = new byte[current.UsedCount - offset];
				Buffer.BlockCopy(current.Buffer, offset, trailing, 0, trailing.Length);

				// truncate block
				current.UsedCount = offset;
			}
			else {
				// insert block
				NcByteBlock next = new NcByteBlock();
				InsertBlockBefore(current, next);
				current = next;
			}

			current.Buffer[offset] = item;
			current.UsedCount = offset + 1;

			// insert trailing
			if (trailing != null) InsertHelper(ref current, ref offset, trailing);
		}

		public void Insert(long index, IEnumerable<byte> data) {
			int offset;
			NcByteBlock current = BlockAt(index, out offset);

			if (current == null) {
				if (index == 0) {
					// first block
					current = _first = _last = new NcByteBlock();
				}
				else if (index == LongCount) {
					// add onto end
					Add(data);
					return;
				}
				else {
					// can't insert at other indices
					throw new IndexOutOfRangeException();
				}
			}

			byte[] trailing = null;
			if (offset > 0) {			
				// copy trailing bytes into a temporary array
				trailing = new byte[current.UsedCount - offset];
				Buffer.BlockCopy(current.Buffer, offset, trailing, 0, trailing.Length);

				// truncate block
				current.UsedCount = offset;
			}
			else {
				// insert block
				NcByteBlock next = new NcByteBlock();
				InsertBlockBefore(current, next);
				current = next;
			}

			// insert data (add blocks)
			InsertHelper(ref current, ref offset, data);

			// insert trailing
			if (trailing != null) InsertHelper(ref current, ref offset, trailing);
		}

		public void Insert(long index, byte[] data) {
			int offset;
			NcByteBlock current = BlockAt(index, out offset);

			if (current == null) {
				if (index == 0) {
					// first block (allow smaller block size)
					current = _first = _last = new NcByteBlock(Math.Min(data.Length, BLOCK_SIZE));
				}
				else if (index == LongCount) {
					// add onto end
					Add(data);
					return;
				}
				else {
					// can't insert at other indices
					throw new IndexOutOfRangeException();
				}
			}

			byte[] trailing = null;
			if (offset > 0) {
				// copy trailing bytes into a temporary array
				trailing = new byte[current.UsedCount - offset];
				Buffer.BlockCopy(current.Buffer, offset, trailing, 0, trailing.Length);

				// truncate block
				current.UsedCount = offset;
			}
			else {
				// insert block
				NcByteBlock next = new NcByteBlock();
				InsertBlockBefore(current, next);
				current = next;
			}

			// insert data (add blocks)
			InsertHelper(ref current, ref offset, data);

			// insert trailing
			if (trailing != null) InsertHelper(ref current, ref offset, trailing);
		}

		void InsertBlockAfter(NcByteBlock after, NcByteBlock block) {
			NcByteBlock oldNext = after.Next;
			block.Prev = after;
			block.Next = oldNext;
			after.Next = block;

			if (oldNext != null)
				oldNext.Prev = block;
			else
				_last = block;			
		}

		void InsertBlockBefore(NcByteBlock before, NcByteBlock block) {
			block.Next = before;
			block.Prev = before.Prev;

			if (before.Prev != null)
				before.Prev.Next = block;
			else
				_first = block;

			before.Prev = block;
		}

		void InsertHelper(ref NcByteBlock current, ref int offset, IEnumerable<byte> data) {
			long length = data.LongCount();
			long i = 0;
			while (i < length) {
				int count = (int)Math.Min(length, current.Buffer.Length - current.UsedCount);
				foreach (byte b in data.Skip((int)i).Take(count)) {
					current.Buffer[offset++] = b;
					current.UsedCount++;
				}
				i += count;

				if (i < length) {
					// insert next block
					NcByteBlock next = new NcByteBlock();
					InsertBlockAfter(current, next);
					current = next;
					offset = 0;
				}
			}
		}

		void InsertHelper(ref NcByteBlock current, ref int offset, byte[] data) {
			int i = 0;
			while (i < data.Length) {
				int count = Math.Min(data.Length - i, current.Buffer.Length - current.UsedCount);
				Buffer.BlockCopy(data, i, current.Buffer, offset, count);
				current.UsedCount = offset + count;
				i += count;
				offset += count;

				if (i < data.Length) {
					// insert next block
					NcByteBlock next = new NcByteBlock();
					InsertBlockAfter(current, next);
					current = next;
					offset = 0;
				}
			}
		}

		bool ICollection<byte>.Remove(byte item) {
			long ndx = IndexOf(item);
			if (ndx >= 0) {
				RemoveAt(ndx);
				return true;
			}

			return false;
		}

		void IList<byte>.RemoveAt(int index) {
			RemoveAt((long)index);
		}

		public void RemoveAt(long index) {
			Remove(index, 1);
		}

		public void Remove(long index, long count) {
			int startOffset;
			NcByteBlock start = BlockAt(index, out startOffset);
			if (start == null) throw new IndexOutOfRangeException();

			int endOffset;
			NcByteBlock end = BlockAt(index + count, out endOffset);

			byte[] trailing = null;
			if (end != null) {
				// copy trailing bytes
				int trailCount = end.UsedCount - endOffset;
				if (trailCount > 0) {
					trailing = new byte[trailCount];
					Buffer.BlockCopy(end.Buffer, endOffset, trailing, 0, trailCount);
				}
			}

			// truncate start block
			start.UsedCount = startOffset;

			// insert trailing bytes
			if (trailing != null) InsertHelper(ref start, ref startOffset, trailing);

			if (end != null) {
				start.Next = end.Next;
				if (end.Next != null) end.Next.Prev = start;
			}
			else {
				start.Next = null;
				_last = start;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public void Copy(byte[] src, int srcIndex, long destIndex, int count) {
			int offset;
			NcByteBlock current = BlockAt(destIndex, out offset);
			if (current == null) throw new IndexOutOfRangeException();

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				int size = Math.Min(count - i, ((current.Next != null) ? current.UsedCount : current.Buffer.Length) - offset);
				Buffer.BlockCopy(src, srcIndex + i, current.Buffer, offset, size);
				current.UsedCount = offset + size;
				i += size;

				if (current.Next == null) {
					// additional block needed
					NcByteBlock next = new NcByteBlock();
					current.Next = next;
					next.Prev = current;
					_last = next;
				}

				offset = 0;
				current = current.Next;
			}
		}

		public int Copy(long srcIndex, byte[] dest, int destIndex, int count) {
			int offset;
			NcByteBlock current = BlockAt(srcIndex, out offset);
			if (current == null) return 0;

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				int size = Math.Min(count - i, current.UsedCount - offset);
				Buffer.BlockCopy(current.Buffer, offset, dest, destIndex + i, size);
				i += size;

				if (current.Next == null) break;

				offset = 0;
				current = current.Next;
			}

			return i;
		}

		public void Copy(IntPtr src, long destIndex, int count) {
			int offset;
			NcByteBlock current = BlockAt(destIndex, out offset);
			if (current == null) throw new IndexOutOfRangeException();

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				int size = Math.Min(count - i, ((current.Next != null) ? current.UsedCount : current.Buffer.Length) - offset);
				Marshal.Copy(src + i, current.Buffer, offset, size);
				current.UsedCount = offset + size;
				i += size;

				if (current.Next == null) {
					// additional block needed
					NcByteBlock next = new NcByteBlock();
					current.Next = next;
					next.Prev = current;
					_last = next;
				}

				offset = 0;
				current = current.Next;
			}
		}

		public int Copy(long srcIndex, IntPtr dest, int count) {
			int offset;
			NcByteBlock current = BlockAt(srcIndex, out offset);
			if (current == null) return 0;

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				int size = Math.Min(count - i, current.UsedCount - offset);
				Marshal.Copy(current.Buffer, offset, dest + i, size);
				i += size;

				if (current.Next == null) break;

				offset = 0;
				current = current.Next;
			}

			return i;
		}

		private void CloneRange(long index, long count, NcByteCollection dest) {			
			if (count > 0) {
				dest._first = dest._last = new NcByteBlock((int)Math.Min(BLOCK_SIZE, count));

				long end = index + count;
				while (index < end) {
					// read as many bytes as possible
					int bytesRead = Copy(index, dest._last.Buffer, 0, dest._last.Buffer.Length);
					if (bytesRead == 0) break;
					index += bytesRead;
					dest._last.UsedCount = bytesRead;

					if (index < end) {
						// additional block required
						NcByteBlock next = new NcByteBlock();
						dest._last.Next = next;
						next.Prev = dest._last;
						dest._last = next;
					}
				}
			}
		}

		public NcByteCollection Clone(long index, long count) {
			NcByteCollection dest = new NcByteCollection();
			CloneRange(index, count, dest);
			return dest;
		}

		public NcByteCollection Clone() {
			NcByteCollection dest = new NcByteCollection();
			CloneRange(0, LongCount, dest);
			return dest;
		}

		object ICloneable.Clone() {
			return Clone();
		}

		public void Compact() {
			NcByteBlock current = _first;

			while (current != null) {
				if (current.UsedCount < current.Buffer.Length) {
					// block can hold more data
					NcByteBlock next = current.Next;
					while (next != null) {
						// copy from subsequent block(s) until full
						int size = Math.Min(current.Buffer.Length - current.UsedCount, next.UsedCount);
						Buffer.BlockCopy(next.Buffer, 0, current.Buffer, current.UsedCount, size);
						current.UsedCount += size;

						byte[] trailing = null;
						if (size < next.UsedCount) {
							// move trailing bytes to start of block
							trailing = new byte[next.UsedCount - size];
							Buffer.BlockCopy(next.Buffer, size, trailing, 0, trailing.Length);
							Buffer.BlockCopy(trailing, 0, next.Buffer, 0, trailing.Length);
							next.UsedCount = trailing.Length;
						}
						else {
							// source block is empty, remove it
							current.Next = next.Next;
							if (next.Next != null)
								next.Next.Prev = current;
							else
								_last = current;
						}

						if (current.UsedCount >= current.Buffer.Length) break;
						next = next.Next;
					}
				}

				current = current.Next;
			}
		}
	}

	public class NcByteStream : Stream {

		NcByteCollection _data;

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => true;

		public override long Length => _data.LongCount;

		public override long Position { get; set; }

		public NcByteCollection Data => _data;

		public NcByteStream() {
			_data = new NcByteCollection();
		}

		public NcByteStream(NcByteCollection data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			_data = data;
		}

		public NcByteStream(byte[] data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			_data = new NcByteCollection(data);
		}

		public NcByteStream(IEnumerable<byte> data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			_data = new NcByteCollection(data);
		}

		public override void Flush() {
			// nothing required here
		}

		public override int Read(byte[] buffer, int offset, int count) {
			int bytesRead = _data.Copy(Position, buffer, offset, count);
			Position += bytesRead;
			return bytesRead;
		}

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

		public override void SetLength(long value) {
			long diff = _data.LongCount - value;

			if (diff > 0) {
				// truncate end
				_data.Remove(value, diff);
			}
			else if (diff < 0) {
				// pad end
				_data.Grow(-diff);
			}
		}

		public override void Write(byte[] buffer, int offset, int count) {
			_data.Copy(buffer, offset, Position, count);
			Position += count;
		}
	}
}
