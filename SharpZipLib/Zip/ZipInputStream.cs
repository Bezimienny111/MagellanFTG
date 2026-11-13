using System;
using System.IO;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace ICSharpCode.SharpZipLib.Zip
{
	public class ZipInputStream : InflaterInputStream
	{
		private Crc32 crc = new Crc32();

		private ZipEntry entry = null;

		private long size;

		private int method;

		private int flags;

		private long avail;

		private string password = null;

		public string Password
		{
			get
			{
				return password;
			}
			set
			{
				password = value;
			}
		}

		public bool CanDecompressEntry
		{
			get
			{
				if (entry != null)
				{
					return entry.Version <= 20;
				}
				return false;
			}
		}

		public override int Available
		{
			get
			{
				if (entry == null)
				{
					return 0;
				}
				return 1;
			}
		}

		public ZipInputStream(Stream baseInputStream)
			: base(baseInputStream, new Inflater(true))
		{
		}

		private void FillBuf(int size)
		{
			avail = (len = baseInputStream.Read(buf, 0, Math.Min(buf.Length, size)));
		}

		private int ReadBuf(byte[] outBuf, int offset, int length)
		{
			if (avail <= 0)
			{
				FillBuf(length);
				if (avail <= 0)
				{
					return 0;
				}
			}
			if (length > avail)
			{
				length = (int)avail;
			}
			Array.Copy(buf, len - (int)avail, outBuf, offset, length);
			avail -= length;
			return length;
		}

		private void ReadFully(byte[] outBuf)
		{
			int num = 0;
			int num2 = outBuf.Length;
			while (num2 > 0)
			{
				int num3 = ReadBuf(outBuf, num, num2);
				if (num3 <= 0)
				{
					throw new ZipException("Unexpected EOF");
				}
				num += num3;
				num2 -= num3;
			}
		}

		private int ReadLeByte()
		{
			if (avail <= 0)
			{
				FillBuf(1);
				if (avail <= 0)
				{
					throw new ZipException("EOF in header");
				}
			}
			return buf[len - avail--] & 0xFF;
		}

		private int ReadLeShort()
		{
			return ReadLeByte() | (ReadLeByte() << 8);
		}

		private int ReadLeInt()
		{
			return ReadLeShort() | (ReadLeShort() << 16);
		}

		private long ReadLeLong()
		{
			return ReadLeInt() | ReadLeInt();
		}

		public ZipEntry GetNextEntry()
		{
			if (crc == null)
			{
				throw new InvalidOperationException("Closed.");
			}
			if (entry != null)
			{
				CloseEntry();
			}
			if (cryptbuffer != null)
			{
				if (avail == 0 && inf.RemainingInput != 0)
				{
					avail = inf.RemainingInput - 16;
					inf.Reset();
				}
				baseInputStream.Position -= len;
				baseInputStream.Read(buf, 0, len);
			}
			if (avail <= 0)
			{
				FillBuf(30);
			}
			int num = ReadLeInt();
			switch (num)
			{
			case 33639248:
			case 84233040:
			case 101010256:
			case 101075792:
				Close();
				return null;
			case 134695760:
			case 808471376:
				num = ReadLeInt();
				break;
			}
			if (num != 67324752)
			{
				throw new ZipException("Wrong Local header signature: 0x" + string.Format("{0:X}", num));
			}
			short num2 = (short)ReadLeShort();
			flags = ReadLeShort();
			method = ReadLeShort();
			uint num3 = (uint)ReadLeInt();
			int num4 = ReadLeInt();
			csize = ReadLeInt();
			size = ReadLeInt();
			int num5 = ReadLeShort();
			int num6 = ReadLeShort();
			bool flag = (flags & 1) == 1;
			byte[] array = new byte[num5];
			ReadFully(array);
			string name = ZipConstants.ConvertToString(array);
			entry = new ZipEntry(name, num2);
			entry.Flags = flags;
			if (method == 0 && ((!flag && csize != size) || (flag && csize - 12 != size)))
			{
				throw new ZipException("Stored, but compressed != uncompressed");
			}
			if (method != 0 && method != 8)
			{
				throw new ZipException("unknown compression method " + method);
			}
			entry.CompressionMethod = (CompressionMethod)method;
			if ((flags & 8) == 0)
			{
				entry.Crc = num4 & 0xFFFFFFFFu;
				entry.Size = size & 0xFFFFFFFFu;
				entry.CompressedSize = csize & 0xFFFFFFFFu;
				BufferReadSize = 0;
			}
			else
			{
				if (flag)
				{
					BufferReadSize = 1;
				}
				else
				{
					BufferReadSize = 0;
				}
				if (num4 != 0)
				{
					entry.Crc = num4 & 0xFFFFFFFFu;
				}
				if (size != 0)
				{
					entry.Size = size & 0xFFFFFFFFu;
				}
				if (csize != 0)
				{
					entry.CompressedSize = csize & 0xFFFFFFFFu;
				}
			}
			entry.DosTime = num3;
			if (num6 > 0)
			{
				byte[] array2 = new byte[num6];
				ReadFully(array2);
				entry.ExtraData = array2;
			}
			if (num2 > 20)
			{
				throw new ZipException("Libray cannot extract this entry version required (" + num2 + ")");
			}
			if (flag)
			{
				if (password == null)
				{
					throw new ZipException("No password set.");
				}
				InitializePassword(password);
				cryptbuffer = new byte[12];
				ReadFully(cryptbuffer);
				DecryptBlock(cryptbuffer, 0, cryptbuffer.Length);
				if ((flags & 8) == 0)
				{
					if (cryptbuffer[11] != (byte)(num4 >> 24))
					{
						throw new ZipException("Invalid password");
					}
				}
				else if (cryptbuffer[11] != (byte)((num3 >> 8) & 0xFF))
				{
					throw new ZipException("Invalid password");
				}
				if (csize >= 12)
				{
					csize -= 12L;
				}
			}
			else
			{
				cryptbuffer = null;
			}
			if (method == 8 && avail > 0)
			{
				Array.Copy(buf, len - (int)avail, buf, 0, (int)avail);
				len = (int)avail;
				avail = 0L;
				if (flag)
				{
					DecryptBlock(buf, 0, Math.Min((int)csize, len));
				}
				inf.SetInput(buf, 0, len);
			}
			return entry;
		}

		private void ReadDataDescriptor()
		{
			if (ReadLeInt() != 134695760)
			{
				throw new ZipException("Data descriptor signature not found");
			}
			entry.Crc = ReadLeInt() & 0xFFFFFFFFu;
			csize = ReadLeInt();
			size = ReadLeInt();
			entry.Size = size & 0xFFFFFFFFu;
			entry.CompressedSize = csize & 0xFFFFFFFFu;
		}

		public void CloseEntry()
		{
			if (crc == null)
			{
				throw new InvalidOperationException("Closed.");
			}
			if (entry == null)
			{
				return;
			}
			if (method == 8)
			{
				if ((flags & 8) != 0)
				{
					byte[] array = new byte[2048];
					while (Read(array, 0, array.Length) > 0)
					{
					}
					return;
				}
				csize -= inf.TotalIn;
				avail = inf.RemainingInput;
			}
			if (avail > csize && csize >= 0)
			{
				avail -= csize;
			}
			else
			{
				csize -= avail;
				avail = 0L;
				while (csize != 0)
				{
					int num = (int)Skip(csize & 0xFFFFFFFFu);
					if (num <= 0)
					{
						throw new ZipException("Zip archive ends early.");
					}
					csize -= num;
				}
			}
			size = 0L;
			crc.Reset();
			if (method == 8)
			{
				inf.Reset();
			}
			entry = null;
		}

		public override int ReadByte()
		{
			byte[] array = new byte[1];
			if (Read(array, 0, 1) <= 0)
			{
				return -1;
			}
			return array[0] & 0xFF;
		}

		public override int Read(byte[] b, int off, int len)
		{
			if (crc == null)
			{
				throw new InvalidOperationException("Closed.");
			}
			if (entry == null)
			{
				return 0;
			}
			bool flag = false;
			switch (method)
			{
			case 8:
				len = base.Read(b, off, len);
				if (len <= 0)
				{
					if (!inf.IsFinished)
					{
						throw new ZipException("Inflater not finished!?");
					}
					avail = inf.RemainingInput;
					if ((flags & 8) == 0 && (inf.TotalIn != csize || inf.TotalOut != size))
					{
						throw new ZipException("size mismatch: " + csize + ";" + size + " <-> " + inf.TotalIn + ";" + inf.TotalOut);
					}
					inf.Reset();
					flag = true;
				}
				break;
			case 0:
				if (len > csize && csize >= 0)
				{
					len = (int)csize;
				}
				len = ReadBuf(b, off, len);
				if (len > 0)
				{
					csize -= len;
					size -= len;
				}
				if (csize == 0)
				{
					flag = true;
				}
				else if (len < 0)
				{
					throw new ZipException("EOF in stored block");
				}
				if (cryptbuffer != null)
				{
					DecryptBlock(b, off, len);
				}
				break;
			}
			if (len > 0)
			{
				crc.Update(b, off, len);
			}
			if (flag)
			{
				StopDecrypting();
				if ((flags & 8) != 0)
				{
					ReadDataDescriptor();
				}
				if ((crc.Value & 0xFFFFFFFFu) != entry.Crc && entry.Crc != -1)
				{
					throw new ZipException("CRC mismatch");
				}
				crc.Reset();
				entry = null;
			}
			return len;
		}

		public override void Close()
		{
			base.Close();
			crc = null;
			entry = null;
		}
	}
}
