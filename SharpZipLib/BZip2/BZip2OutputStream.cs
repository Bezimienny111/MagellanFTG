using System;
using System.IO;
using ICSharpCode.SharpZipLib.Checksums;

namespace ICSharpCode.SharpZipLib.BZip2
{
	public class BZip2OutputStream : Stream
	{
		private class StackElem
		{
			public int ll;

			public int hh;

			public int dd;
		}

		private static readonly int SETMASK = 2097152;

		private static readonly int CLEARMASK = ~SETMASK;

		private static readonly int GREATER_ICOST = 15;

		private static readonly int LESSER_ICOST = 0;

		private static readonly int SMALL_THRESH = 20;

		private static readonly int DEPTH_THRESH = 10;

		private static readonly int QSORT_STACK_SIZE = 1000;

		private int last;

		private int origPtr;

		private int blockSize100k;

		private bool blockRandomised;

		private int bytesOut;

		private int bsBuff;

		private int bsLive;

		private IChecksum mCrc = new StrangeCRC();

		private bool[] inUse = new bool[256];

		private int nInUse;

		private char[] seqToUnseq = new char[256];

		private char[] unseqToSeq = new char[256];

		private char[] selector = new char[BZip2Constants.MAX_SELECTORS];

		private char[] selectorMtf = new char[BZip2Constants.MAX_SELECTORS];

		private byte[] block;

		private int[] quadrant;

		private int[] zptr;

		private short[] szptr;

		private int[] ftab;

		private int nMTF;

		private int[] mtfFreq = new int[BZip2Constants.MAX_ALPHA_SIZE];

		private int workFactor;

		private int workDone;

		private int workLimit;

		private bool firstAttempt;

		private int nBlocksRandomised;

		private int currentChar = -1;

		private int runLength = 0;

		private bool closed = false;

		private uint blockCRC;

		private uint combinedCRC;

		private int allowableBlockSize;

		private Stream baseStream;

		private readonly int[] incs = new int[14]
		{
			1, 4, 13, 40, 121, 364, 1093, 3280, 9841, 29524,
			88573, 265720, 797161, 2391484
		};

		public override bool CanRead
		{
			get
			{
				return false;
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
				return baseStream.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				return baseStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return baseStream.Position;
			}
			set
			{
				throw new NotSupportedException("BZip2OutputStream position cannot be set");
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("BZip2OutputStream Seek not supported");
		}

		public override void SetLength(long val)
		{
			throw new NotSupportedException("BZip2OutputStream SetLength not supported");
		}

		public override int ReadByte()
		{
			throw new NotSupportedException("BZip2OutputStream ReadByte not supported");
		}

		public override int Read(byte[] b, int off, int len)
		{
			throw new NotSupportedException("BZip2OutputStream Read not supported");
		}

		public override void Write(byte[] buf, int off, int len)
		{
			for (int i = 0; i < len; i++)
			{
				WriteByte(buf[off + i]);
			}
		}

		private static void Panic()
		{
			throw new ApplicationException("BZip2 output stream panic");
		}

		private void MakeMaps()
		{
			nInUse = 0;
			for (int i = 0; i < 256; i++)
			{
				if (inUse[i])
				{
					seqToUnseq[nInUse] = (char)i;
					unseqToSeq[i] = (char)nInUse;
					nInUse++;
				}
			}
		}

		private static void HbMakeCodeLengths(char[] len, int[] freq, int alphaSize, int maxLen)
		{
			int[] array = new int[BZip2Constants.MAX_ALPHA_SIZE + 2];
			int[] array2 = new int[BZip2Constants.MAX_ALPHA_SIZE * 2];
			int[] array3 = new int[BZip2Constants.MAX_ALPHA_SIZE * 2];
			for (int i = 0; i < alphaSize; i++)
			{
				array2[i + 1] = ((freq[i] == 0) ? 1 : freq[i]) << 8;
			}
			while (true)
			{
				int num = alphaSize;
				int num2 = 0;
				array[0] = 0;
				array2[0] = 0;
				array3[0] = -2;
				for (int j = 1; j <= alphaSize; j++)
				{
					array3[j] = -1;
					num2++;
					array[num2] = j;
					int num3 = num2;
					int num4 = array[num3];
					while (array2[num4] < array2[array[num3 >> 1]])
					{
						array[num3] = array[num3 >> 1];
						num3 >>= 1;
					}
					array[num3] = num4;
				}
				if (num2 >= BZip2Constants.MAX_ALPHA_SIZE + 2)
				{
					Panic();
				}
				while (num2 > 1)
				{
					int num5 = array[1];
					array[1] = array[num2];
					num2--;
					int num6 = 1;
					int num7 = 0;
					int num8 = array[num6];
					while (true)
					{
						num7 = num6 << 1;
						if (num7 > num2)
						{
							break;
						}
						if (num7 < num2 && array2[array[num7 + 1]] < array2[array[num7]])
						{
							num7++;
						}
						if (array2[num8] < array2[array[num7]])
						{
							break;
						}
						array[num6] = array[num7];
						num6 = num7;
					}
					array[num6] = num8;
					int num9 = array[1];
					array[1] = array[num2];
					num2--;
					num6 = 1;
					num7 = 0;
					num8 = array[num6];
					while (true)
					{
						num7 = num6 << 1;
						if (num7 > num2)
						{
							break;
						}
						if (num7 < num2 && array2[array[num7 + 1]] < array2[array[num7]])
						{
							num7++;
						}
						if (array2[num8] < array2[array[num7]])
						{
							break;
						}
						array[num6] = array[num7];
						num6 = num7;
					}
					array[num6] = num8;
					num++;
					array3[num5] = (array3[num9] = num);
					array2[num] = (int)((array2[num5] & 0xFFFFFF00u) + (array2[num9] & 0xFFFFFF00u)) | (1 + (((array2[num5] & 0xFF) > (array2[num9] & 0xFF)) ? (array2[num5] & 0xFF) : (array2[num9] & 0xFF)));
					array3[num] = -1;
					num2++;
					array[num2] = num;
					num6 = num2;
					num8 = array[num6];
					while (array2[num8] < array2[array[num6 >> 1]])
					{
						array[num6] = array[num6 >> 1];
						num6 >>= 1;
					}
					array[num6] = num8;
				}
				if (num >= BZip2Constants.MAX_ALPHA_SIZE * 2)
				{
					Panic();
				}
				bool flag = false;
				for (int k = 1; k <= alphaSize; k++)
				{
					int num10 = 0;
					int num11 = k;
					while (array3[num11] >= 0)
					{
						num11 = array3[num11];
						num10++;
					}
					len[k - 1] = (char)num10;
					if (num10 > maxLen)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					break;
				}
				for (int l = 1; l < alphaSize; l++)
				{
					int num10 = array2[l] >> 8;
					num10 = 1 + num10 / 2;
					array2[l] = num10 << 8;
				}
			}
		}

		public BZip2OutputStream(Stream inStream)
			: this(inStream, 9)
		{
		}

		public BZip2OutputStream(Stream inStream, int inBlockSize)
		{
			block = null;
			quadrant = null;
			zptr = null;
			ftab = null;
			BsSetStream(inStream);
			workFactor = 50;
			if (inBlockSize > 9)
			{
				inBlockSize = 9;
			}
			if (inBlockSize < 1)
			{
				inBlockSize = 1;
			}
			blockSize100k = inBlockSize;
			AllocateCompressStructures();
			Initialize();
			InitBlock();
		}

		public override void WriteByte(byte bv)
		{
			int num = (256 + bv) % 256;
			if (currentChar != -1)
			{
				if (currentChar == num)
				{
					runLength++;
					if (runLength > 254)
					{
						WriteRun();
						currentChar = -1;
						runLength = 0;
					}
				}
				else
				{
					WriteRun();
					runLength = 1;
					currentChar = num;
				}
			}
			else
			{
				currentChar = num;
				runLength++;
			}
		}

		private void WriteRun()
		{
			if (last < allowableBlockSize)
			{
				inUse[currentChar] = true;
				for (int i = 0; i < runLength; i++)
				{
					mCrc.Update(currentChar);
				}
				switch (runLength)
				{
				case 1:
					last++;
					block[last + 1] = (byte)currentChar;
					break;
				case 2:
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)currentChar;
					break;
				case 3:
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)currentChar;
					break;
				default:
					inUse[runLength - 4] = true;
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)currentChar;
					last++;
					block[last + 1] = (byte)(runLength - 4);
					break;
				}
			}
			else
			{
				EndBlock();
				InitBlock();
				WriteRun();
			}
		}

		public void Finalize()
		{
			Close();
		}

		public override void Close()
		{
			if (!closed)
			{
				if (runLength > 0)
				{
					WriteRun();
				}
				currentChar = -1;
				EndBlock();
				EndCompression();
				closed = true;
				Flush();
				baseStream.Close();
			}
		}

		public override void Flush()
		{
			baseStream.Flush();
		}

		private void Initialize()
		{
			bytesOut = 0;
			nBlocksRandomised = 0;
			BsPutUChar(66);
			BsPutUChar(90);
			BsPutUChar(104);
			BsPutUChar(48 + blockSize100k);
			combinedCRC = 0u;
		}

		private void InitBlock()
		{
			mCrc.Reset();
			last = -1;
			for (int i = 0; i < 256; i++)
			{
				inUse[i] = false;
			}
			allowableBlockSize = BZip2Constants.baseBlockSize * blockSize100k - 20;
		}

		private void EndBlock()
		{
			if (last >= 0)
			{
				blockCRC = (uint)mCrc.Value;
				combinedCRC = (combinedCRC << 1) | (combinedCRC >> 31);
				combinedCRC ^= blockCRC;
				DoReversibleTransformation();
				BsPutUChar(49);
				BsPutUChar(65);
				BsPutUChar(89);
				BsPutUChar(38);
				BsPutUChar(83);
				BsPutUChar(89);
				BsPutint((int)blockCRC);
				if (blockRandomised)
				{
					BsW(1, 1);
					nBlocksRandomised++;
				}
				else
				{
					BsW(1, 0);
				}
				MoveToFrontCodeAndSend();
			}
		}

		private void EndCompression()
		{
			BsPutUChar(23);
			BsPutUChar(114);
			BsPutUChar(69);
			BsPutUChar(56);
			BsPutUChar(80);
			BsPutUChar(144);
			BsPutint((int)combinedCRC);
			BsFinishedWithStream();
		}

		private void HbAssignCodes(int[] code, char[] length, int minLen, int maxLen, int alphaSize)
		{
			int num = 0;
			for (int i = minLen; i <= maxLen; i++)
			{
				for (int j = 0; j < alphaSize; j++)
				{
					if (length[j] == i)
					{
						code[j] = num;
						num++;
					}
				}
				num <<= 1;
			}
		}

		private void BsSetStream(Stream f)
		{
			baseStream = f;
			bsLive = 0;
			bsBuff = 0;
			bytesOut = 0;
		}

		private void BsFinishedWithStream()
		{
			while (bsLive > 0)
			{
				int num = bsBuff >> 24;
				baseStream.WriteByte((byte)num);
				bsBuff <<= 8;
				bsLive -= 8;
				bytesOut++;
			}
		}

		private void BsW(int n, int v)
		{
			while (bsLive >= 8)
			{
				int num = bsBuff >> 24;
				baseStream.WriteByte((byte)num);
				bsBuff <<= 8;
				bsLive -= 8;
				bytesOut++;
			}
			bsBuff |= v << 32 - bsLive - n;
			bsLive += n;
		}

		private void BsPutUChar(int c)
		{
			BsW(8, c);
		}

		private void BsPutint(int u)
		{
			BsW(8, (u >> 24) & 0xFF);
			BsW(8, (u >> 16) & 0xFF);
			BsW(8, (u >> 8) & 0xFF);
			BsW(8, u & 0xFF);
		}

		private void BsPutIntVS(int numBits, int c)
		{
			BsW(numBits, c);
		}

		private unsafe void SendMTFValues()
		{
			char[][] array = new char[BZip2Constants.N_GROUPS][];
			for (int i = 0; i < BZip2Constants.N_GROUPS; i++)
			{
				array[i] = new char[BZip2Constants.MAX_ALPHA_SIZE];
			}
			int num = 0;
			int num2 = nInUse + 2;
			for (int j = 0; j < BZip2Constants.N_GROUPS; j++)
			{
				for (int k = 0; k < num2; k++)
				{
					array[j][k] = (char)GREATER_ICOST;
				}
			}
			if (nMTF <= 0)
			{
				Panic();
			}
			int num3;
			if (nMTF < 200)
			{
				num3 = 2;
			}
			else if (nMTF < 600)
			{
				num3 = 3;
			}
			else if (nMTF < 1200)
			{
				num3 = 4;
			}
			else
			{
				num3 = ((nMTF >= 2400) ? 6 : 5);
			}
			int num4 = num3;
			int num5 = nMTF;
			int num6 = 0;
			while (num4 > 0)
			{
				int num7 = num5 / num4;
				int l = 0;
				int num8;
				for (num8 = num6 - 1; l < num7; l += mtfFreq[num8])
				{
					if (num8 >= num2 - 1)
					{
						break;
					}
					num8++;
				}
				if (num8 > num6 && num4 != num3 && num4 != 1 && (num3 - num4) % 2 == 1)
				{
					l -= mtfFreq[num8];
					num8--;
				}
				for (int m = 0; m < num2; m++)
				{
					if (m >= num6 && m <= num8)
					{
						array[num4 - 1][m] = (char)LESSER_ICOST;
					}
					else
					{
						array[num4 - 1][m] = (char)GREATER_ICOST;
					}
				}
				num4--;
				num6 = num8 + 1;
				num5 -= l;
			}
			int[][] array2 = new int[BZip2Constants.N_GROUPS][];
			for (int n = 0; n < BZip2Constants.N_GROUPS; n++)
			{
				array2[n] = new int[BZip2Constants.MAX_ALPHA_SIZE];
			}
			int[] array3 = new int[BZip2Constants.N_GROUPS];
			short[] array4 = new short[BZip2Constants.N_GROUPS];
			for (int num9 = 0; num9 < BZip2Constants.N_ITERS; num9++)
			{
				for (int num10 = 0; num10 < num3; num10++)
				{
					array3[num10] = 0;
				}
				for (int num11 = 0; num11 < num3; num11++)
				{
					for (int num12 = 0; num12 < num2; num12++)
					{
						array2[num11][num12] = 0;
					}
				}
				num = 0;
				int num13 = 0;
				num6 = 0;
				while (num6 < nMTF)
				{
					int num8 = num6 + BZip2Constants.G_SIZE - 1;
					if (num8 >= nMTF)
					{
						num8 = nMTF - 1;
					}
					for (int num14 = 0; num14 < num3; num14++)
					{
						array4[num14] = 0;
					}
					IntPtr intPtr;
					if (num3 == 6)
					{
						short num16;
						short num17;
						short num18;
						short num19;
						short num20;
						short num15 = (num16 = (num17 = (num18 = (num19 = (num20 = 0)))));
						for (int num21 = num6; num21 <= num8; num21++)
						{
							short num22 = szptr[num21];
							num15 += (short)array[0][num22];
							num16 += (short)array[1][num22];
							num17 += (short)array[2][num22];
							num18 += (short)array[3][num22];
							num19 += (short)array[4][num22];
							num20 += (short)array[5][num22];
						}
						array4[0] = num15;
						array4[1] = num16;
						array4[2] = num17;
						array4[3] = num18;
						array4[4] = num19;
						array4[5] = num20;
					}
					else
					{
						for (int num23 = num6; num23 <= num8; num23++)
						{
							short num24 = szptr[num23];
							for (int num25 = 0; num25 < num3; num25++)
							{
								short[] array6;
								short[] array5 = (array6 = array4);
								int num26 = num25;
								intPtr = (IntPtr)num26;
								array5[num26] = (short)(array6[(long)intPtr] + (short)array[num25][num24]);
							}
						}
					}
					int num27 = 999999999;
					int num28 = -1;
					for (int num29 = 0; num29 < num3; num29++)
					{
						if (array4[num29] < num27)
						{
							num27 = array4[num29];
							num28 = num29;
						}
					}
					num13 += num27;
					int[] array8;
					int[] array7 = (array8 = array3);
					int num30 = num28;
					intPtr = (IntPtr)num30;
					array7[num30] = array8[(long)intPtr] + 1;
					selector[num] = (char)num28;
					num++;
					for (int num31 = num6; num31 <= num8; num31++)
					{
						int[] array9 = (array8 = array2[num28]);
						short num32 = szptr[num31];
						intPtr = (IntPtr)(void*)num32;
						array9[num32] = array8[(long)intPtr] + 1;
					}
					num6 = num8 + 1;
				}
				for (int num33 = 0; num33 < num3; num33++)
				{
					HbMakeCodeLengths(array[num33], array2[num33], num2, 20);
				}
			}
			array2 = null;
			array3 = null;
			array4 = null;
			if (num3 >= 8)
			{
				Panic();
			}
			if (num >= 32768 || num > 2 + 900000 / BZip2Constants.G_SIZE)
			{
				Panic();
			}
			char[] array10 = new char[BZip2Constants.N_GROUPS];
			for (int num34 = 0; num34 < num3; num34++)
			{
				array10[num34] = (char)num34;
			}
			for (int num35 = 0; num35 < num; num35++)
			{
				char c = selector[num35];
				int num36 = 0;
				char c2 = array10[num36];
				while (c != c2)
				{
					num36++;
					char c3 = c2;
					c2 = array10[num36];
					array10[num36] = c3;
				}
				array10[0] = c2;
				selectorMtf[num35] = (char)num36;
			}
			int[][] array11 = new int[BZip2Constants.N_GROUPS][];
			for (int num37 = 0; num37 < BZip2Constants.N_GROUPS; num37++)
			{
				array11[num37] = new int[BZip2Constants.MAX_ALPHA_SIZE];
			}
			for (int num38 = 0; num38 < num3; num38++)
			{
				int num39 = 32;
				int num40 = 0;
				for (int num41 = 0; num41 < num2; num41++)
				{
					if (array[num38][num41] > num40)
					{
						num40 = array[num38][num41];
					}
					if (array[num38][num41] < num39)
					{
						num39 = array[num38][num41];
					}
				}
				if (num40 > 20)
				{
					Panic();
				}
				if (num39 < 1)
				{
					Panic();
				}
				HbAssignCodes(array11[num38], array[num38], num39, num40, num2);
			}
			bool[] array12 = new bool[16];
			for (int num42 = 0; num42 < 16; num42++)
			{
				array12[num42] = false;
				for (int num43 = 0; num43 < 16; num43++)
				{
					if (inUse[num42 * 16 + num43])
					{
						array12[num42] = true;
					}
				}
			}
			for (int num44 = 0; num44 < 16; num44++)
			{
				if (array12[num44])
				{
					BsW(1, 1);
				}
				else
				{
					BsW(1, 0);
				}
			}
			for (int num45 = 0; num45 < 16; num45++)
			{
				if (!array12[num45])
				{
					continue;
				}
				for (int num46 = 0; num46 < 16; num46++)
				{
					if (inUse[num45 * 16 + num46])
					{
						BsW(1, 1);
					}
					else
					{
						BsW(1, 0);
					}
				}
			}
			BsW(3, num3);
			BsW(15, num);
			for (int num47 = 0; num47 < num; num47++)
			{
				for (int num48 = 0; num48 < selectorMtf[num47]; num48++)
				{
					BsW(1, 1);
				}
				BsW(1, 0);
			}
			for (int num49 = 0; num49 < num3; num49++)
			{
				int num50 = array[num49][0];
				BsW(5, num50);
				for (int num51 = 0; num51 < num2; num51++)
				{
					for (; num50 < array[num49][num51]; num50++)
					{
						BsW(2, 2);
					}
					while (num50 > array[num49][num51])
					{
						BsW(2, 3);
						num50--;
					}
					BsW(1, 0);
				}
			}
			int num52 = 0;
			num6 = 0;
			while (num6 < nMTF)
			{
				int num8 = num6 + BZip2Constants.G_SIZE - 1;
				if (num8 >= nMTF)
				{
					num8 = nMTF - 1;
				}
				for (int num53 = num6; num53 <= num8; num53++)
				{
					BsW(array[(uint)selector[num52]][szptr[num53]], array11[(uint)selector[num52]][szptr[num53]]);
				}
				num6 = num8 + 1;
				num52++;
			}
			if (num52 != num)
			{
				Panic();
			}
		}

		private void MoveToFrontCodeAndSend()
		{
			BsPutIntVS(24, origPtr);
			GenerateMTFValues();
			SendMTFValues();
		}

		private void SimpleSort(int lo, int hi, int d)
		{
			int num = hi - lo + 1;
			if (num < 2)
			{
				return;
			}
			int i;
			for (i = 0; incs[i] < num; i++)
			{
			}
			for (i--; i >= 0; i--)
			{
				int num2 = incs[i];
				int num3 = lo + num2;
				while (num3 <= hi)
				{
					int num4 = zptr[num3];
					int num5 = num3;
					while (FullGtU(zptr[num5 - num2] + d, num4 + d))
					{
						zptr[num5] = zptr[num5 - num2];
						num5 -= num2;
						if (num5 <= lo + num2 - 1)
						{
							break;
						}
					}
					zptr[num5] = num4;
					num3++;
					if (num3 > hi)
					{
						break;
					}
					num4 = zptr[num3];
					num5 = num3;
					while (FullGtU(zptr[num5 - num2] + d, num4 + d))
					{
						zptr[num5] = zptr[num5 - num2];
						num5 -= num2;
						if (num5 <= lo + num2 - 1)
						{
							break;
						}
					}
					zptr[num5] = num4;
					num3++;
					if (num3 > hi)
					{
						break;
					}
					num4 = zptr[num3];
					num5 = num3;
					while (FullGtU(zptr[num5 - num2] + d, num4 + d))
					{
						zptr[num5] = zptr[num5 - num2];
						num5 -= num2;
						if (num5 <= lo + num2 - 1)
						{
							break;
						}
					}
					zptr[num5] = num4;
					num3++;
					if (workDone > workLimit && firstAttempt)
					{
						return;
					}
				}
			}
		}

		private void Vswap(int p1, int p2, int n)
		{
			int num = 0;
			while (n > 0)
			{
				num = zptr[p1];
				zptr[p1] = zptr[p2];
				zptr[p2] = num;
				p1++;
				p2++;
				n--;
			}
		}

		private byte Med3(byte a, byte b, byte c)
		{
			if (a > b)
			{
				byte b2 = a;
				a = b;
				b = b2;
			}
			if (b > c)
			{
				byte b2 = b;
				b = c;
				c = b2;
			}
			if (a > b)
			{
				b = a;
			}
			return b;
		}

		private void QSort3(int loSt, int hiSt, int dSt)
		{
			StackElem[] array = new StackElem[QSORT_STACK_SIZE];
			for (int i = 0; i < QSORT_STACK_SIZE; i++)
			{
				array[i] = new StackElem();
			}
			int num = 0;
			array[num].ll = loSt;
			array[num].hh = hiSt;
			array[num].dd = dSt;
			num++;
			while (num > 0)
			{
				if (num >= QSORT_STACK_SIZE)
				{
					Panic();
				}
				num--;
				int ll = array[num].ll;
				int hh = array[num].hh;
				int dd = array[num].dd;
				if (hh - ll < SMALL_THRESH || dd > DEPTH_THRESH)
				{
					SimpleSort(ll, hh, dd);
					if (workDone > workLimit && firstAttempt)
					{
						break;
					}
					continue;
				}
				int num2 = Med3(block[zptr[ll] + dd + 1], block[zptr[hh] + dd + 1], block[zptr[ll + hh >> 1] + dd + 1]);
				int num4;
				int num3 = (num4 = ll);
				int num6;
				int num5 = (num6 = hh);
				int num7;
				while (true)
				{
					if (num3 <= num5)
					{
						num7 = block[zptr[num3] + dd + 1] - num2;
						if (num7 == 0)
						{
							int num8 = 0;
							num8 = zptr[num3];
							zptr[num3] = zptr[num4];
							zptr[num4] = num8;
							num4++;
							num3++;
							continue;
						}
						if (num7 <= 0)
						{
							num3++;
							continue;
						}
					}
					while (num3 <= num5)
					{
						num7 = block[zptr[num5] + dd + 1] - num2;
						if (num7 == 0)
						{
							int num9 = 0;
							num9 = zptr[num5];
							zptr[num5] = zptr[num6];
							zptr[num6] = num9;
							num6--;
							num5--;
						}
						else
						{
							if (num7 < 0)
							{
								break;
							}
							num5--;
						}
					}
					if (num3 > num5)
					{
						break;
					}
					int num10 = zptr[num3];
					zptr[num3] = zptr[num5];
					zptr[num5] = num10;
					num3++;
					num5--;
				}
				if (num6 < num4)
				{
					array[num].ll = ll;
					array[num].hh = hh;
					array[num].dd = dd + 1;
					num++;
					continue;
				}
				num7 = ((num4 - ll < num3 - num4) ? (num4 - ll) : (num3 - num4));
				Vswap(ll, num3 - num7, num7);
				int num11 = ((hh - num6 < num6 - num5) ? (hh - num6) : (num6 - num5));
				Vswap(num3, hh - num11 + 1, num11);
				num7 = ll + num3 - num4 - 1;
				num11 = hh - (num6 - num5) + 1;
				array[num].ll = ll;
				array[num].hh = num7;
				array[num].dd = dd;
				num++;
				array[num].ll = num7 + 1;
				array[num].hh = num11 - 1;
				array[num].dd = dd + 1;
				num++;
				array[num].ll = num11;
				array[num].hh = hh;
				array[num].dd = dd;
				num++;
			}
		}

		private void MainSort()
		{
			int[] array = new int[256];
			int[] array2 = new int[256];
			bool[] array3 = new bool[256];
			for (int i = 0; i < BZip2Constants.NUM_OVERSHOOT_BYTES; i++)
			{
				block[last + i + 2] = block[i % (last + 1) + 1];
			}
			for (int i = 0; i <= last + BZip2Constants.NUM_OVERSHOOT_BYTES; i++)
			{
				quadrant[i] = 0;
			}
			block[0] = block[last + 1];
			if (last < 4000)
			{
				for (int i = 0; i <= last; i++)
				{
					zptr[i] = i;
				}
				firstAttempt = false;
				workDone = (workLimit = 0);
				SimpleSort(0, last, 0);
				return;
			}
			int num = 0;
			for (int i = 0; i <= 255; i++)
			{
				array3[i] = false;
			}
			for (int i = 0; i <= 65536; i++)
			{
				ftab[i] = 0;
			}
			int num2 = block[0];
			int[] array5;
			IntPtr intPtr;
			for (int i = 0; i <= last; i++)
			{
				int num3 = block[i + 1];
				int[] array4 = (array5 = ftab);
				int num4 = (num2 << 8) + num3;
				intPtr = (IntPtr)num4;
				array4[num4] = array5[(long)intPtr] + 1;
				num2 = num3;
			}
			for (int i = 1; i <= 65536; i++)
			{
				int[] array6 = (array5 = ftab);
				int num5 = i;
				intPtr = (IntPtr)num5;
				array6[num5] = array5[(long)intPtr] + ftab[i - 1];
			}
			num2 = block[1];
			int num6;
			for (int i = 0; i < last; i++)
			{
				int num3 = block[i + 2];
				num6 = (num2 << 8) + num3;
				num2 = num3;
				int[] array7 = (array5 = ftab);
				int num7 = num6;
				intPtr = (IntPtr)num7;
				array7[num7] = array5[(long)intPtr] - 1;
				zptr[ftab[num6]] = i;
			}
			num6 = (block[last + 1] << 8) + block[1];
			int[] array8 = (array5 = ftab);
			int num8 = num6;
			intPtr = (IntPtr)num8;
			array8[num8] = array5[(long)intPtr] - 1;
			zptr[ftab[num6]] = last;
			for (int i = 0; i <= 255; i++)
			{
				array[i] = i;
			}
			int num9 = 1;
			do
			{
				num9 = 3 * num9 + 1;
			}
			while (num9 <= 256);
			do
			{
				num9 /= 3;
				for (int i = num9; i <= 255; i++)
				{
					int num10 = array[i];
					num6 = i;
					while (ftab[array[num6 - num9] + 1 << 8] - ftab[array[num6 - num9] << 8] > ftab[num10 + 1 << 8] - ftab[num10 << 8])
					{
						array[num6] = array[num6 - num9];
						num6 -= num9;
						if (num6 <= num9 - 1)
						{
							break;
						}
					}
					array[num6] = num10;
				}
			}
			while (num9 != 1);
			for (int i = 0; i <= 255; i++)
			{
				int num11 = array[i];
				for (num6 = 0; num6 <= 255; num6++)
				{
					int num12 = (num11 << 8) + num6;
					if ((ftab[num12] & SETMASK) == SETMASK)
					{
						continue;
					}
					int num13 = ftab[num12] & CLEARMASK;
					int num14 = (ftab[num12 + 1] & CLEARMASK) - 1;
					if (num14 > num13)
					{
						QSort3(num13, num14, 2);
						num += num14 - num13 + 1;
						if (workDone > workLimit && firstAttempt)
						{
							return;
						}
					}
					int[] array9 = (array5 = ftab);
					intPtr = (IntPtr)num12;
					array9[num12] = array5[(long)intPtr] | SETMASK;
				}
				array3[num11] = true;
				if (i < 255)
				{
					int num15 = ftab[num11 << 8] & CLEARMASK;
					int num16 = (ftab[num11 + 1 << 8] & CLEARMASK) - num15;
					int j;
					for (j = 0; num16 >> j > 65534; j++)
					{
					}
					for (num6 = 0; num6 < num16; num6++)
					{
						int num17 = zptr[num15 + num6];
						int num18 = num6 >> j;
						quadrant[num17] = num18;
						if (num17 < BZip2Constants.NUM_OVERSHOOT_BYTES)
						{
							quadrant[num17 + last + 1] = num18;
						}
					}
					if (num16 - 1 >> j > 65535)
					{
						Panic();
					}
				}
				for (num6 = 0; num6 <= 255; num6++)
				{
					array2[num6] = ftab[(num6 << 8) + num11] & CLEARMASK;
				}
				for (num6 = ftab[num11 << 8] & CLEARMASK; num6 < (ftab[num11 + 1 << 8] & CLEARMASK); num6++)
				{
					num2 = block[zptr[num6]];
					if (!array3[num2])
					{
						zptr[array2[num2]] = ((zptr[num6] == 0) ? last : (zptr[num6] - 1));
						int[] array10 = (array5 = array2);
						int num19 = num2;
						intPtr = (IntPtr)num19;
						array10[num19] = array5[(long)intPtr] + 1;
					}
				}
				for (num6 = 0; num6 <= 255; num6++)
				{
					int[] array11 = (array5 = ftab);
					int num20 = (num6 << 8) + num11;
					intPtr = (IntPtr)num20;
					array11[num20] = array5[(long)intPtr] | SETMASK;
				}
			}
		}

		private void RandomiseBlock()
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < 256; i++)
			{
				inUse[i] = false;
			}
			for (int i = 0; i <= last; i++)
			{
				if (num == 0)
				{
					num = BZip2Constants.rNums[num2];
					num2++;
					if (num2 == 512)
					{
						num2 = 0;
					}
				}
				num--;
				byte[] array2;
				byte[] array = (array2 = block);
				int num3 = i + 1;
				IntPtr intPtr = (IntPtr)num3;
				array[num3] = (byte)((uint)array2[(long)intPtr] ^ ((num == 1) ? 1u : 0u));
				byte[] array3 = (array2 = block);
				int num4 = i + 1;
				intPtr = (IntPtr)num4;
				array3[num4] = (byte)(array2[(long)intPtr] & 0xFF);
				inUse[block[i + 1]] = true;
			}
		}

		private void DoReversibleTransformation()
		{
			workLimit = workFactor * last;
			workDone = 0;
			blockRandomised = false;
			firstAttempt = true;
			MainSort();
			if (workDone > workLimit && firstAttempt)
			{
				RandomiseBlock();
				workLimit = (workDone = 0);
				blockRandomised = true;
				firstAttempt = false;
				MainSort();
			}
			origPtr = -1;
			for (int i = 0; i <= last; i++)
			{
				if (zptr[i] == 0)
				{
					origPtr = i;
					break;
				}
			}
			if (origPtr == -1)
			{
				Panic();
			}
		}

		private bool FullGtU(int i1, int i2)
		{
			byte b = block[i1 + 1];
			byte b2 = block[i2 + 1];
			if (b != b2)
			{
				return b > b2;
			}
			i1++;
			i2++;
			b = block[i1 + 1];
			b2 = block[i2 + 1];
			if (b != b2)
			{
				return b > b2;
			}
			i1++;
			i2++;
			b = block[i1 + 1];
			b2 = block[i2 + 1];
			if (b != b2)
			{
				return b > b2;
			}
			i1++;
			i2++;
			b = block[i1 + 1];
			b2 = block[i2 + 1];
			if (b != b2)
			{
				return b > b2;
			}
			i1++;
			i2++;
			b = block[i1 + 1];
			b2 = block[i2 + 1];
			if (b != b2)
			{
				return b > b2;
			}
			i1++;
			i2++;
			b = block[i1 + 1];
			b2 = block[i2 + 1];
			if (b != b2)
			{
				return b > b2;
			}
			i1++;
			i2++;
			int num = last + 1;
			do
			{
				b = block[i1 + 1];
				b2 = block[i2 + 1];
				if (b != b2)
				{
					return b > b2;
				}
				int num2 = quadrant[i1];
				int num3 = quadrant[i2];
				if (num2 != num3)
				{
					return num2 > num3;
				}
				i1++;
				i2++;
				b = block[i1 + 1];
				b2 = block[i2 + 1];
				if (b != b2)
				{
					return b > b2;
				}
				num2 = quadrant[i1];
				num3 = quadrant[i2];
				if (num2 != num3)
				{
					return num2 > num3;
				}
				i1++;
				i2++;
				b = block[i1 + 1];
				b2 = block[i2 + 1];
				if (b != b2)
				{
					return b > b2;
				}
				num2 = quadrant[i1];
				num3 = quadrant[i2];
				if (num2 != num3)
				{
					return num2 > num3;
				}
				i1++;
				i2++;
				b = block[i1 + 1];
				b2 = block[i2 + 1];
				if (b != b2)
				{
					return b > b2;
				}
				num2 = quadrant[i1];
				num3 = quadrant[i2];
				if (num2 != num3)
				{
					return num2 > num3;
				}
				i1++;
				i2++;
				if (i1 > last)
				{
					i1 -= last;
					i1--;
				}
				if (i2 > last)
				{
					i2 -= last;
					i2--;
				}
				num -= 4;
				workDone++;
			}
			while (num >= 0);
			return false;
		}

		private void AllocateCompressStructures()
		{
			int num = BZip2Constants.baseBlockSize * blockSize100k;
			block = new byte[num + 1 + BZip2Constants.NUM_OVERSHOOT_BYTES];
			quadrant = new int[num + BZip2Constants.NUM_OVERSHOOT_BYTES];
			zptr = new int[num];
			ftab = new int[65537];
			if (block != null && quadrant != null && zptr != null)
			{
				int[] ftab2 = ftab;
			}
			szptr = new short[2 * num];
		}

		private void GenerateMTFValues()
		{
			char[] array = new char[256];
			MakeMaps();
			int num = nInUse + 1;
			for (int i = 0; i <= num; i++)
			{
				mtfFreq[i] = 0;
			}
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < nInUse; i++)
			{
				array[i] = (char)i;
			}
			int[] array3;
			IntPtr intPtr;
			for (int i = 0; i <= last; i++)
			{
				char c = unseqToSeq[block[zptr[i]]];
				int num4 = 0;
				char c2 = array[num4];
				while (c != c2)
				{
					num4++;
					char c3 = c2;
					c2 = array[num4];
					array[num4] = c3;
				}
				array[0] = c2;
				if (num4 == 0)
				{
					num3++;
					continue;
				}
				if (num3 > 0)
				{
					num3--;
					while (true)
					{
						switch (num3 % 2)
						{
						case 0:
						{
							szptr[num2] = (short)BZip2Constants.RUNA;
							num2++;
							int[] array4 = (array3 = mtfFreq);
							int rUNA = BZip2Constants.RUNA;
							intPtr = (IntPtr)rUNA;
							array4[rUNA] = array3[(long)intPtr] + 1;
							break;
						}
						case 1:
						{
							szptr[num2] = (short)BZip2Constants.RUNB;
							num2++;
							int[] array2 = (array3 = mtfFreq);
							int rUNB = BZip2Constants.RUNB;
							intPtr = (IntPtr)rUNB;
							array2[rUNB] = array3[(long)intPtr] + 1;
							break;
						}
						}
						if (num3 < 2)
						{
							break;
						}
						num3 = (num3 - 2) / 2;
					}
					num3 = 0;
				}
				szptr[num2] = (short)(num4 + 1);
				num2++;
				int[] array5 = (array3 = mtfFreq);
				int num5 = num4 + 1;
				intPtr = (IntPtr)num5;
				array5[num5] = array3[(long)intPtr] + 1;
			}
			if (num3 > 0)
			{
				num3--;
				while (true)
				{
					switch (num3 % 2)
					{
					case 0:
					{
						szptr[num2] = (short)BZip2Constants.RUNA;
						num2++;
						int[] array7 = (array3 = mtfFreq);
						int rUNA2 = BZip2Constants.RUNA;
						intPtr = (IntPtr)rUNA2;
						array7[rUNA2] = array3[(long)intPtr] + 1;
						break;
					}
					case 1:
					{
						szptr[num2] = (short)BZip2Constants.RUNB;
						num2++;
						int[] array6 = (array3 = mtfFreq);
						int rUNB2 = BZip2Constants.RUNB;
						intPtr = (IntPtr)rUNB2;
						array6[rUNB2] = array3[(long)intPtr] + 1;
						break;
					}
					}
					if (num3 < 2)
					{
						break;
					}
					num3 = (num3 - 2) / 2;
				}
			}
			szptr[num2] = (short)num;
			num2++;
			int[] array8 = (array3 = mtfFreq);
			intPtr = (IntPtr)num;
			array8[num] = array3[(long)intPtr] + 1;
			nMTF = num2;
		}
	}
}
