using System;
using System.IO;
using System.Text;

namespace ICSharpCode.SharpZipLib.Tar
{
	public class TarInputStream : Stream
	{
		public interface IEntryFactory
		{
			TarEntry CreateEntry(string name);

			TarEntry CreateEntryFromFile(string fileName);

			TarEntry CreateEntry(byte[] headerBuf);
		}

		public class EntryFactoryAdapter : IEntryFactory
		{
			public TarEntry CreateEntry(string name)
			{
				return TarEntry.CreateTarEntry(name);
			}

			public TarEntry CreateEntryFromFile(string fileName)
			{
				return TarEntry.CreateEntryFromFile(fileName);
			}

			public TarEntry CreateEntry(byte[] headerBuf)
			{
				return new TarEntry(headerBuf);
			}
		}

		protected bool debug;

		protected bool hasHitEOF;

		protected int entrySize;

		protected int entryOffset;

		protected byte[] readBuf;

		protected TarBuffer buffer;

		protected TarEntry currEntry;

		protected IEntryFactory eFactory;

		private Stream inputStream;

		public override bool CanRead
		{
			get
			{
				return inputStream.CanRead;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return false;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return false;
			}
		}

		public override long Length
		{
			get
			{
				return inputStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return inputStream.Position;
			}
			set
			{
				throw new NotSupportedException("TarInputStream Seek not supported");
			}
		}

		public int Available
		{
			get
			{
				return entrySize - entryOffset;
			}
		}

		public bool IsMarkSupported
		{
			get
			{
				return false;
			}
		}

		public override void Flush()
		{
			inputStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("TarInputStream Seek not supported");
		}

		public override void SetLength(long val)
		{
			throw new NotSupportedException("TarInputStream SetLength not supported");
		}

		public override void Write(byte[] array, int offset, int count)
		{
			throw new NotSupportedException("TarInputStream Write not supported");
		}

		public override void WriteByte(byte val)
		{
			throw new NotSupportedException("TarInputStream WriteByte not supported");
		}

		public TarInputStream(Stream inputStream)
			: this(inputStream, TarBuffer.DefaultBlockFactor)
		{
		}

		public TarInputStream(Stream inputStream, int blockFactor)
		{
			this.inputStream = inputStream;
			buffer = TarBuffer.CreateInputTarBuffer(inputStream, blockFactor);
			readBuf = null;
			debug = false;
			hasHitEOF = false;
			eFactory = null;
		}

		public void SetDebug(bool debugFlag)
		{
			debug = debugFlag;
			SetBufferDebug(debugFlag);
		}

		public void SetBufferDebug(bool debug)
		{
			buffer.SetDebug(debug);
		}

		public void SetEntryFactory(IEntryFactory factory)
		{
			eFactory = factory;
		}

		public override void Close()
		{
			buffer.Close();
		}

		public int GetRecordSize()
		{
			return buffer.GetRecordSize();
		}

		public void Skip(int numToSkip)
		{
			byte[] array = new byte[8192];
			int num = numToSkip;
			while (num > 0)
			{
				int num2 = Read(array, 0, (num > array.Length) ? array.Length : num);
				if (num2 == -1)
				{
					break;
				}
				num -= num2;
			}
		}

		public void Mark(int markLimit)
		{
		}

		public void Reset()
		{
		}

		private void SkipToNextEntry()
		{
			int num = entrySize - entryOffset;
			bool debug2 = debug;
			if (num > 0)
			{
				Skip(num);
			}
			readBuf = null;
		}

		public TarEntry GetNextEntry()
		{
			if (hasHitEOF)
			{
				return null;
			}
			if (currEntry != null)
			{
				SkipToNextEntry();
			}
			byte[] array = buffer.ReadBlock();
			if (array == null)
			{
				bool debug2 = debug;
				hasHitEOF = true;
			}
			else if (buffer.IsEOFBlock(array))
			{
				bool debug3 = debug;
				hasHitEOF = true;
			}
			if (hasHitEOF)
			{
				currEntry = null;
			}
			else
			{
				try
				{
					TarHeader tarHeader = new TarHeader();
					tarHeader.ParseBuffer(array);
					entryOffset = 0;
					entrySize = (int)tarHeader.size;
					StringBuilder stringBuilder = null;
					if (tarHeader.typeFlag == 76)
					{
						byte[] array2 = new byte[TarBuffer.BlockSize];
						int num = entrySize;
						stringBuilder = new StringBuilder();
						while (num > 0)
						{
							int num2 = Read(array2, 0, (num > array2.Length) ? array2.Length : num);
							if (num2 == -1)
							{
								throw new InvalidHeaderException("Failed to read long name entry");
							}
							stringBuilder.Append(TarHeader.ParseName(array2, 0, num2).ToString());
							num -= num2;
						}
						SkipToNextEntry();
						array = buffer.ReadBlock();
					}
					else if (tarHeader.typeFlag == 103)
					{
						SkipToNextEntry();
						array = buffer.ReadBlock();
					}
					else if (tarHeader.typeFlag == TarHeader.LF_XHDR)
					{
						SkipToNextEntry();
						array = buffer.ReadBlock();
					}
					else if (tarHeader.typeFlag == 86)
					{
						SkipToNextEntry();
						array = buffer.ReadBlock();
					}
					else if (tarHeader.typeFlag != 48 && tarHeader.typeFlag != 0 && tarHeader.typeFlag != 53)
					{
						SkipToNextEntry();
						array = buffer.ReadBlock();
					}
					if (eFactory == null)
					{
						currEntry = new TarEntry(array);
						if (stringBuilder != null)
						{
							currEntry.TarHeader.name.Length = 0;
							currEntry.TarHeader.name.Append(stringBuilder.ToString());
						}
					}
					else
					{
						currEntry = eFactory.CreateEntry(array);
					}
					if (array[257] != 117 || array[258] != 115 || array[259] != 116 || array[260] != 97 || array[261] != 114)
					{
						throw new InvalidHeaderException("header magic is not 'ustar', but '" + array[257] + array[258] + array[259] + array[260] + array[261] + "', or (dec) " + (int)array[257] + ", " + (int)array[258] + ", " + (int)array[259] + ", " + (int)array[260] + ", " + (int)array[261]);
					}
					bool debug4 = debug;
					entryOffset = 0;
					entrySize = (int)currEntry.Size;
				}
				catch (InvalidHeaderException ex)
				{
					entrySize = 0;
					entryOffset = 0;
					currEntry = null;
					throw new InvalidHeaderException("bad header in record " + buffer.GetCurrentBlockNum() + " block " + buffer.GetCurrentBlockNum() + ", " + ex.Message);
				}
			}
			return currEntry;
		}

		public override int ReadByte()
		{
			byte[] array = new byte[1];
			int num = Read(array, 0, 1);
			if (num <= 0)
			{
				return -1;
			}
			return array[0];
		}

		public override int Read(byte[] outputBuffer, int offset, int numToRead)
		{
			int num = 0;
			if (entryOffset >= entrySize)
			{
				return 0;
			}
			if (numToRead + entryOffset > entrySize)
			{
				numToRead = entrySize - entryOffset;
			}
			if (readBuf != null)
			{
				int num2 = ((numToRead > readBuf.Length) ? readBuf.Length : numToRead);
				Array.Copy(readBuf, 0, outputBuffer, offset, num2);
				if (num2 >= readBuf.Length)
				{
					readBuf = null;
				}
				else
				{
					int num3 = readBuf.Length - num2;
					byte[] destinationArray = new byte[num3];
					Array.Copy(readBuf, num2, destinationArray, 0, num3);
					readBuf = destinationArray;
				}
				num += num2;
				numToRead -= num2;
				offset += num2;
			}
			while (numToRead > 0)
			{
				byte[] array = buffer.ReadBlock();
				if (array == null)
				{
					throw new IOException("unexpected EOF with " + numToRead + " bytes unread");
				}
				int num4 = numToRead;
				int num5 = array.Length;
				if (num5 > num4)
				{
					Array.Copy(array, 0, outputBuffer, offset, num4);
					readBuf = new byte[num5 - num4];
					Array.Copy(array, num4, readBuf, 0, num5 - num4);
				}
				else
				{
					num4 = num5;
					Array.Copy(array, 0, outputBuffer, offset, num5);
				}
				num += num4;
				numToRead -= num4;
				offset += num4;
			}
			entryOffset += num;
			return num;
		}

		public void CopyEntryContents(Stream outputStream)
		{
			byte[] array = new byte[32768];
			while (true)
			{
				int num = Read(array, 0, array.Length);
				if (num <= 0)
				{
					break;
				}
				outputStream.Write(array, 0, num);
			}
		}
	}
}
