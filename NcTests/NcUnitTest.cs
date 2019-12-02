using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NonContig;

namespace NcTests {

	internal static class NcTestUtils {

		public static byte[] RandomBytes(int count) {
			byte[] data = new byte[count];
			Random rnd = new Random();
			for (int i = 0; i < data.Length; i++) {
				data[i] = (byte)rnd.Next(256);
			}
			return data;
		}
	}

	[TestClass]
	public class NcCollectionTests {

		[TestMethod]
		public void TestAdd_Small() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(100);
			instance.Add(data);

			Assert.IsTrue(instance.SequenceEqual(data));
		}

		[TestMethod]
		public void TestAdd_1() {
			NcByteCollection instance = new NcByteCollection();

			instance.Add(127);

			Assert.IsTrue(instance[0] == 127);
		}

		[TestMethod]
		public void TestAdd_IEnumerable() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(100);
			instance.Add((IEnumerable<byte>)data);

			Assert.IsTrue(instance.SequenceEqual(data));
		}

		[TestMethod]
		public void TestAdd_Medium() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(5 * 1024);
			instance.Add(data);

			Assert.IsTrue(instance.SequenceEqual(data));
		}

		[TestMethod]
		public void TestAdd_Large() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(17 * 1024);
			instance.Add(data);

			Assert.IsTrue(instance.SequenceEqual(data));
		}

		[TestMethod]
		public void TestInsert_Small() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(100);
			byte[] inserted = NcTestUtils.RandomBytes(50);
			instance.Add(initial);
			instance.Insert(10, inserted);

			Assert.IsTrue(instance.SequenceEqual(initial.Take(10).Concat(inserted).Concat(initial.Skip(10))));
		}

		[TestMethod]
		public void TestInsert_Medium() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(5 * 1024);
			byte[] inserted = NcTestUtils.RandomBytes(2 * 1024);
			instance.Add(initial);
			instance.Insert(2048, inserted);

			Assert.IsTrue(instance.SequenceEqual(initial.Take(2048).Concat(inserted).Concat(initial.Skip(2048))));
		}

		[TestMethod]
		public void TestInsert_Large() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(5 * 1024);
			byte[] inserted = NcTestUtils.RandomBytes(10 * 1024);
			instance.Add(initial);
			instance.Insert(5120, inserted);

			Assert.IsTrue(instance.SequenceEqual(initial.Take(5120).Concat(inserted).Concat(initial.Skip(5120))));
		}

		[TestMethod]
		public void TestInsert_IEnumerable() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(5 * 1024);
			byte[] inserted = NcTestUtils.RandomBytes(10 * 1024);
			instance.Add((IEnumerable<byte>)initial);
			instance.Insert(5120, (IEnumerable<byte>)inserted);

			Assert.IsTrue(instance.SequenceEqual(initial.Take(5120).Concat(inserted).Concat(initial.Skip(5120))));
		}

		[TestMethod]
		public void TestInsert_1() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(5 * 1024);
			byte[] inserted = new byte[] { 127 };
			instance.Add((IEnumerable<byte>)initial);
			instance.Insert(5120, inserted[0]);

			Assert.IsTrue(instance.SequenceEqual(initial.Take(5120).Concat(inserted).Concat(initial.Skip(5120))));
		}

		[TestMethod]
		public void TestInsert_Start() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(5 * 1024);
			byte[] inserted = NcTestUtils.RandomBytes(2 * 1024);
			instance.Add(initial);
			instance.Insert(0, inserted);

			Assert.IsTrue(instance.SequenceEqual(inserted.Concat(initial)));
		}

		[TestMethod]
		public void TestInsert_End() {
			NcByteCollection instance = new NcByteCollection();

			byte[] initial = NcTestUtils.RandomBytes(5 * 1024);
			byte[] inserted = NcTestUtils.RandomBytes(2 * 1024);
			instance.Add(initial);
			instance.Insert(initial.Length-1, inserted);

			Assert.IsTrue(instance.SequenceEqual(initial.Take(initial.Length - 1).Concat(inserted).Concat(initial.Skip(initial.Length - 1))));
		}

		[TestMethod]
		public void TestRemove_Small() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(100);
			instance.Add(data);
			instance.Remove(10, 50);

			Assert.IsTrue(instance.SequenceEqual(data.Take(10).Concat(data.Skip(10+50))));
		}

		[TestMethod]
		public void TestRemove_Medium() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(5 * 1024);
			instance.Add(data);
			instance.Remove(3*1024, 1536);

			Assert.IsTrue(instance.SequenceEqual(data.Take(3 * 1024).Concat(data.Skip(3 * 1024 + 1536))));
		}

		[TestMethod]
		public void TestRemove_Large() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(12 * 1024);
			instance.Add(data);
			instance.Remove(3 * 1024, 5*1024);

			Assert.IsTrue(instance.SequenceEqual(data.Take(3 * 1024).Concat(data.Skip(3 * 1024 + 5 * 1024))));
		}

		[TestMethod]
		public void TestRemove_1() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(12 * 1024);
			instance.Add(data);
			instance.RemoveAt(3 * 1024);

			Assert.IsTrue(instance.SequenceEqual(data.Take(3 * 1024).Concat(data.Skip(3 * 1024 + 1))));
		}

		[TestMethod]
		public void TestRemove_Start() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(12 * 1024);
			instance.Add(data);
			instance.Remove(0, 5 * 1024);

			Assert.IsTrue(instance.SequenceEqual(data.Skip(5 * 1024)));
		}

		[TestMethod]
		public void TestRemove_End() {
			NcByteCollection instance = new NcByteCollection();

			byte[] data = NcTestUtils.RandomBytes(12 * 1024);
			instance.Add(data);
			instance.Remove(7 * 1024, 5 * 1024);

			Assert.IsTrue(instance.SequenceEqual(data.Take(7 * 1024)));
		}

		[TestMethod]
		public void TestClone() {
			NcByteCollection original = new NcByteCollection(NcTestUtils.RandomBytes(12 * 1024));
			NcByteCollection cloned = original.Clone();

			Assert.IsTrue(original.SequenceEqual(cloned));
		}

		[TestMethod]
		public void CompactTest() {
			NcByteCollection data = new NcByteCollection(NcTestUtils.RandomBytes(12 * 1024));

			data.Remove(3 * 1024, 100);
			data.Insert(2 * 1024, NcTestUtils.RandomBytes(200));
			data.Remove(5 * 1024, 5 * 1024);
			data.Insert(6 * 1024, NcTestUtils.RandomBytes(5 * 1024));
			data.Remove(7 * 1024, 3 * 1024);

			byte[] snapshot = data.ToArray();
			data.Compact();
			byte[] compacted = data.ToArray();

			Assert.IsTrue(snapshot.SequenceEqual(compacted));
		}
	}

	[TestClass]
	public class NcStreamTests {

		[TestMethod]
		public void TestStreamRead() {
			byte[] data = NcTestUtils.RandomBytes(12 * 1024);
			NcByteStream stream = new NcByteStream(data);
			MemoryStream ms = new MemoryStream();

			stream.Seek(5 * 1024, SeekOrigin.Begin);
			stream.CopyTo(ms);

			Assert.IsTrue(ms.ToArray().SequenceEqual(data.Skip(5 * 1024)));
		}

		[TestMethod]
		public void TestStreamWrite() {
			NcByteCollection original = new NcByteCollection(NcTestUtils.RandomBytes(12 * 1024));
			NcByteStream stream = new NcByteStream(original);
			MemoryStream ms = new MemoryStream(NcTestUtils.RandomBytes(5 * 1024));

			stream.Seek(5 * 1024, SeekOrigin.Begin);
			ms.CopyTo(stream);

			Assert.IsTrue(original.Take(5 * 1024).Concat(ms.ToArray()).Concat(original.Skip(5 * 1024 + 5 * 1024)).SequenceEqual(stream.Data));
		}
	}
}
