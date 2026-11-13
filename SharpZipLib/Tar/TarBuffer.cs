using System;
using System.IO;

namespace ICSharpCode.SharpZipLib.Tar
{
	public class TarBuffer
	{
		public static readonly int BlockSize = 512;

		public static readonly int DefaultBlockFactor = 20;

		public static readonly int DefaultRecordSize = BlockSize * DefaultBlockFactor;

		private Stream inputStream;

		private Stream outputStream;

		private byte[] recordBuffer;

		private int currentBlockIndex;

		private int currentRecordIndex;

		private int recordSize = DefaultRecordSize;

		private int blockFactor = DefaultBlockFactor;

		private bool debug = false;

		public int RecordSize
		{
			get
			{
				return recordSize;
			}
		}

		public int BlockFactor
		{
			get
			{
				return blockFactor;
			}
		}

		public void SetDebug(bool debug)
		{
			this.debug = debug;
		}

		protected TarBuffer()
		{
		}

		public static TarBuffer CreateInputTarBuffer(Stream inputStream)
		{
			return CreateInputTarBuffer(inputStream, DefaultBlockFactor);
		}

		public static TarBuffer CreateInputTarBuffer(Stream inputStream, int blockFactor)
		{
			TarBuffer tarBuffer = new TarBuffer();
			tarBuffer.inputStream = inputStream;
			tarBuffer.outputStream = null;
			tarBuffer.Initialize(blockFactor);
			return tarBuffer;
		}

		public static TarBuffer CreateOutputTarBuffer(Stream outputStream)
		{
			return CreateOutputTarBuffer(outputStream, DefaultBlockFactor);
		}

		public static TarBuffer CreateOutputTarBuffer(Stream outputStream, int blockFactor)
		{
			TarBuffer tarBuffer = new TarBuffer();
			tarBuffer.inputStream = null;
			tarBuffer.outputStream = outputStream;
			tarBuffer.Initialize(blockFactor);
			return tarBuffer;
		}

		private void Initialize(int blockFactor)
		{
			debug = false;
			this.blockFactor = blockFactor;
			recordSize = blockFactor * BlockSize;
			recordBuffer = new byte[RecordSize];
			if (inputStream != null)
			{
				currentRecordIndex = -1;
				currentBlockIndex = BlockFactor;
			}
			else
			{
				currentRecordIndex = 0;
				currentBlockIndex = 0;
			}
		}

		public int GetBlockFactor()
		{
			return blockFactor;
		}

		public int GetRecordSize()
		{
			return recordSize;
		}

		public bool IsEOFBlock(byte[] block)
		{
			int i = 0;
			for (int blockSize = BlockSize; i < blockSize; i++)
			{
				if (block[i] != 0)
				{
					return false;
				}
			}
			return true;
		}

		public void SkipBlock()
		{
			bool debug2 = debug;
			if (inputStream == null)
			{
				throw new ApplicationException("no input stream defined");
			}
			if (currentBlockIndex < BlockFactor || ReadRecord())
			{
				currentBlockIndex++;
			}
		}

		public byte[] ReadBlock()
		{
			bool debug2 = debug;
			if (inputStream == null)
			{
				throw new ApplicationException("TarBuffer.ReadBlock - no input stream defined");
			}
			if (currentBlockIndex >= BlockFactor && !ReadRecord())
			{
				return null;
			}
			byte[] array = new byte[BlockSize];
			Array.Copy(recordBuffer, currentBlockIndex * BlockSize, array, 0, BlockSize);
			currentBlockIndex++;
			return array;
		}

		private bool ReadRecord()
		{
			bool debug2 = debug;
			if (inputStream == null)
			{
				throw new ApplicationException("no input stream stream defined");
			}
			currentBlockIndex = 0;
			int num = 0;
			int num2 = RecordSize;
			while (num2 > 0)
			{
				long num3 = inputStream.Read(recordBuffer, num, num2);
				if (num3 <= 0)
				{
					break;
				}
				num += (int)num3;
				num2 -= (int)num3;
				if (num3 != RecordSize)
				{
					bool debug3 = debug;
				}
			}
			currentRecordIndex++;
			return true;
		}

		public int GetCurrentBlockNum()
		{
			return currentBlockIndex;
		}

		public int GetCurrentRecordNum()
		{
			return currentRecordIndex;
		}

		public void WriteBlock(byte[] block)
		{
			bool debug2 = debug;
			if (outputStream == null)
			{
				throw new ApplicationException("TarBuffer.WriteBlock - no output stream defined");
			}
			if (block.Length != BlockSize)
			{
				throw new ApplicationException("TarBuffer.WriteBlock - block to write has length '" + block.Length + "' which is not the block size of '" + BlockSize + "'");
			}
			if (currentBlockIndex >= BlockFactor)
			{
				WriteRecord();
			}
			Array.Copy(block, 0, recordBuffer, currentBlockIndex * BlockSize, BlockSize);
			currentBlockIndex++;
		}

		public void WriteBlock(byte[] buf, int offset)
		{
			bool debug2 = debug;
			if (outputStream == null)
			{
				throw new ApplicationException("TarBuffer.WriteBlock - no output stream stream defined");
			}
			if (offset + BlockSize > buf.Length)
			{
				throw new ApplicationException("TarBuffer.WriteBlock - record has length '" + buf.Length + "' with offset '" + offset + "' which is less than the record size of '" + recordSize + "'");
			}
			if (currentBlockIndex >= BlockFactor)
			{
				WriteRecord();
			}
			Array.Copy(buf, offset, recordBuffer, currentBlockIndex * BlockSize, BlockSize);
			currentBlockIndex++;
		}

		private void WriteRecord()
		{
			bool debug2 = debug;
			if (outputStream == null)
			{
				throw new ApplicationException("TarBuffer.WriteRecord no output stream defined");
			}
			outputStream.Write(recordBuffer, 0, RecordSize);
			outputStream.Flush();
			currentBlockIndex = 0;
			currentRecordIndex++;
		}

		private void Flush()
		{
			bool debug2 = debug;
			if (outputStream == null)
			{
				throw new ApplicationException("TarBuffer.Flush no output stream defined");
			}
			if (currentBlockIndex > 0)
			{
				WriteRecord();
			}
			outputStream.Flush();
		}

		public void Close()
		{
			bool debug2 = debug;
			if (outputStream != null)
			{
				Flush();
				outputStream.Close();
				outputStream = null;
			}
			else if (inputStream != null)
			{
				inputStream.Close();
				inputStream = null;
			}
		}
	}
}
