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
	/// Represents a collection of bytes that does not require a contiguous block of memory to be allocated.
	/// </summary>
	public class NcByteCollection : IList<byte>, ICloneable {

		const int BLOCK_SIZE = 4096;

		NcByteBlock _first;
		NcByteBlock _last;

		/// <summary>
		/// Represents a node in a <see cref="NcByteCollection"/>.
		/// </summary>
		private class NcByteBlock {

			/// <summary>
			/// Gets or sets the contiguously-allocated byte array used to hold this block.
			/// </summary>
			public byte[] Buffer { get; set; }
			/// <summary>
			/// Gets or sets the number of bytes actually used by the block.
			/// </summary>
			public int UsedCount { get; set; }
			/// <summary>
			/// Gets or sets the previous node, or null if this is the first node.
			/// </summary>
			public NcByteBlock Prev { get; set; }
			/// <summary>
			/// Gets or sets the next node, or null if this is the last node.
			/// </summary>
			public NcByteBlock Next { get; set; }

			/// <summary>
			/// Initialises a new instance of the <see cref="NcByteBlock"/> class using the specified block size.
			/// </summary>
			/// <param name="capacity"></param>
			public NcByteBlock(int capacity = BLOCK_SIZE) {
				Buffer = new byte[capacity];
			}
		}

		/// <summary>
		/// Gets or sets the byte at the specified index (<see cref="Int64"/>) in the collection.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		/// <remarks>
		/// This is an O(n) operation.
		/// </remarks>
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
		/// <summary>
		/// Gets or sets the byte at the specified index (<see cref="Int32"/>) in the collection.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		/// <remarks>
		/// This is an O(n) operation.
		/// </remarks>
		public byte this[int index] {
			get {
				return this[(long)index];
			}
			set {
				this[(long)index] = value;
			}
		}
		/// <summary>
		/// Gets the number of bytes (<see cref="Int64"/>) in the collection.
		/// </summary>
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
		/// <summary>
		/// Gets the number of bytes (<see cref="Int32"/>) in the collection.
		/// </summary>
		public int Count {
			get {
				return (int)LongCount;
			}
		}

		bool ICollection<byte>.IsReadOnly => false;

		/// <summary>
		/// Initialises a new, empty instance of the <see cref="NcByteCollection"/> 
		/// class.
		/// </summary>
		public NcByteCollection() { }

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteCollection"/> 
		/// class, copying the values from an existing collection.
		/// </summary>
		/// <param name="data"></param>
		public NcByteCollection(NcByteCollection data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			data.CloneRange(0, data.LongCount, this);
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteCollection"/> 
		/// class, copying the values from an existing array of bytes.
		/// </summary>
		/// <param name="data"></param>
		public NcByteCollection(byte[] data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			Add(data);			
		}
		
		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteCollection"/> 
		/// class, copying the values from an existing sequence of bytes.
		/// </summary>
		/// <param name="data"></param>
		public NcByteCollection(IEnumerable<byte> data) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			Add(data);
		}

		/// <summary>
		/// Returns the block containing the byte at the specified index and 
		/// outputs its offset relative to the block.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="offset"></param>
		/// <returns>The block containing the index or null if the index is beyond the end of the collection.</returns>
		private NcByteBlock BlockAt(long index, out int offset) {
			if (index < 0) throw new IndexOutOfRangeException();
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

		/// <summary>
		/// Adds a single byte to the end of the collection.
		/// </summary>
		/// <param name="item"></param>
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

		/// <summary>
		/// Adds a sequence of bytes to the end of the collection.
		/// </summary>
		/// <param name="data"></param>
		public void Add(IEnumerable<byte> data) {
			foreach (byte b in data) {
				Add(b);
			}
		}

		/// <summary>
		/// Adds an array of bytes to the end of the collection.
		/// </summary>
		/// <param name="data"></param>
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

		/// <summary>
		/// Increases the size of the collection by the specified number of bytes. 
		/// The values of the additional bytes are undefined.
		/// </summary>
		/// <param name="count"></param>
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

		/// <summary>
		/// Removes all elements from the collection.
		/// </summary>
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

		void ICollection<byte>.CopyTo(byte[] array, int arrayIndex) {
			int i = arrayIndex;
			foreach (byte b in this) {
				if (i < array.Length)
					array[i++] = b;
				else
					break;
			}
		}

		/// <summary>
		/// Returns an object that can be used to iterate over the collection.
		/// </summary>
		/// <returns></returns>
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

		/// <summary>
		/// Performs a basic linear search to determine the index of the specified byte in the collection.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Inserts a single byte at the specified index in the collection.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
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

		/// <summary>
		/// Inserts a sequence of bytes at the specified index in the collection.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="data"></param>
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

		/// <summary>
		/// Inserts an array of bytes at the specified index in the collection.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="data"></param>
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

		/// <summary>
		/// Removes the byte at the specified index in the collection.
		/// </summary>
		/// <param name="index"></param>
		public void RemoveAt(long index) {
			Remove(index, 1);
		}

		/// <summary>
		/// Removes a range of bytes from the collection, starting at the specified index.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="count"></param>
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

		/// <summary>
		/// Copies a range of bytes from an array into the collection at the specified index.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="srcIndex"></param>
		/// <param name="destIndex"></param>
		/// <param name="count"></param>
		/// <remarks>
		/// This method can be used to overwrite existing data or append new 
		/// data to the end of the collection, but not to insert data.
		/// </remarks>
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

		/// <summary>
		/// Copies a range of bytes, starting from the specified index, into an array.
		/// </summary>
		/// <param name="srcIndex"></param>
		/// <param name="dest"></param>
		/// <param name="destIndex"></param>
		/// <param name="count"></param>
		/// <returns>The actual number of bytes copied to the array.</returns>
		/// <remarks>
		/// If the collection does not contain the requested number of bytes, 
		/// the return value will be less than <paramref name="count"/>.
		/// </remarks>
		public int Copy(long srcIndex, byte[] dest, int destIndex, int count) {
			int offset;
			NcByteBlock current = BlockAt(srcIndex, out offset);
			if (current == null) return 0;

			int i = 0;
			while (i < count) {
				// only the used bytes are read
				int size = Math.Min(count - i, current.UsedCount - offset);
				Buffer.BlockCopy(current.Buffer, offset, dest, destIndex + i, size);
				i += size;

				if (current.Next == null) break;

				offset = 0;
				current = current.Next;
			}

			return i;
		}

		/// <summary>
		/// Copies a range of bytes from unmanaged memory into the collection at the specified index.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="destIndex"></param>
		/// <param name="count"></param>
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

		/// <summary>
		/// Copies a range of bytes, starting from the specified index, into unmanaged memory.
		/// </summary>
		/// <param name="srcIndex"></param>
		/// <param name="dest"></param>
		/// <param name="count"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Copies a range of bytes from this collection into another collection.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="count"></param>
		/// <param name="dest"></param>
		/// <remarks>
		/// Fragmentation in the source collection will not be reflected in the destination.
		/// </remarks>
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

		/// <summary>
		/// Returns a new <see cref="NcByteCollection"/> containing a copy of a range of bytes in this collection.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public NcByteCollection Clone(long index, long count) {
			NcByteCollection dest = new NcByteCollection();
			CloneRange(index, count, dest);
			return dest;
		}

		/// <summary>
		/// Returns a new <see cref="NcByteCollection"/> containing a copy of the data in this collection.
		/// </summary>
		/// <returns></returns>
		public NcByteCollection Clone() {
			NcByteCollection dest = new NcByteCollection();
			CloneRange(0, LongCount, dest);
			return dest;
		}

		object ICloneable.Clone() {
			return Clone();
		}

		/// <summary>
		/// Reduces the amount of memory allocated to the collection by removing internal fragmentation.
		/// </summary>
		/// <remarks>
		/// Because inserts and removals are optimised for performance, blocks may become fragmented. 
		/// You can call this method to improve storage efficiency, at the cost of performance.
		/// </remarks>
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
}
