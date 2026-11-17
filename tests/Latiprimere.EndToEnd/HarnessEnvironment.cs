using System.Globalization;

namespace Latiprimere.EndToEnd;

/// <summary>
/// The harness's own settings, and where it writes its reports.
/// </summary>
/// <remarks>
/// Which test printers exist comes from the integration tests' variables (<see cref="HarnessTargets"/>). The harness
/// adds one more: it runs only when <see cref="EnabledVariable"/> is set, so <c>dotnet test</c> in CI builds it but
/// skips it (ADR 0029).
/// </remarks>
public static class HarnessEnvironment
{
	/// <summary>
	/// The variable that switches the end-to-end scenarios on under xUnit: <c>1</c> or <c>true</c>.
	/// </summary>
	public const string EnabledVariable = "LATIPRIMERE_TEST_E2E";

	/// <summary>
	/// The variable naming an IPP printer that announces itself over mDNS, by its DNS-SD instance name, for the
	/// discovery scenario, such as <c>Virtual IPP Printer</c>.
	/// </summary>
	public const string MdnsNameVariable = "LATIPRIMERE_TEST_MDNS_NAME";

	/// <summary>
	/// The variable naming the folder reports are written under. Defaults to <c>out/harness</c> in the repository.
	/// </summary>
	public const string ReportRootVariable = "LATIPRIMERE_TEST_E2E_REPORTS";

	/// <summary>
	/// Gets a value indicating whether <see cref="EnabledVariable"/> switches the scenarios on.
	/// </summary>
	public static bool IsEnabled
	{
		get
		{
			string? value = Environment.GetEnvironmentVariable(HarnessEnvironment.EnabledVariable)?.Trim();

			return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// Gets the name of the .NET this process runs on, such as <c>net10.0</c>.
	/// </summary>
	public static string FrameworkName
	{
		get
		{
			return string.Create(CultureInfo.InvariantCulture, $"net{Environment.Version.Major}.{Environment.Version.Minor}");
		}
	}

	/// <summary>
	/// Gets a short name for this operating system: <c>windows</c>, <c>linux</c>, <c>macos</c> or <c>other</c>.
	/// </summary>
	public static string OperatingSystemName
	{
		get
		{
			if (OperatingSystem.IsWindows())
			{
				return "windows";
			}

			if (OperatingSystem.IsLinux())
			{
				return "linux";
			}

			return OperatingSystem.IsMacOS() ? "macos" : "other";
		}
	}

	/// <summary>
	/// Creates a new, empty folder for one run's report, named after the time, the OS, the .NET and the program.
	/// </summary>
	/// <param name="hostName">The program running the scenarios, such as <c>xunit</c> or <c>console</c>.</param>
	/// <returns>The folder's full path.</returns>
	/// <exception cref="ArgumentException"><paramref name="hostName"/> is <see langword="null"/>, empty or whitespace.</exception>
	public static string CreateReportDirectory(string hostName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

		string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
		string name = $"{timestamp}-{HarnessEnvironment.OperatingSystemName}-{HarnessEnvironment.FrameworkName}-{hostName}";
		string directory = Path.Combine(HarnessEnvironment.GetReportRoot(), name);
		Directory.CreateDirectory(directory);

		return directory;
	}

	/// <summary>
	/// Gets the folder reports are written under: <see cref="ReportRootVariable"/>, otherwise <c>out/harness</c> in the
	/// repository this program was built from, otherwise the temporary folder.
	/// </summary>
	/// <returns>The folder's full path.</returns>
	private static string GetReportRoot()
	{
		string? configured = Environment.GetEnvironmentVariable(HarnessEnvironment.ReportRootVariable);

		if (!string.IsNullOrWhiteSpace(configured))
		{
			return Path.GetFullPath(configured.Trim());
		}

		string? repository = HarnessEnvironment.FindRepositoryRoot();

		return repository is null
			? Path.Combine(Path.GetTempPath(), "latiprimere-harness")
			: Path.Combine(repository, "out", "harness");
	}

	/// <summary>
	/// Finds the repository this program was built from: the folder holding <c>Latiprimere.slnx</c>, up from
	/// <c>bin/&lt;configuration&gt;/&lt;framework&gt;/</c>.
	/// </summary>
	/// <returns>The folder, or <see langword="null"/> if the program wasn't run from a build in the repository.</returns>
	public static string? FindRepositoryRoot()
	{
		for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Latiprimere.slnx")))
			{
				return directory.FullName;
			}
		}

		return null;
	}
}
