using System;
using System.IO;

namespace ICSharpCode.SharpZipLib.Tar
{
	public class TarOutputStream : Stream
	{
		protected bool debug;

		protected long currSize;

		protected int currBytes;

		protected byte[] blockBuf;

		protected int assemLen;

		protected byte[] assemBuf;

		protected TarBuffer buffer;

		protected Stream outputStream;

		public override bool CanRead
		{
			get
			{
				return outputStream.CanRead;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return outputStream.CanSeek;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return outputStream.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				return outputStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return outputStream.Position;
			}
			set
			{
				outputStream.Position = value;
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return outputStream.Seek(offset, origin);
		}

		public override void SetLength(long val)
		{
			outputStream.SetLength(val);
		}

		public override int ReadByte()
		{
			return outputStream.ReadByte();
		}

		public override int Read(byte[] b, int off, int len)
		{
			return outputStream.Read(b, off, len);
		}

		public override void Flush()
		{
			outputStream.Flush();
		}

		public TarOutputStream(Stream outputStream)
			: this(outputStream, TarBuffer.DefaultBlockFactor)
		{
		}

		public TarOutputStream(Stream outputStream, int blockFactor)
		{
			this.outputStream = outputStream;
			buffer = TarBuffer.CreateOutputTarBuffer(outputStream, blockFactor);
			debug = false;
			assemLen = 0;
			assemBuf = new byte[TarBuffer.BlockSize];
			blockBuf = new byte[TarBuffer.BlockSize];
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

		public void Finish()
		{
			WriteEOFRecord();
		}

		public override void Close()
		{
			Finish();
			buffer.Close();
		}

		public int GetRecordSize()
		{
			return buffer.GetRecordSize();
		}

		public void PutNextEntry(TarEntry entry)
		{
			if (entry.TarHeader.name.Length > TarHeader.NAMELEN)
			{
				TarHeader tarHeader = new TarHeader();
				tarHeader.typeFlag = 76;
				tarHeader.name.Append("././@LongLink");
				tarHeader.userId = 0;
				tarHeader.groupId = 0;
				tarHeader.groupName.Length = 0;
				tarHeader.userName.Length = 0;
				tarHeader.linkName.Length = 0;
				tarHeader.size = entry.TarHeader.name.Length;
				tarHeader.WriteHeader(blockBuf);
				buffer.WriteBlock(blockBuf);
				int num = 0;
				while (num < entry.TarHeader.name.Length)
				{
					TarHeader.GetNameBytes(entry.TarHeader.name, num, blockBuf, 0, TarBuffer.BlockSize);
					num += TarBuffer.BlockSize;
					buffer.WriteBlock(blockBuf);
				}
			}
			entry.WriteEntryHeader(blockBuf);
			buffer.WriteBlock(blockBuf);
			currBytes = 0;
			currSize = (entry.IsDirectory ? 0 : entry.Size);
		}

		public void CloseEntry()
		{
			if (assemLen > 0)
			{
				for (int i = assemLen; i < assemBuf.Length; i++)
				{
					assemBuf[i] = 0;
				}
				buffer.WriteBlock(assemBuf);
				currBytes += assemLen;
				assemLen = 0;
			}
			if (currBytes < currSize)
			{
				throw new IOException("entry closed at '" + currBytes + "' before the '" + currSize + "' bytes specified in the header were written");
			}
		}

		public override void WriteByte(byte b)
		{
			Write(new byte[1] { b }, 0, 1);
		}

		public override void Write(byte[] wBuf, int wOffset, int numToWrite)
		{
			if (wBuf == null)
			{
				throw new ArgumentNullException("TarOutputStream.Write buffer null");
			}
			if (currBytes + numToWrite > currSize)
			{
				throw new ArgumentOutOfRangeException("request to write '" + numToWrite + "' bytes exceeds size in header of '" + currSize + "' bytes");
			}
			if (assemLen > 0)
			{
				if (assemLen + numToWrite >= blockBuf.Length)
				{
					int num = blockBuf.Length - assemLen;
					Array.Copy(assemBuf, 0, blockBuf, 0, assemLen);
					Array.Copy(wBuf, wOffset, blockBuf, assemLen, num);
					buffer.WriteBlock(blockBuf);
					currBytes += blockBuf.Length;
					wOffset += num;
					numToWrite -= num;
					assemLen = 0;
				}
				else
				{
					Array.Copy(wBuf, wOffset, assemBuf, assemLen, numToWrite);
					wOffset += numToWrite;
					assemLen += numToWrite;
					numToWrite -= numToWrite;
				}
			}
			while (numToWrite > 0)
			{
				if (numToWrite < blockBuf.Length)
				{
					Array.Copy(wBuf, wOffset, assemBuf, assemLen, numToWrite);
					assemLen += numToWrite;
					break;
				}
				buffer.WriteBlock(wBuf, wOffset);
				int num2 = blockBuf.Length;
				currBytes += num2;
				numToWrite -= num2;
				wOffset += num2;
			}
		}

		private void WriteEOFRecord()
		{
			for (int i = 0; i < blockBuf.Length; i++)
			{
				blockBuf[i] = 0;
			}
			buffer.WriteBlock(blockBuf);
		}
	}
}
