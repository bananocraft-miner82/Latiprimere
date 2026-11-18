using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// A COM <c>IStream</c> pointer, called through its vtable and released with <c>IUnknown::Release</c>.
/// </summary>
/// <remarks>
/// Only the four methods the Print Ticket code needs are called, through unmanaged function pointers, so no COM
/// interop marshalling is involved. The vtable's order is fixed by the COM ABI: IUnknown's QueryInterface, AddRef
/// and Release, ISequentialStream's Read and Write, then IStream's Seek and the rest.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed unsafe class SafeComStreamHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	private const int ReleaseSlot = 2; // IUnknown::Release
	private const int ReadSlot = 3;    // ISequentialStream::Read
	private const int WriteSlot = 4;   // ISequentialStream::Write
	private const int SeekSlot = 5;    // IStream::Seek

	/// <summary>
	/// Initialises a new instance of the <see cref="SafeComStreamHandle"/> class. Called by the interop marshaller.
	/// </summary>
	public SafeComStreamHandle()
		: base(ownsHandle: true)
	{
	}

	/// <summary>
	/// Writes bytes at the stream's position.
	/// </summary>
	/// <param name="data">The bytes.</param>
	/// <returns>The HRESULT.</returns>
	internal int Write(ReadOnlySpan<byte> data)
	{
		bool added = false;

		try
		{
			this.DangerousAddRef(ref added);
			IntPtr stream = this.handle;
			delegate* unmanaged[Stdcall]<IntPtr, byte*, uint, uint*, int> write =
				(delegate* unmanaged[Stdcall]<IntPtr, byte*, uint, uint*, int>)SafeComStreamHandle.Slot(stream, SafeComStreamHandle.WriteSlot);

			uint written;

			fixed (byte* bytes = data)
			{
				int result = write(stream, bytes, (uint)data.Length, &written);

				// A short write without an error would leave the ticket cut off
				return result >= 0 && written != (uint)data.Length ? PrintTicketConstants.WriteFault : result;
			}
		}
		finally
		{
			if (added)
			{
				this.DangerousRelease();
			}
		}
	}

	/// <summary>
	/// Reads the whole stream, from its start.
	/// </summary>
	/// <param name="result">The HRESULT of the first call that failed, or S_OK.</param>
	/// <returns>The bytes; empty if a call failed.</returns>
	internal byte[] ReadAll(out int result)
	{
		result = this.Rewind();

		if (result < 0)
		{
			return [];
		}

		bool added = false;

		try
		{
			this.DangerousAddRef(ref added);
			IntPtr stream = this.handle;
			delegate* unmanaged[Stdcall]<IntPtr, byte*, uint, uint*, int> read =
				(delegate* unmanaged[Stdcall]<IntPtr, byte*, uint, uint*, int>)SafeComStreamHandle.Slot(stream, SafeComStreamHandle.ReadSlot);

			using MemoryStream output = new MemoryStream();
			byte[] chunk = new byte[16 * 1024];

			// A memory stream reports S_OK with fewer bytes, or none, at its end
			while (true)
			{
				uint count;

				fixed (byte* bytes = chunk)
				{
					result = read(stream, bytes, (uint)chunk.Length, &count);
				}

				if (result < 0)
				{
					return [];
				}

				if (count == 0)
				{
					break;
				}

				output.Write(chunk, 0, (int)count);
			}

			result = 0;

			return output.ToArray();
		}
		finally
		{
			if (added)
			{
				this.DangerousRelease();
			}
		}
	}

	/// <summary>
	/// Moves the stream's position back to its start, so the next call reads it from the beginning.
	/// </summary>
	/// <returns>The HRESULT.</returns>
	internal int Rewind()
	{
		bool added = false;

		try
		{
			this.DangerousAddRef(ref added);
			IntPtr stream = this.handle;
			delegate* unmanaged[Stdcall]<IntPtr, long, uint, ulong*, int> seek =
				(delegate* unmanaged[Stdcall]<IntPtr, long, uint, ulong*, int>)SafeComStreamHandle.Slot(stream, SafeComStreamHandle.SeekSlot);

			return seek(stream, 0, PrintTicketConstants.SeekFromStart, null);
		}
		finally
		{
			if (added)
			{
				this.DangerousRelease();
			}
		}
	}

	/// <summary>
	/// Releases the stream, which frees its memory.
	/// </summary>
	/// <returns><see langword="true"/>.</returns>
	protected override bool ReleaseHandle()
	{
		delegate* unmanaged[Stdcall]<IntPtr, uint> release =
			(delegate* unmanaged[Stdcall]<IntPtr, uint>)SafeComStreamHandle.Slot(this.handle, SafeComStreamHandle.ReleaseSlot);

		release(this.handle);

		return true;
	}

	/// <summary>
	/// Reads a method's address from a COM object's vtable.
	/// </summary>
	/// <param name="instance">The COM object.</param>
	/// <param name="slot">The method's place in the vtable.</param>
	/// <returns>The method's address.</returns>
	private static void* Slot(IntPtr instance, int slot)
	{
		void** vtable = *(void***)instance;

		return vtable[slot];
	}
}
