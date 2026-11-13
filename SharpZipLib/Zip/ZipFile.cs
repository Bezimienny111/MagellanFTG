using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace ICSharpCode.SharpZipLib.Zip
{
	public class ZipFile : IEnumerable
	{
		private class ZipEntryEnumeration : IEnumerator
		{
			private ZipEntry[] array;

			private int ptr = -1;

			public object Current
			{
				get
				{
					return array[ptr];
				}
			}

			public ZipEntryEnumeration(ZipEntry[] arr)
			{
				array = arr;
			}

			public void Reset()
			{
				ptr = -1;
			}

			public bool MoveNext()
			{
				return ++ptr < array.Length;
			}
		}

		private class PartialInputStream : InflaterInputStream
		{
			private Stream baseStream;

			private long filepos;

			private long end;

			public override int Available
			{
				get
				{
					long num = end - filepos;
					if (num > int.MaxValue)
					{
						return int.MaxValue;
					}
					return (int)num;
				}
			}

			public PartialInputStream(Stream baseStream, long start, long len)
				: base(baseStream)
			{
				this.baseStream = baseStream;
				filepos = start;
				end = start + len;
			}

			public override int ReadByte()
			{
				if (filepos == end)
				{
					return -1;
				}
				lock (baseStream)
				{
					baseStream.Seek(filepos++, SeekOrigin.Begin);
					return baseStream.ReadByte();
				}
			}

			public override int Read(byte[] b, int off, int len)
			{
				if (len > end - filepos)
				{
					len = (int)(end - filepos);
					if (len == 0)
					{
						return 0;
					}
				}
				lock (baseStream)
				{
					baseStream.Seek(filepos, SeekOrigin.Begin);
					int num = baseStream.Read(b, off, len);
					if (num > 0)
					{
						filepos += len;
					}
					return num;
				}
			}

			public long SkipBytes(long amount)
			{
				if (amount < 0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (amount > end - filepos)
				{
					amount = end - filepos;
				}
				filepos += amount;
				return amount;
			}
		}

		private string name;

		private string comment;

		private Stream baseStream;

		private ZipEntry[] entries;

		[IndexerName("EntryByIndex")]
		public ZipEntry this[int index]
		{
			get
			{
				return (ZipEntry)entries[index].Clone();
			}
		}

		public string ZipFileComment
		{
			get
			{
				return comment;
			}
		}

		public string Name
		{
			get
			{
				return name;
			}
		}

		public int Size
		{
			get
			{
				if (entries != null)
				{
					return entries.Length;
				}
				throw new InvalidOperationException("ZipFile is closed");
			}
		}

		public ZipFile(string name)
			: this(File.OpenRead(name))
		{
		}

		public ZipFile(FileStream file)
		{
			baseStream = file;
			name = file.Name;
			ReadEntries();
		}

		public ZipFile(Stream baseStream)
		{
			this.baseStream = baseStream;
			name = null;
			ReadEntries();
		}

		private int ReadLeShort()
		{
			return baseStream.ReadByte() | (baseStream.ReadByte() << 8);
		}

		private int ReadLeInt()
		{
			return ReadLeShort() | (ReadLeShort() << 16);
		}

		private void ReadEntries()
		{
			if (!baseStream.CanSeek)
			{
				throw new ZipException("ZipFile stream must be seekable");
			}
			long num = baseStream.Length - 22;
			if (num <= 0)
			{
				throw new ZipException("File is too small to be a Zip file");
			}
			long num2 = Math.Max(num - 65536, 0L);
			do
			{
				if (num < num2)
				{
					throw new ZipException("central directory not found, probably not a zip file");
				}
				baseStream.Seek(num--, SeekOrigin.Begin);
			}
			while (ReadLeInt() != 101010256);
			ReadLeShort();
			ReadLeShort();
			ReadLeShort();
			int num3 = ReadLeShort();
			ReadLeInt();
			int num4 = ReadLeInt();
			int num5 = ReadLeShort();
			byte[] array = new byte[num5];
			baseStream.Read(array, 0, array.Length);
			comment = ZipConstants.ConvertToString(array);
			entries = new ZipEntry[num3];
			baseStream.Seek(num4, SeekOrigin.Begin);
			for (int i = 0; i < num3; i++)
			{
				if (ReadLeInt() != 33639248)
				{
					throw new ZipException("Wrong Central Directory signature");
				}
				int madeByInfo = ReadLeShort();
				int versionRequiredToExtract = ReadLeShort();
				ReadLeShort();
				int compressionMethod = ReadLeShort();
				int num6 = ReadLeInt();
				int num7 = ReadLeInt();
				int num8 = ReadLeInt();
				int num9 = ReadLeInt();
				int num10 = ReadLeShort();
				int num11 = ReadLeShort();
				int num12 = ReadLeShort();
				ReadLeShort();
				ReadLeShort();
				int externalFileAttributes = ReadLeInt();
				int offset = ReadLeInt();
				byte[] array2 = new byte[Math.Max(num10, num12)];
				baseStream.Read(array2, 0, num10);
				string text = ZipConstants.ConvertToString(array2, num10);
				ZipEntry zipEntry = new ZipEntry(text, versionRequiredToExtract, madeByInfo);
				zipEntry.CompressionMethod = (CompressionMethod)compressionMethod;
				zipEntry.Crc = num7 & 0xFFFFFFFFu;
				zipEntry.Size = num9 & 0xFFFFFFFFu;
				zipEntry.CompressedSize = num8 & 0xFFFFFFFFu;
				zipEntry.DosTime = (uint)num6;
				if (num11 > 0)
				{
					byte[] array3 = new byte[num11];
					baseStream.Read(array3, 0, num11);
					zipEntry.ExtraData = array3;
				}
				if (num12 > 0)
				{
					baseStream.Read(array2, 0, num12);
					zipEntry.Comment = ZipConstants.ConvertToString(array2, num12);
				}
				zipEntry.ZipFileIndex = i;
				zipEntry.Offset = offset;
				zipEntry.ExternalFileAttributes = externalFileAttributes;
				entries[i] = zipEntry;
			}
		}

		public void Close()
		{
			entries = null;
			lock (baseStream)
			{
				baseStream.Close();
			}
		}

		public IEnumerator GetEnumerator()
		{
			if (entries == null)
			{
				throw new InvalidOperationException("ZipFile has closed");
			}
			return new ZipEntryEnumeration(entries);
		}

		public int FindEntry(string name, bool ignoreCase)
		{
			if (entries == null)
			{
				throw new InvalidOperationException("ZipFile has been closed");
			}
			for (int i = 0; i < entries.Length; i++)
			{
				if (string.Compare(name, entries[i].Name, ignoreCase) == 0)
				{
					return i;
				}
			}
			return -1;
		}

		public ZipEntry GetEntry(string name)
		{
			if (entries == null)
			{
				throw new InvalidOperationException("ZipFile has been closed");
			}
			int num = FindEntry(name, true);
			if (num < 0)
			{
				return null;
			}
			return (ZipEntry)entries[num].Clone();
		}

		private long CheckLocalHeader(ZipEntry entry)
		{
			lock (baseStream)
			{
				baseStream.Seek(entry.Offset, SeekOrigin.Begin);
				if (ReadLeInt() != 67324752)
				{
					throw new ZipException("Wrong Local header signature");
				}
				short num = (short)ReadLeShort();
				if (num > 20)
				{
					throw new ZipException(string.Format("Version required to extract this entry not supported ({0})", num));
				}
				num = (short)ReadLeShort();
				if ((num & 0x30) != 0)
				{
					throw new ZipException("The library doesnt support the zip version required to extract this entry");
				}
				if (entry.CompressionMethod != (CompressionMethod)ReadLeShort())
				{
					throw new ZipException("Compression method mismatch");
				}
				long position = baseStream.Position;
				baseStream.Position += 16L;
				if (baseStream.Position - position != 16)
				{
					throw new EndOfStreamException();
				}
				int num2 = ReadLeShort();
				if (entry.Name.Length > num2)
				{
					throw new ZipException("file name length mismatch");
				}
				int num3 = num2 + ReadLeShort();
				return entry.Offset + 30 + num3;
			}
		}

		public Stream GetInputStream(ZipEntry entry)
		{
			if (entries == null)
			{
				throw new InvalidOperationException("ZipFile has closed");
			}
			int num = entry.ZipFileIndex;
			if (num < 0 || num >= entries.Length || entries[num].Name != entry.Name)
			{
				num = FindEntry(entry.Name, true);
				if (num < 0)
				{
					throw new IndexOutOfRangeException();
				}
			}
			return GetInputStream(num);
		}

		public Stream GetInputStream(int entryIndex)
		{
			if (entries == null)
			{
				throw new InvalidOperationException("ZipFile has closed");
			}
			long start = CheckLocalHeader(entries[entryIndex]);
			CompressionMethod compressionMethod = entries[entryIndex].CompressionMethod;
			Stream stream = new PartialInputStream(baseStream, start, entries[entryIndex].CompressedSize);
			switch (compressionMethod)
			{
			case CompressionMethod.Stored:
				return stream;
			case CompressionMethod.Deflated:
				return new InflaterInputStream(stream, new Inflater(true));
			default:
				throw new ZipException("Unknown compression method " + compressionMethod);
			}
		}
	}
}
