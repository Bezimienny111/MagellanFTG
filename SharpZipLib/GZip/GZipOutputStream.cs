using System;
using System.IO;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace ICSharpCode.SharpZipLib.GZip
{
	public class GZipOutputStream : DeflaterOutputStream
	{
		protected Crc32 crc = new Crc32();

		public GZipOutputStream(Stream baseOutputStream)
			: this(baseOutputStream, 4096)
		{
		}

		public GZipOutputStream(Stream baseOutputStream, int size)
			: base(baseOutputStream, new Deflater(Deflater.DEFAULT_COMPRESSION, true), size)
		{
			WriteHeader();
		}

		private void WriteHeader()
		{
			int num = (int)(DateTime.Now.Ticks / 10000);
			byte[] array = new byte[10]
			{
				(byte)(GZipConstants.GZIP_MAGIC >> 8),
				(byte)GZipConstants.GZIP_MAGIC,
				(byte)Deflater.DEFLATED,
				0,
				(byte)num,
				(byte)(num >> 8),
				(byte)(num >> 16),
				(byte)(num >> 24),
				0,
				255
			};
			baseOutputStream.Write(array, 0, array.Length);
		}

		public override void Write(byte[] buf, int off, int len)
		{
			crc.Update(buf, off, len);
			base.Write(buf, off, len);
		}

		public override void Close()
		{
			Finish();
			baseOutputStream.Close();
		}

		public override void Finish()
		{
			base.Finish();
			int totalIn = def.TotalIn;
			int num = (int)(crc.Value & 0xFFFFFFFFu);
			byte[] array = new byte[8]
			{
				(byte)num,
				(byte)(num >> 8),
				(byte)(num >> 16),
				(byte)(num >> 24),
				(byte)totalIn,
				(byte)(totalIn >> 8),
				(byte)(totalIn >> 16),
				(byte)(totalIn >> 24)
			};
			baseOutputStream.Write(array, 0, array.Length);
		}
	}
}
