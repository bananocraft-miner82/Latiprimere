namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// The environment variables that switch on the integration tests against virtual printers.
/// </summary>
/// <remarks>
/// Every variable is unset by default, and the tests that need it are skipped. CI sets them on the runners that have
/// the virtual printers. See "Integration Tests and Real Printers" in CONTRIBUTING.md.
/// </remarks>
public static class IntegrationEnvironment
{
	/// <summary>
	/// The CUPS server of the test container, for example <c>localhost:631</c>.
	/// </summary>
	public const string CupsServerVariable = "LATIPRIMERE_TEST_CUPS_SERVER";

	/// <summary>
	/// The host directory mounted as the test container's <c>/output</c>, where its Raw queue saves job data; or, on a
	/// Mac, the folder where the capture server behind its Raw queue saves it.
	/// </summary>
	public const string CupsOutputVariable = "LATIPRIMERE_TEST_CUPS_OUTPUT";

	/// <summary>
	/// Which test queues the CUPS server has: <c>container</c> (the default) or <c>macos</c>; see
	/// <see cref="CupsTestSetup"/>.
	/// </summary>
	public const string CupsSetupVariable = "LATIPRIMERE_TEST_CUPS_SETUP";

	/// <summary>
	/// The URI of a virtual IPP printer, for example <c>ipp://localhost:8631/ipp/print</c>.
	/// </summary>
	public const string IppPrinterVariable = "LATIPRIMERE_TEST_IPP_PRINTER";

	/// <summary>
	/// The output directory of the Windows test printers that <c>tools/windows-test-printers/Install-TestPrinters.ps1</c>
	/// creates. Setting it says those printers are installed.
	/// </summary>
	public const string WindowsOutputVariable = "LATIPRIMERE_TEST_WINDOWS_OUTPUT";

	/// <summary>
	/// Gets the CUPS server of the test container, or <see langword="null"/> when CUPS tests are switched off.
	/// </summary>
	public static string? CupsServer
	{
		get
		{
			return IntegrationEnvironment.Read(IntegrationEnvironment.CupsServerVariable);
		}
	}

	/// <summary>
	/// Gets the host directory holding the test container's output, or <see langword="null"/> when it isn't mounted.
	/// </summary>
	public static string? CupsOutputDirectory
	{
		get
		{
			return IntegrationEnvironment.Read(IntegrationEnvironment.CupsOutputVariable);
		}
	}

	/// <summary>
	/// Gets which test queues the CUPS server has: the test container's unless the variable says otherwise.
	/// </summary>
	/// <exception cref="InvalidOperationException">The variable is set to an unknown value.</exception>
	public static CupsTestSetup CupsSetup
	{
		get
		{
			string? value = IntegrationEnvironment.Read(IntegrationEnvironment.CupsSetupVariable);

			if (value is null || string.Equals(value, "container", StringComparison.OrdinalIgnoreCase))
			{
				return CupsTestSetup.Container;
			}

			if (string.Equals(value, "macos", StringComparison.OrdinalIgnoreCase))
			{
				return CupsTestSetup.Mac;
			}

			throw new InvalidOperationException
			(
				$"{IntegrationEnvironment.CupsSetupVariable} must be 'container' or 'macos', not '{value}'."
			);
		}
	}

	/// <summary>
	/// Gets the URI of the virtual IPP printer, or <see langword="null"/> when direct IPP tests are switched off.
	/// </summary>
	/// <exception cref="InvalidOperationException">The variable is set but isn't an absolute URI.</exception>
	public static Uri? IppPrinterUri
	{
		get
		{
			string? value = IntegrationEnvironment.Read(IntegrationEnvironment.IppPrinterVariable);

			if (value is null)
			{
				return null;
			}

			if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? printerUri))
			{
				throw new InvalidOperationException($"{IntegrationEnvironment.IppPrinterVariable} is not an absolute URI: '{value}'.");
			}

			return printerUri;
		}
	}

	/// <summary>
	/// Gets the output directory of the Windows test printers, or <see langword="null"/> when they aren't installed.
	/// </summary>
	public static string? WindowsOutputDirectory
	{
		get
		{
			return IntegrationEnvironment.Read(IntegrationEnvironment.WindowsOutputVariable);
		}
	}

	/// <summary>
	/// Gets the message shown when a test is skipped because a variable is unset.
	/// </summary>
	/// <param name="variableName">The variable the test needs.</param>
	/// <returns>The skip reason.</returns>
	public static string GetSkipReason(string variableName)
	{
		return $"Set {variableName} to run this test against the virtual printers (see CONTRIBUTING.md).";
	}

	/// <summary>
	/// Gets the message shown when a test is skipped because its CUPS queue exists only in the test container.
	/// </summary>
	/// <param name="queueName">The queue the test needs.</param>
	/// <returns>The skip reason.</returns>
	public static string GetContainerOnlySkipReason(string queueName)
	{
		return $"The {queueName} queue exists only in the CUPS test container, and {IntegrationEnvironment.CupsSetupVariable} says this "
			+ "server is a Mac's own CUPS.";
	}

	/// <summary>
	/// Reads a variable, treating an empty or blank value as unset.
	/// </summary>
	/// <param name="variableName">The variable to read.</param>
	/// <returns>The trimmed value, or <see langword="null"/>.</returns>
	private static string? Read(string variableName)
	{
		string? value = Environment.GetEnvironmentVariable(variableName);

		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
