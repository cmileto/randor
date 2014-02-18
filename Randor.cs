/***************************************************************************
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Improved
{
	public enum RNGType
	{
		AutoDetect,
		CSPRandom,
		RDRand32,
		RDRand64
	}

    public class Randor
    {
		private bool _Pooling = false;

		private RNGType _RNGType; 
		private IRandomImpl _Random;

		public Randor() : this(RNGType.AutoDetect, true) { }

		public Randor(RNGType r, bool pooling)
		{
			_Pooling = pooling;

			_RNGType = r;
			_Random = RandomImpl.Acquire(r);

			if (_Pooling)
			{
				_Working = new byte[BUFFER_SIZE];
				_Buffer = new byte[BUFFER_SIZE];
				_Random.GetBytes(_Working);
				ThreadPool.QueueUserWorkItem(new WaitCallback(Fill));
			}
		}

		public bool IsHardwareRNG { get { return _Random is IHardwareRNG; } }

		public string Name { get { return _Random.GetType().ToString(); } }

		public RNGType RNGType { get { return _RNGType; } }

		public int Next(int c)
		{
			return (int)(c * NextDouble());
		}

		public bool NextBool()
		{
			return (NextByte() & 1) == 1;
		}

		public unsafe double NextDouble()
		{
			byte[] b = new byte[8];

			NextBytes(b);

			if (BitConverter.IsLittleEndian)
				b[7] = 0;
			else
				b[0] = 0;

			ulong r = 0;
			fixed (byte* buf = b)
				r = *(ulong*)(&buf[0]) >> 3;

			/* double: 53 bits of significand precision
			 * ulong.MaxValue >> 11 = 9007199254740991
			 * 2^53 = 9007199254740992
			 */

			return (double)r / 9007199254740992;
		}

		public byte NextByte()
		{
			if (!_Pooling)
			{
				byte[] b = new byte[1];
				_Random.GetBytes(b);
				return b[0];
			}

			lock (_Sync)
			{
				CheckSwap(1);
				return _Working[_Index++];
			}
		}

		public void NextBytes(byte[] b)
		{
			int c = b.Length;

			if (!_Pooling || c >= LARGE_REQUEST)
			{
				_Random.GetBytes(b);
				return;
			}

			lock (_Sync)
			{
				CheckSwap(c);
				Buffer.BlockCopy(_Working, _Index, b, 0, c);
				_Index += c;
			}
		}

		#region Pooling
		private int BUFFER_SIZE = 0x10000;
		private int LARGE_REQUEST = 0x40;

		private byte[] _Working;
		private byte[] _Buffer;

		private int _Index = 0;

		private object _Sync = new object();

		private ManualResetEvent _Filled = new ManualResetEvent(false);

		private void CheckSwap(int c)
		{
			if (_Index + c < BUFFER_SIZE)
				return;

			_Filled.WaitOne();

			byte[] b = _Working;
			_Working = _Buffer;
			_Buffer = b;
			_Index = 0;

			_Filled.Reset();

			ThreadPool.QueueUserWorkItem(new WaitCallback(Fill));
		}

		private void Fill(object o)
		{
			_Random.GetBytes(_Buffer);
			_Filled.Set();
		}
		#endregion
	}

	internal static class RandomImpl {
		private static Dictionary<RNGType, IRandomImpl> _Instances = new Dictionary<RNGType, IRandomImpl>();

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static IRandomImpl Acquire(RNGType r)
		{
			bool autodetect = (r == RNGType.AutoDetect);
			
			if (autodetect)
			{
				bool _unix = false;
				int platform = (int)Environment.OSVersion.Platform;
				if (platform == 4 || platform == 128) // MS 4, MONO 128
					_unix = true;

				if (_unix)
				{
					r = RNGType.CSPRandom;
				}
				else if (Environment.Is64BitProcess)
				{
					r = RNGType.RDRand64;
				}
				else
				{
					r = RNGType.RDRand32;
				}
			}

			if (r == RNGType.RDRand32)
			{
				if (!File.Exists("rdrand32.dll"))
				{
					if (autodetect)
						r = RNGType.CSPRandom;
					else
						throw new NotSupportedException("The selected hardware random number generator is not supported.");
				}
			}
			else if (r == RNGType.RDRand64)
			{
				if (!File.Exists("rdrand64.dll"))
				{
					if (autodetect)
						r = RNGType.CSPRandom;
					else
						throw new NotSupportedException("The selected hardware random number generator is not supported.");
				}
			}

			if (_Instances.ContainsKey(r))
				return _Instances[r];

			IRandomImpl i = null;

			switch (r)
			{
				case RNGType.CSPRandom: _Instances[r] = i = new CSPRandom(); break;
				case RNGType.RDRand32: _Instances[r] = i = new RDRand32(); break;
				case RNGType.RDRand64: _Instances[r] = i = new RDRand64(); break;
				default: throw new NotImplementedException("Unknown RNGType");
			}

			if (i is IHardwareRNG && !((IHardwareRNG)i).IsSupported())
			{
				if (autodetect)
				{
					_Instances.Remove(r);
					_Instances[RNGType.CSPRandom] = i = new CSPRandom();
				}
				else
				{
					throw new NotSupportedException("The selected hardware random number generator is not supported.");
				}
			}

			return i;
		}
	}
}
