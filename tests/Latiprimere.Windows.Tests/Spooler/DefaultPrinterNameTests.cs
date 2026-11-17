using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Spooler;

/// <summary>
/// Reading the default printer's name while it changes: Windows can make another printer the default between asking
/// for the name's length and reading it. The spooler is played by a fake GetDefaultPrinter.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DefaultPrinterNameTests
{
	[Fact]
	public void GivenDefaultPrinterWhenReadThenReturnsItsName()
	{
		// Arrange
		ChangingDefault spooler = new ChangingDefault("Office");

		// Act
		string? name = WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		Assert.Equal("Office", name);
		Assert.Equal(2, spooler.CallCount);
	}

	[Fact]
	public void GivenNoDefaultPrinterWhenReadThenReturnsNull()
	{
		// Arrange
		ChangingDefault spooler = new ChangingDefault((string?)null);

		// Act
		string? name = WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		Assert.Null(name);
	}

	[Fact]
	public void GivenDefaultChangingToLongerNameBetweenCallsWhenReadThenAsksAgainAndReturnsTheNewName()
	{
		// Arrange — the race seen on 2026-11-16: the name grew after its length was read
		ChangingDefault spooler = new ChangingDefault("Fax", "Microsoft Print to PDF");

		// Act
		string? name = WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		Assert.Equal("Microsoft Print to PDF", name);
		Assert.Equal(3, spooler.CallCount);
	}

	[Fact]
	public void GivenDefaultChangingToShorterNameBetweenCallsWhenReadThenReturnsTheNewNameWithoutItsPadding()
	{
		// Arrange
		ChangingDefault spooler = new ChangingDefault("Microsoft Print to PDF", "Fax");

		// Act
		string? name = WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		Assert.Equal("Fax", name);
	}

	[Fact]
	public void GivenDefaultRemovedBetweenCallsWhenReadThenReturnsNull()
	{
		// Arrange
		ChangingDefault spooler = new ChangingDefault("Office", null);

		// Act
		string? name = WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		Assert.Null(name);
	}

	[Fact]
	public void GivenNameThatKeepsGrowingWhenReadThenGivesUpWithInsufficientBuffer()
	{
		// Arrange — every call finds a longer name, so the buffer is never big enough
		Assert.SkipUnless(OperatingSystem.IsWindows(), "Spooler error messages come from Windows.");
		ChangingDefault spooler = new ChangingDefault("A", "AB", "ABC", "ABCD", "ABCDE");

		// Act
		Action read = () => WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(read);
		Assert.Equal(Win32ErrorCodes.InsufficientBuffer, exception.NativeErrorCode);
	}

	[Fact]
	public void GivenSpoolerFailureWhenReadThenThrowsWithItsError()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), "Spooler error messages come from Windows.");
		ChangingDefault spooler = new ChangingDefault("Office") { FailWith = Win32ErrorCodes.RpcServerUnavailable };

		// Act
		Action read = () => WinSpoolApi.ReadDefaultPrinterName(spooler.Call);

		// Assert
		Exception exception = Assert.ThrowsAny<Exception>(read);
		Assert.Equal(Win32ErrorCodes.RpcServerUnavailable, Assert.IsAssignableFrom<PrintException>(exception).NativeErrorCode);
	}

	/// <summary>
	/// A fake GetDefaultPrinter whose default printer can change from one call to the next.
	/// </summary>
	private sealed class ChangingDefault
	{
		private readonly string?[] defaults; // The default printer at each call; the last one stays
		private int callIndex;               // The next call's index into defaults

		/// <summary>
		/// Initialises a new instance of the <see cref="ChangingDefault"/> class.
		/// </summary>
		/// <param name="defaults">The default printer's name at each call, or <see langword="null"/> for none.</param>
		public ChangingDefault(params string?[] defaults)
		{
			this.defaults = defaults;
		}

		/// <summary>
		/// Gets the number of calls made.
		/// </summary>
		public int CallCount
		{
			get
			{
				return this.callIndex;
			}
		}

		/// <summary>
		/// Gets or sets a Win32 error every call fails with, or 0 to behave normally.
		/// </summary>
		public int FailWith { get; set; }

		/// <summary>
		/// Behaves like GetDefaultPrinterW with the default printer of this call.
		/// </summary>
		/// <param name="buffer">The buffer; empty to ask for the length.</param>
		/// <param name="length">The buffer's length; on return, the length needed including the null.</param>
		/// <param name="error">The Win32 error when the call fails.</param>
		/// <returns><see langword="true"/> on success.</returns>
		public bool Call(Span<char> buffer, ref uint length, out int error)
		{
			string? name = this.defaults[Math.Min(this.callIndex, this.defaults.Length - 1)];
			this.callIndex++;

			if (this.FailWith != 0)
			{
				error = this.FailWith;

				return false;
			}

			if (name is null)
			{
				error = Win32ErrorCodes.FileNotFound;

				return false;
			}

			uint needed = (uint)name.Length + 1;

			if (length < needed)
			{
				length = needed;
				error = Win32ErrorCodes.InsufficientBuffer;

				return false;
			}

			name.AsSpan().CopyTo(buffer);
			buffer[name.Length] = '\0';
			length = needed;
			error = Win32ErrorCodes.Success;

			return true;
		}
	}
}
