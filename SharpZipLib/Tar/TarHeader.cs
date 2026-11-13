using System;
using System.Text;

namespace ICSharpCode.SharpZipLib.Tar
{
	public class TarHeader : ICloneable
	{
		public const byte LF_OLDNORM = 0;

		public const byte LF_NORMAL = 48;

		public const byte LF_LINK = 49;

		public const byte LF_SYMLINK = 50;

		public const byte LF_CHR = 51;

		public const byte LF_BLK = 52;

		public const byte LF_DIR = 53;

		public const byte LF_FIFO = 54;

		public const byte LF_CONTIG = 55;

		public const byte LF_GHDR = 103;

		public const byte LF_ACL = 65;

		public const byte LF_GNU_DUMPDIR = 68;

		public const byte LF_EXTATTR = 69;

		public const byte LF_META = 73;

		public const byte LF_GNU_LONGLINK = 75;

		public const byte LF_GNU_LONGNAME = 76;

		public const byte LF_GNU_MULTIVOL = 77;

		public const byte LF_GNU_NAMES = 78;

		public const byte LF_GNU_SPARSE = 83;

		public const byte LF_GNU_VOLHDR = 86;

		public static readonly int NAMELEN = 100;

		public static readonly int MODELEN = 8;

		public static readonly int UIDLEN = 8;

		public static readonly int GIDLEN = 8;

		public static readonly int CHKSUMLEN = 8;

		public static readonly int SIZELEN = 12;

		public static readonly int MAGICLEN = 6;

		public static readonly int VERSIONLEN = 2;

		public static readonly int MODTIMELEN = 12;

		public static readonly int UNAMELEN = 32;

		public static readonly int GNAMELEN = 32;

		public static readonly int DEVLEN = 8;

		public static readonly byte LF_XHDR = 120;

		public static readonly string TMAGIC = "ustar ";

		public static readonly string GNU_TMAGIC = "ustar  ";

		public StringBuilder name;

		public int mode;

		public int userId;

		public int groupId;

		public long size;

		public DateTime modTime;

		public int checkSum;

		public byte typeFlag;

		public StringBuilder linkName;

		public StringBuilder magic;

		public StringBuilder version;

		public StringBuilder userName;

		public StringBuilder groupName;

		public int devMajor;

		public int devMinor;

		private static readonly long timeConversionFactor = 10000000L;

		private static readonly DateTime datetTime1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		public TarHeader()
		{
			magic = new StringBuilder(TMAGIC);
			version = new StringBuilder(" ");
			name = new StringBuilder();
			linkName = new StringBuilder();
			string text = Environment.UserName;
			if (text.Length > 31)
			{
				text = text.Substring(0, 31);
			}
			userId = 0;
			groupId = 0;
			userName = new StringBuilder(text);
			groupName = new StringBuilder("None");
			size = 0L;
		}

		public object Clone()
		{
			TarHeader tarHeader = new TarHeader();
			tarHeader.name = ((name == null) ? null : new StringBuilder(name.ToString()));
			tarHeader.mode = mode;
			tarHeader.userId = userId;
			tarHeader.groupId = groupId;
			tarHeader.size = size;
			tarHeader.modTime = modTime;
			tarHeader.checkSum = checkSum;
			tarHeader.typeFlag = typeFlag;
			tarHeader.linkName = ((linkName == null) ? null : new StringBuilder(linkName.ToString()));
			tarHeader.magic = ((magic == null) ? null : new StringBuilder(magic.ToString()));
			tarHeader.version = ((version == null) ? null : new StringBuilder(version.ToString()));
			tarHeader.userName = ((userName == null) ? null : new StringBuilder(userName.ToString()));
			tarHeader.groupName = ((groupName == null) ? null : new StringBuilder(groupName.ToString()));
			tarHeader.devMajor = devMajor;
			tarHeader.devMinor = devMinor;
			return tarHeader;
		}

		public string GetName()
		{
			return name.ToString();
		}

		public static long ParseOctal(byte[] header, int offset, int length)
		{
			long num = 0L;
			bool flag = true;
			int num2 = offset + length;
			for (int i = offset; i < num2 && header[i] != 0; i++)
			{
				if (header[i] == 32 || header[i] == 48)
				{
					if (flag)
					{
						continue;
					}
					if (header[i] == 32)
					{
						break;
					}
				}
				flag = false;
				num = (num << 3) + (header[i] - 48);
			}
			return num;
		}

		public static StringBuilder ParseName(byte[] header, int offset, int length)
		{
			StringBuilder stringBuilder = new StringBuilder(length);
			for (int i = offset; i < offset + length && header[i] != 0; i++)
			{
				stringBuilder.Append((char)header[i]);
			}
			return stringBuilder;
		}

		public static int GetNameBytes(StringBuilder name, int nameOffset, byte[] buf, int bufferOffset, int length)
		{
			int i;
			for (i = 0; i < length && nameOffset + i < name.Length; i++)
			{
				buf[bufferOffset + i] = (byte)name[nameOffset + i];
			}
			for (; i < length; i++)
			{
				buf[bufferOffset + i] = 0;
			}
			return bufferOffset + length;
		}

		public static int GetNameBytes(StringBuilder name, byte[] buf, int offset, int length)
		{
			return GetNameBytes(name, 0, buf, offset, length);
		}

		public static int GetOctalBytes(long val, byte[] buf, int offset, int length)
		{
			int num = length - 1;
			buf[offset + num] = 0;
			num--;
			if (val > 0)
			{
				long num2 = val;
				while (num >= 0 && num2 > 0)
				{
					buf[offset + num] = (byte)(48 + (byte)(num2 & 7));
					num2 >>= 3;
					num--;
				}
			}
			while (num >= 0)
			{
				buf[offset + num] = 48;
				num--;
			}
			return offset + length;
		}

		public static int GetLongOctalBytes(long val, byte[] buf, int offset, int length)
		{
			return GetOctalBytes(val, buf, offset, length);
		}

		private static int GetCheckSumOctalBytes(long val, byte[] buf, int offset, int length)
		{
			GetOctalBytes(val, buf, offset, length - 1);
			return offset + length;
		}

		private static long ComputeCheckSum(byte[] buf)
		{
			long num = 0L;
			for (int i = 0; i < buf.Length; i++)
			{
				num += buf[i];
			}
			return num;
		}

		private static int GetCTime(DateTime dateTime)
		{
			return (int)((dateTime.Ticks - datetTime1970.Ticks) / timeConversionFactor);
		}

		private static DateTime GetDateTimeFromCTime(long ticks)
		{
			return new DateTime(datetTime1970.Ticks + ticks * timeConversionFactor);
		}

		public void ParseBuffer(byte[] header)
		{
			int num = 0;
			name = ParseName(header, num, NAMELEN);
			num += NAMELEN;
			mode = (int)ParseOctal(header, num, MODELEN);
			num += MODELEN;
			userId = (int)ParseOctal(header, num, UIDLEN);
			num += UIDLEN;
			groupId = (int)ParseOctal(header, num, GIDLEN);
			num += GIDLEN;
			size = ParseOctal(header, num, SIZELEN);
			num += SIZELEN;
			modTime = GetDateTimeFromCTime(ParseOctal(header, num, MODTIMELEN));
			num += MODTIMELEN;
			checkSum = (int)ParseOctal(header, num, CHKSUMLEN);
			num += CHKSUMLEN;
			typeFlag = header[num++];
			linkName = ParseName(header, num, NAMELEN);
			num += NAMELEN;
			magic = ParseName(header, num, MAGICLEN);
			num += MAGICLEN;
			version = ParseName(header, num, VERSIONLEN);
			num += VERSIONLEN;
			userName = ParseName(header, num, UNAMELEN);
			num += UNAMELEN;
			groupName = ParseName(header, num, GNAMELEN);
			num += GNAMELEN;
			devMajor = (int)ParseOctal(header, num, DEVLEN);
			num += DEVLEN;
			devMinor = (int)ParseOctal(header, num, DEVLEN);
		}

		public void WriteHeader(byte[] outbuf)
		{
			int offset = 0;
			offset = GetNameBytes(name, outbuf, offset, NAMELEN);
			offset = GetOctalBytes(mode, outbuf, offset, MODELEN);
			offset = GetOctalBytes(userId, outbuf, offset, UIDLEN);
			offset = GetOctalBytes(groupId, outbuf, offset, GIDLEN);
			long val = size;
			offset = GetLongOctalBytes(val, outbuf, offset, SIZELEN);
			offset = GetLongOctalBytes(GetCTime(modTime), outbuf, offset, MODTIMELEN);
			int offset2 = offset;
			for (int i = 0; i < CHKSUMLEN; i++)
			{
				outbuf[offset++] = 32;
			}
			outbuf[offset++] = typeFlag;
			offset = GetNameBytes(linkName, outbuf, offset, NAMELEN);
			offset = GetNameBytes(magic, outbuf, offset, MAGICLEN);
			offset = GetNameBytes(version, outbuf, offset, VERSIONLEN);
			offset = GetNameBytes(userName, outbuf, offset, UNAMELEN);
			offset = GetNameBytes(groupName, outbuf, offset, GNAMELEN);
			if (typeFlag == 51 || typeFlag == 52)
			{
				offset = GetOctalBytes(devMajor, outbuf, offset, DEVLEN);
				offset = GetOctalBytes(devMinor, outbuf, offset, DEVLEN);
			}
			while (offset < outbuf.Length)
			{
				outbuf[offset++] = 0;
			}
			long val2 = ComputeCheckSum(outbuf);
			GetCheckSumOctalBytes(val2, outbuf, offset2, CHKSUMLEN);
		}
	}
}
