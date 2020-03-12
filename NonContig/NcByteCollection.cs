using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace NonContig {

	/// <summary>
	/// Represents a collection of bytes that does not require a contiguous 
	/// block of memory to be allocated.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This type implements <see cref="IList{Byte}"/> to allow both sequential 
	/// and random access to individual bytes. Additional methods allow adding, 
	/// inserting and removing sequences of bytes.
	/// </para>
	/// <para>
	/// The collection can be duplicated via <see cref="ICloneable"/>, with 
	/// additional methods to copy subsets of the data. Also included are the 
	/// stream-like <see cref="Copy"/> methods which allow efficient reading 
	/// and writing using managed or unmanaged memory.
	/// </para>
	/// <para>
	/// Custom serialization is implemented via <see cref="ISerializable"/> and 
	/// <see cref="IXmlSerializable"/>.
	/// </para>
	/// </remarks>
	[Serializable]
	public class NcByteCollection : IList<byte>, ICloneable, ISerializable, IXmlSerializable {

		internal const int DEFAULT_BLOCK_SIZE = 4096;
		static bool? _isMemCmpSupported;

		/// <summary>
		/// Gets a value indicating whether <see cref="NativeMethods.memcmp"/> is supported.
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// Since the function is part of the C runtime library, the required DLL may 
		/// not be installed or supported on the current platform.
		/// </remarks>
		private static bool IsMemCmpSupported {
			get {
				if (!_isMemCmpSupported.HasValue) {
					_isMemCmpSupported = false;
					try {
						Marshal.PrelinkAll(typeof(NativeMethods));
						_isMemCmpSupported = true;
					}
					catch { }
				}

				return _isMemCmpSupported.Value;
			}
		}

		readonly int _blockSize;
		NcByteBlock _first;
		NcByteBlock _last;

		/// <summary>
		/// Represents a node in a <see cref="NcByteCollection"/>.
		/// </summary>
		private class NcByteBlock {

			/// <summary>
			/// The contiguously-allocated byte array used to hold this block.
			/// </summary>
			public byte[] Buffer;
			/// <summary>
			/// The number of bytes actually used by the block.
			/// </summary>
			public int UsedCount;
			/// <summary>
			/// The previous node, or null if this is the first node.
			/// </summary>
			public NcByteBlock Prev;
			/// <summary>
			/// The next node, or null if this is the last node.
			/// </summary>
			public NcByteBlock Next;

			/// <summary>
			/// Initialises a new instance of the <see cref="NcByteBlock"/> class using the specified block size.
			/// </summary>
			/// <param name="length"></param>
			public NcByteBlock(int length) {
				Buffer = new byte[length];
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
#if DEBUG
		/// <summary>
		/// Gets the number of blocks used by the collection to store its data.
		/// </summary>
		public int BlockCount {
			get {
				int count = 0;

				NcByteBlock current = _first;
				while (current != null) {
					count++;
					current = current.Next;
				}

				return count;
			}
		}
		/// <summary>
		/// Gets the total block bytes, regardless of whether they are used or free.
		/// </summary>
		public long BlockLengthTotal {
			get {
				long count = 0;

				NcByteBlock current = _first;
				while (current != null) {
					count += current.Buffer.Length;
					current = current.Next;
				}

				return count;
			}
		}
#endif

		bool ICollection<byte>.IsReadOnly => false;

		/// <summary>
		/// Initialises a new, empty instance of the <see cref="NcByteCollection"/> 
		/// class using the specified block size.
		/// </summary>
		/// <param name="blockSize"></param>
		public NcByteCollection(int blockSize) {
			_blockSize = blockSize;
		}

		/// <summary>
		/// Initialises a new, empty instance of the <see cref="NcByteCollection"/> 
		/// class.
		/// </summary>
		public NcByteCollection() : this(DEFAULT_BLOCK_SIZE) { }

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteCollection"/> 
		/// class, copying the values from an existing collection.
		/// </summary>
		/// <param name="data"></param>
		/// <remarks>
		/// This constructor allocates exactly the number of bytes required to hold the source data.
		/// </remarks>
		public NcByteCollection(NcByteCollection data, int? blockSize = null) : this(blockSize ?? GetAutoBlockSize(data)) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			Reserve(data.LongCount);
			AddRange(data);
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteCollection"/> 
		/// class, copying the values from an existing array of bytes.
		/// </summary>
		/// <param name="data"></param>
		/// <remarks>
		/// This constructor allocates exactly the number of bytes required to hold the source data.
		/// </remarks>
		public NcByteCollection(byte[] data, int? blockSize = null) : this(blockSize ?? GetAutoBlockSize(data)) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			Reserve(data.LongLength);
			AddRange(data);
		}

		/// <summary>
		/// Initialises a new instance of the <see cref="NcByteCollection"/> 
		/// class, copying the values from an existing sequence of bytes.
		/// </summary>
		/// <param name="data"></param>
		/// <remarks>
		/// If <paramref name="data"/> implements <see cref="ICollection{Byte}"/> 
		/// then this constructor allocates exactly the number of bytes 
		/// required to hold the source data.
		/// </remarks>
		public NcByteCollection(IEnumerable<byte> data, int? blockSize = null) : this(blockSize ?? GetAutoBlockSize(data)) {
			if (data == null) throw new ArgumentNullException(nameof(data));
			var maybeCollection = data as ICollection<byte>;
			if (maybeCollection != null) Reserve(maybeCollection.Count);
			AddRange(data);
		}

		/// <summary>
		/// Serialization constructor.
		/// </summary>
		/// <param name="info"></param>
		/// <param name="context"></param>
		protected NcByteCollection(SerializationInfo info, StreamingContext context) : this() {
			int blockCount = info.GetInt32("Count");
			long position = 0;

			for (int i = 0; i < blockCount; i++) {
				string name = String.Format("Block{0}", i);
				byte[] value = (byte[])info.GetValue(name, typeof(byte[]));
				Copy(value, 0, position, value.Length);
				position += value.Length;
			}
		}

		/// <summary>
		/// Calculates an optimal block size based on the size of the specified data.
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		/// <remarks>
		/// This method selects a block size between 4KB and 32KB depending on the length of the data.
		/// </remarks>
		private static int GetAutoBlockSize(IEnumerable<byte> data) {
			var maybeCollection = data as ICollection<byte>;
			if (maybeCollection != null) {
				return Math.Max(1, Math.Min(8, maybeCollection.Count / (DEFAULT_BLOCK_SIZE * 2))) * DEFAULT_BLOCK_SIZE;
			}

			return DEFAULT_BLOCK_SIZE;
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
		/// Returns the first block in which new data can be added, or null if 
		/// there are no blocks or the last block is full.
		/// </summary>
		/// <returns></returns>
		private NcByteBlock NextBlock() {
			NcByteBlock current = _last;
			while (current != null) {
				if ((current.UsedCount > 0) || (current.Prev == null)) {
					if (current.UsedCount >= current.Buffer.Length) current = current.Next;
					break;
				}
				current = current.Prev;
			}

			return current;
		}

		/// <summary>
		/// Returns the first block in which new data can be added (and updates 
		/// <paramref name="offset"/>), or null if there are no blocks or the 
		/// last block is full.
		/// </summary>
		/// <param name="offset"></param>
		/// <returns></returns>
		private NcByteBlock NextBlock(ref int offset) {
			NcByteBlock next = NextBlock();
			if (next != null) offset = next.UsedCount;
			return next;
		}

		/// <summary>
		/// Adds a single byte to the end of the collection.
		/// </summary>
		/// <param name="item"></param>
		public void Add(byte item) {
			int offset = 0;
			NcByteBlock current = NextBlock(ref offset);
			if (current == null) {
				if (_last == null) {
					// first block
					current = _first = _last = new NcByteBlock(_blockSize);
				}
				else {
					// subsequent blocks
					current = new NcByteBlock(_blockSize);
					_last.Next = current;
					current.Prev = _last;
					_last = current;
				}
			}

			current.Buffer[offset] = item;
			current.UsedCount++;
		}

		/// <summary>
		/// Adds a sequence of bytes to the end of the collection.
		/// </summary>
		/// <param name="data"></param>
		public void AddRange(IEnumerable<byte> data) {
			foreach (byte b in data) {
				Add(b);
			}
		}

		/// <summary>
		/// Adds the contents of another <see cref="NcByteCollection"/> to the end of the collection.
		/// </summary>
		/// <param name="other"></param>
		public void AddRange(NcByteCollection other) {
			NcByteBlock current = other._first;
			long i = LongCount;

			while (current != null) {
				Copy(current.Buffer, 0, i, current.UsedCount);
				i += current.UsedCount;
				current = current.Next;
			}
		}

		/// <summary>
		/// Adds an array of bytes to the end of the collection.
		/// </summary>
		/// <param name="data"></param>
		public void AddRange(byte[] data) {
			Copy(data, 0, LongCount, data.Length);
		}

		/// <summary>
		/// Increases the size of the collection by the specified number of bytes. 
		/// The values of the additional bytes are undefined.
		/// </summary>
		/// <param name="count"></param>
		public void Grow(long count) {
			long i = 0;
			int size;

			while (i < count) {
				if (_last == null) {
					// first block (allow smaller block size)
					_first = _last = new NcByteBlock(size = (int)Math.Min(count, _blockSize));
				}
				else if ((size = _last.Buffer.Length - _last.UsedCount) == 0) {
					// new block needed, becomes the new last node
					NcByteBlock next = new NcByteBlock(size = _blockSize);
					_last.Next = next;
					next.Prev = _last;
					_last = next;
				}

				size = (int)Math.Min(size, count - i);
				_last.UsedCount += size;
				i += size;
			}
		}

		/// <summary>
		/// Reserves the specified number of additional bytes for the collection.
		/// </summary>
		/// <param name="count"></param>
		/// <remarks>
		/// This method allocates exactly the number of bytes requested and therefore 
		/// should not be called frequently with only small increments in size.
		/// </remarks>
		public void Reserve(long count) {
			long i = 0;

			while (i < count) {
				if (_last == null) {
					// first block
					_first = _last = new NcByteBlock((int)Math.Min(count, _blockSize));
				}
				else {
					i += _last.Buffer.Length;

					if (i < count) {
						// next block needed
						NcByteBlock next = new NcByteBlock((int)Math.Min(count - i, _blockSize));
						_last.Next = next;
						next.Prev = _last;
						_last = next;
					}
				}
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
					current = _first = _last = new NcByteBlock(_blockSize);
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
				NcByteBlock next = new NcByteBlock(_blockSize);
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
		public void InsertRange(long index, IEnumerable<byte> data) {
			int offset;
			NcByteBlock current = BlockAt(index, out offset);

			if (current == null) {
				if (index == 0) {
					// first block
					current = _first = _last = new NcByteBlock(_blockSize);
				}
				else if (index == LongCount) {
					// add onto end
					AddRange(data);
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
				NcByteBlock next = new NcByteBlock(_blockSize);
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
		public void InsertRange(long index, byte[] data) {
			int offset;
			NcByteBlock current = BlockAt(index, out offset);

			if (current == null) {
				if (index == 0) {
					// first block (allow smaller block size)
					current = _first = _last = new NcByteBlock(Math.Min(data.Length, _blockSize));
				}
				else if (index == LongCount) {
					// add onto end
					AddRange(data);
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
				NcByteBlock next = new NcByteBlock(_blockSize);
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
					NcByteBlock next = new NcByteBlock(_blockSize);
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
					NcByteBlock next = new NcByteBlock(_blockSize);
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
			RemoveRange(index, 1);
		}

		/// <summary>
		/// Removes a range of bytes from the collection, starting at the specified index.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="count"></param>
		public void RemoveRange(long index, long count) {
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
			NcByteBlock current = BlockAt(destIndex, out offset) ?? NextBlock(ref offset);
			if (current == null) {
				if ((destIndex == 0) && (_last == null)) {
					// first block
					current = _first = _last = new NcByteBlock((int)Math.Min(count, _blockSize));
				}
				else if (destIndex == LongCount) {
					// subsequent blocks
					current = new NcByteBlock(_blockSize);
					_last.Next = current;
					current.Prev = _last;
					_last = current;
				}
				else {
					throw new IndexOutOfRangeException();
				}
			}

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				bool canResize = (current.Next == null) || (current == NextBlock());
				int size = (int)Math.Min(count - i, (canResize ? current.Buffer.Length : current.UsedCount) - offset);
				Buffer.BlockCopy(src, srcIndex + i, current.Buffer, offset, size);
				current.UsedCount = Math.Max(current.UsedCount, offset + size);
				i += size;

				if ((current.Next == null) && (i < count)) {
					// additional block needed
					NcByteBlock next = new NcByteBlock(_blockSize);
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
			NcByteBlock current = BlockAt(destIndex, out offset) ?? NextBlock(ref offset);
			if (current == null) {
				if (destIndex == 0) {
					// first block
					current = _first = _last = new NcByteBlock(Math.Min(count, _blockSize));
				}
				else if (destIndex == LongCount) {
					// subsequent blocks
					current = new NcByteBlock(_blockSize);
					_last.Next = current;
					current.Prev = _last;
					_last = current;
				}
				else {
					throw new IndexOutOfRangeException();
				}
			}

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				bool canResize = (current.Next == null) || (current == NextBlock());
				int size = (int)Math.Min(count - i, (canResize ? current.Buffer.Length : current.UsedCount) - offset);
				Marshal.Copy(src + i, current.Buffer, offset, size);
				current.UsedCount = Math.Max(current.UsedCount, offset + size);
				i += size;

				if ((current.Next == null) && (i < count)) {
					// additional block needed
					NcByteBlock next = new NcByteBlock(_blockSize);
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
		/// Copies a range of bytes from a stream into the collection at the specified index.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="destIndex"></param>
		/// <param name="count"></param>
		/// <returns>The number of bytes actually copied from the stream.</returns>
		/// <remarks>
		/// <para>
		/// If the stream does not contain the requested number of bytes, 
		/// the return value will be less than <paramref name="count"/>.
		/// </para>
		/// <para>
		/// This method copies data directly from the stream without using an intermediate buffer.
		/// </para>
		/// </remarks>
		public int Copy(Stream src, long destIndex, int count) {
			int offset;
			NcByteBlock current = BlockAt(destIndex, out offset) ?? NextBlock(ref offset);
			if (current == null) {
				if (destIndex == 0) {
					// first block
					current = _first = _last = new NcByteBlock(Math.Min(count, _blockSize));
				}
				else if (destIndex == LongCount) {
					// subsequent blocks
					current = new NcByteBlock(_blockSize);
					_last.Next = current;
					current.Prev = _last;
					_last = current;
				}
				else {
					throw new IndexOutOfRangeException();
				}
			}

			int i = 0;
			while (i < count) {
				// existing blocks can't be resized (except the last block)
				bool canResize = (current.Next == null) || (current == NextBlock());
				int size = (int)Math.Min(count - i, (canResize ? current.Buffer.Length : current.UsedCount) - offset);

				int actual;
				int remaining = size;
				do {
					// keep reading from source stream until we get the requested number of bytes or we hit the end of the stream
					actual = src.Read(current.Buffer, offset, remaining);
					offset += actual;
					i += actual;
					remaining -= actual;
					current.UsedCount = Math.Max(current.UsedCount, offset);

					if ((remaining > 0) && (actual == 0)) {
						// stream does not contain requested number of bytes
						return i;
					}
				}
				while ((actual > 0) && (actual < remaining));

				if ((current.Next == null) && (i < count)) {
					// additional block needed
					NcByteBlock next = new NcByteBlock(_blockSize);
					current.Next = next;
					next.Prev = current;
					_last = next;
				}

				offset = 0;
				current = current.Next;
			}

			return i;
		}

		/// <summary>
		/// Copies a range of bytes, starting from the specified index, into a stream.
		/// </summary>
		/// <param name="srcIndex"></param>
		/// <param name="dest"></param>
		/// <param name="count"></param>
		/// <returns>The number of bytes actually copied to the stream.</returns>
		/// <remarks>
		/// <para>
		/// If the collection does not contain the requested number of bytes, 
		/// the return value will be less than <paramref name="count"/>.
		/// </para>
		/// <para>
		/// This method copies data directly to the stream without using an intermediate buffer.
		/// </para>
		/// </remarks>
		public int Copy(long srcIndex, Stream dest, int count) {
			int offset;
			NcByteBlock current = BlockAt(srcIndex, out offset);
			if (current == null) return 0;

			int i = 0;
			while (i < count) {
				int size = Math.Min(count - i, current.UsedCount - offset);
				dest.Write(current.Buffer, offset, size);
				i += size;

				if (current.Next == null) break;

				offset = 0;
				current = current.Next;
			}

			return i;
		}

		/// <summary>
		/// Writes the entire collection to the specified <see cref="BinaryWriter"/>.
		/// </summary>
		/// <param name="dest"></param>
		public void WriteTo(BinaryWriter dest) {
			NcByteBlock current = _first;

			while (current != null) {
				dest.Write(current.Buffer, 0, current.UsedCount);
				current = current.Next;
			}
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
				dest._first = dest._last = new NcByteBlock((int)Math.Min(_blockSize, count));

				long end = index + count;
				while (index < end) {
					// read as many bytes as possible
					int bytesRead = Copy(index, dest._last.Buffer, 0, dest._last.Buffer.Length);
					if (bytesRead == 0) break;
					index += bytesRead;
					dest._last.UsedCount = bytesRead;

					if (index < end) {
						// additional block required
						NcByteBlock next = new NcByteBlock(_blockSize);
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

		/// <summary>
		/// Copies the data in the collection into a byte array.
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// Included for compatibility with APIs that require contiguously-allocated memory.
		/// </remarks>
		public byte[] ToArray() {
			byte[] dest = new byte[Count];
			Copy(0, dest, 0, dest.Length);
			return dest;
		}

		/// <summary>
		/// Populates a <see cref="SerializationInfo"/> with the data needed to 
		/// serialize the object.
		/// </summary>
		/// <param name="info"></param>
		/// <param name="context"></param>
		/// <remarks>
		/// Any block fragmentation present in the collection will persist in 
		/// the serialized form of the object, but is compacted during 
		/// deserialization.
		/// </remarks>
		protected virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
			int blockCount = 0;
			NcByteBlock current = _first;
			while (current != null) {
				string name = String.Format("Block{0}", blockCount++);
				byte[] value = new byte[current.UsedCount];
				Buffer.BlockCopy(current.Buffer, 0, value, 0, current.UsedCount);
				info.AddValue(name, value);
				current = current.Next;
			}

			info.AddValue("Count", blockCount);
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
			GetObjectData(info, context);
		}

		XmlSchema IXmlSerializable.GetSchema() {
			return null;
		}

		/// <summary>
		/// Generates the object from its XML representation.
		/// </summary>
		/// <param name="reader"></param>
		protected virtual void ReadXml(XmlReader reader) {
			long position = 0;

			while (reader.LocalName.Equals("Block") || reader.ReadToFollowing("Block")) {
				byte[] buffer = new byte[_blockSize];
				int bytesRead = 0;
				while ((bytesRead = reader.ReadElementContentAsBase64(buffer, 0, buffer.Length)) > 0) {
					Copy(buffer, 0, position, bytesRead);
					position += bytesRead;
				}
			}
		}

		void IXmlSerializable.ReadXml(XmlReader reader) {
			ReadXml(reader);
		}

		/// <summary>
		/// Converts the object into its XML representation.
		/// </summary>
		/// <param name="writer"></param>
		/// <remarks>
		/// Any block fragmentation present in the collection will persist in 
		/// the serialized form of the object, but is compacted during 
		/// deserialization.
		/// </remarks>
		protected virtual void WriteXml(XmlWriter writer) {
			NcByteBlock current = _first;
			while (current != null) {
				writer.WriteStartElement("Block");
				writer.WriteBase64(current.Buffer, 0, current.UsedCount);
				writer.WriteEndElement();
				current = current.Next;
			}
		}

		void IXmlSerializable.WriteXml(XmlWriter writer) {
			WriteXml(writer);
		}

		/// <summary>
		/// Native methods used by <see cref="SequenceEqual"/>.
		/// </summary>
		private static class NativeMethods {

			/// <summary>
			/// Compares bytes in two buffers.
			/// </summary>
			/// <param name="buffer1"></param>
			/// <param name="buffer2"></param>
			/// <param name="count"></param>
			/// <returns>Indicates the relationship between the buffers. 0 means buffer1 identical to buffer2.</returns>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
			public static extern int memcmp(IntPtr buffer1, IntPtr buffer2, IntPtr count);
		}

		/// <summary>
		/// Determines whether two subarrays of bytes are equal.
		/// </summary>
		/// <param name="range1"></param>
		/// <param name="offset1"></param>
		/// <param name="range2"></param>
		/// <param name="offset2"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		/// <remarks>
		/// Working backwards lets the compiler optimize away bound checking after the first loop.
		/// </remarks>
		private static bool Compare(byte[] range1, int offset1, byte[] range2, int offset2, int count) {
			for (int i = count - 1; i >= 0; --i) {
				if (range1[offset1 + i] != range2[offset2 + i]) {
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Determines whether the contents of two collections of bytes are equal.
		/// </summary>
		/// <param name="that"></param>
		/// <returns></returns>
		/// <remarks>
		/// <para>
		/// This is a performance-optimised replacement for <see cref="Enumerable.SequenceEqual"/>. 
		/// It uses the native <see href="https://docs.microsoft.com/en-us/cpp/c-runtime-library/reference/memcmp-wmemcmp">memcmp</see> 
		/// function from the C runtime library, falling back to managed code if not supported.
		/// </para>
		/// </remarks>
		public bool SequenceEqual(NcByteCollection that) {
			long total = this.LongCount;
			long pos = 0;

			// different lengths
			if (total != that.LongCount) return false;

			// both empty
			if (total == 0) return true;

			if (IsMemCmpSupported) {
				// memcmp version
				NcByteBlock bX = this._first;
				int iX = 0;
				GCHandle? gX = GCHandle.Alloc(bX.Buffer, GCHandleType.Pinned);

				NcByteBlock bY = that._first;
				int iY = 0;
				GCHandle? gY = GCHandle.Alloc(bY.Buffer, GCHandleType.Pinned);

				try {
					while (pos < total) {
						// compare largest subarray
						int sX = bX.UsedCount - iX;
						int sY = bY.UsedCount - iY;
						int s = Math.Min(sX, sY);

						int cmp = NativeMethods.memcmp(
							Marshal.UnsafeAddrOfPinnedArrayElement(bX.Buffer, iX),
							Marshal.UnsafeAddrOfPinnedArrayElement(bY.Buffer, iY),
							new IntPtr(s)
						);

						if (cmp == 0) {
							// memory equal, continue
							pos += s;

							iX += s;
							if (iX >= bX.UsedCount) {
								// move to next block in X
								iX = 0;
								bX = bX.Next;
								gX.Value.Free();
								gX = null;
								if (bX != null) gX = GCHandle.Alloc(bX.Buffer, GCHandleType.Pinned);
							}

							iY += s;
							if (iY >= bY.UsedCount) {
								// move to next block in Y
								iY = 0;
								bY = bY.Next;
								gY.Value.Free();
								gY = null;
								if (bY != null) gY = GCHandle.Alloc(bY.Buffer, GCHandleType.Pinned);
							}
						}
						else {
							return false;
						}
					}
				}
				finally {
					if (gX.HasValue) gX.Value.Free();
					if (gY.HasValue) gY.Value.Free();
				}

				// reached the end without encountering inequality
				return true;
			}
			else {
				// span version
				NcByteBlock bX = this._first;
				int iX = 0;

				NcByteBlock bY = that._first;
				int iY = 0;

				while (pos < total) {
					// compare largest subarray
					int sX = bX.UsedCount - iX;
					int sY = bY.UsedCount - iY;
					int s = Math.Min(sX, sY);

					if (Compare(bX.Buffer, iX, bY.Buffer, iY, s)) {
						// memory equal, continue
						pos += s;

						iX += s;
						if (iX >= bX.UsedCount) {
							// move to next block in X
							iX = 0;
							bX = bX.Next;
						}

						iY += s;
						if (iY >= bY.UsedCount) {
							// move to next block in Y
							iY = 0;
							bY = bY.Next;
						}
					}
					else {
						return false;
					}
				}

				// reached the end without encountering inequality
				return true;
			}
		}
	}
}
