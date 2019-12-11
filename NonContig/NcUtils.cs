using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace NonContig {

	public static class NcUtils {

		/// <summary>
		/// Opens a binary file, reads the contents into a <see cref="NcByteCollection"/> and then closes the file.
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static NcByteCollection ReadAllBytes(string filename) {
			NcByteStream nbs = new NcByteStream();
			using (Stream s = File.OpenRead(filename)) {
				s.CopyTo(nbs);
			}
			return nbs.Data;
		}

		/// <summary>
		/// Creates a new file, writes the specified <see cref="NcByteCollection"/> 
		/// to the file, and then closes the file. If the target file already exists, 
		/// it is overwritten.
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static void WriteAllBytes(string filename, NcByteCollection data) {
			using (NcByteStream nbs = new NcByteStream(data)) {
				using (Stream s = File.Open(filename, FileMode.Create, FileAccess.Write)) {
					nbs.CopyTo(s);
				}
			}
		}

		/// <summary>
		/// Encodes the specified string and returns a <see cref="NcByteCollection"/> containing the binary data.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		public static NcByteCollection EncodeString(string value, Encoding encoding) {
			NcByteStream nbs = new NcByteStream();
			
			using (StreamWriter sw = new StreamWriter(nbs, encoding, NcByteCollection.DEFAULT_BLOCK_SIZE, true)) {
				sw.Write(value);
			}

			return nbs.Data;
		}

		/// <summary>
		/// Decodes the binary data in the specified <see cref="NcByteCollection"/> and returns a string.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="encoding"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public static string DecodeString(NcByteCollection data, Encoding encoding, int offset = 0, int? count = null) {
			using (NcByteStream nbs = new NcByteStream(data)) {
				nbs.Position = offset;

				using (StreamReader sr = new StreamReader(nbs, encoding)) {
					if (count.HasValue) {
						char[] buffer = new char[count.Value];
						int charsRead;
						int bufPos = 0;
						while ((bufPos < buffer.Length) && ((charsRead = sr.Read(buffer, bufPos, buffer.Length)) > 0)) {
							bufPos += charsRead;
						}
						return new string(buffer, 0, bufPos);
					}
					else {
						return sr.ReadToEnd();
					}
				}
			}
		}
	}
}
