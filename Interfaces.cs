namespace Improved
{
	interface IRandomImpl
	{
		void GetBytes(byte[] b);
	}

	interface IHardwareRNG
	{
		bool IsSupported();
	}
}