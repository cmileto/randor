using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Improved
{
	public enum RDRandError : int
	{
		Unknown = -4,
		Unsupported = -3,
		Supported = -2,
		NotReady = -1,

		Failure = 0,

		Success = 1,
	}

	internal sealed class CSPRandom : IRandomImpl
	{
		private static RNGCryptoServiceProvider _CSP = new RNGCryptoServiceProvider();

		public void GetBytes(byte[] b)
		{
			lock (_CSP)
				_CSP.GetBytes(b);
		}
	}

	internal sealed class RDRand32 : IRandomImpl, IHardwareRNG
	{
		internal class SafeNativeMethods
		{
			[DllImport("rdrand32")]
			internal static extern RDRandError rdrand_32(ref uint rand, bool retry);

			[DllImport("rdrand32")]
			internal static extern RDRandError rdrand_get_bytes(int n, byte[] buffer);
		}

		public bool IsSupported()
		{
			uint r = 0;
			return SafeNativeMethods.rdrand_32(ref r, true) == RDRandError.Success;
		}

		public void GetBytes(byte[] b)
		{
			SafeNativeMethods.rdrand_get_bytes(b.Length, b);
		}
	}

	internal sealed class RDRand64 : IRandomImpl, IHardwareRNG
	{
		internal static class SafeNativeMethods
		{
			[DllImport("rdrand64")]
			internal static extern RDRandError rdrand_64(ref ulong rand, bool retry);

			[DllImport("rdrand64")]
			internal static extern RDRandError rdrand_get_bytes(int n, byte[] buffer);
		}

		public bool IsSupported()
		{
			ulong r = 0;
			return SafeNativeMethods.rdrand_64(ref r, true) == RDRandError.Success;
		}

		public void GetBytes(byte[] b)
		{
			SafeNativeMethods.rdrand_get_bytes(b.Length, b);
		}
	}
}