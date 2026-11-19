namespace Latiprimere.Ipp;

/// <summary>
/// The IPP attributes Latiprimere requests from print systems (RFC 8011 and CUPS extensions).
/// </summary>
internal static class IppRequestedAttributes
{
	/// <summary>The printer's queue name.</summary>
	internal const string PrinterName = "printer-name";

	/// <summary>The URIs the printer accepts requests on.</summary>
	internal const string PrinterUriSupported = "printer-uri-supported";

	/// <summary>The printer's human-readable description.</summary>
	internal const string PrinterInfo = "printer-info";

	/// <summary>The printer's location.</summary>
	internal const string PrinterLocation = "printer-location";

	/// <summary>The printer's make and model.</summary>
	internal const string PrinterMakeAndModel = "printer-make-and-model";

	/// <summary>The printer's state.</summary>
	internal const string PrinterState = "printer-state";

	/// <summary>Detail about the printer's state.</summary>
	internal const string PrinterStateMessage = "printer-state-message";

	/// <summary>The document formats the printer accepts.</summary>
	internal const string DocumentFormatSupported = "document-format-supported";

	/// <summary>The operations the printer supports, such as Validate-Job.</summary>
	internal const string OperationsSupported = "operations-supported";

	/// <summary>The printer's UUID, which IPP Everywhere printers also announce over DNS-SD.</summary>
	internal const string PrinterUuid = "printer-uuid";

	/// <summary>Paper sizes supported.</summary>
	internal const string MediaSupported = "media-supported";

	/// <summary>Default paper size.</summary>
	internal const string MediaDefault = "media-default";

	/// <summary>One- and two-sided modes supported.</summary>
	internal const string SidesSupported = "sides-supported";

	/// <summary>Default one- or two-sided mode.</summary>
	internal const string SidesDefault = "sides-default";

	/// <summary>Colour modes supported.</summary>
	internal const string PrintColorModeSupported = "print-color-mode-supported";

	/// <summary>Default colour mode.</summary>
	internal const string PrintColorModeDefault = "print-color-mode-default";

	/// <summary>Whether the printer can print in colour; older than <c>print-color-mode-supported</c>.</summary>
	internal const string ColorSupported = "color-supported";

	/// <summary>The range of copies a job can ask for.</summary>
	internal const string CopiesSupported = "copies-supported";

	/// <summary>Page orientations supported.</summary>
	internal const string OrientationRequestedSupported = "orientation-requested-supported";

	/// <summary>Paper sources (trays).</summary>
	internal const string MediaSourceSupported = "media-source-supported";

	/// <summary>The job's identifier.</summary>
	internal const string JobId = "job-id";

	/// <summary>The job's state.</summary>
	internal const string JobState = "job-state";

	/// <summary>Detail about the job's state.</summary>
	internal const string JobStateMessage = "job-state-message";

	/// <summary>The number of impressions (page sides) printed so far.</summary>
	internal const string JobImpressionsCompleted = "job-impressions-completed";

	/// <summary>
	/// The printer description attributes Latiprimere reads to build a <c>PrinterInfo</c>.
	/// </summary>
	internal static readonly string[] PrinterSummaryAttributes = new string[]
	{
		IppRequestedAttributes.PrinterName,
		IppRequestedAttributes.PrinterUriSupported,
		IppRequestedAttributes.PrinterInfo,
		IppRequestedAttributes.PrinterLocation,
		IppRequestedAttributes.PrinterMakeAndModel,
		IppRequestedAttributes.PrinterState,
		IppRequestedAttributes.PrinterStateMessage
	};

	/// <summary>
	/// The attributes Latiprimere reads from a network printer: the summary, plus the formats it accepts, because a
	/// printer (unlike CUPS) can't convert documents it doesn't support, and its UUID, to check that a remembered
	/// address still reaches the same printer.
	/// </summary>
	internal static readonly string[] NetworkPrinterAttributes = new string[]
	{
		IppRequestedAttributes.PrinterName,
		IppRequestedAttributes.PrinterUriSupported,
		IppRequestedAttributes.PrinterInfo,
		IppRequestedAttributes.PrinterLocation,
		IppRequestedAttributes.PrinterMakeAndModel,
		IppRequestedAttributes.PrinterState,
		IppRequestedAttributes.PrinterStateMessage,
		IppRequestedAttributes.DocumentFormatSupported,
		IppRequestedAttributes.PrinterUuid,
		IppRequestedAttributes.OperationsSupported
	};

	/// <summary>
	/// The attributes read for <see cref="IPrinter.GetCapabilitiesAsync"/>.
	/// </summary>
	internal static readonly string[] CapabilityAttributes = new string[]
	{
		IppRequestedAttributes.MediaSupported,
		IppRequestedAttributes.MediaDefault,
		IppRequestedAttributes.SidesSupported,
		IppRequestedAttributes.SidesDefault,
		IppRequestedAttributes.PrintColorModeSupported,
		IppRequestedAttributes.PrintColorModeDefault,
		IppRequestedAttributes.ColorSupported,
		IppRequestedAttributes.CopiesSupported,
		IppRequestedAttributes.OrientationRequestedSupported,
		IppRequestedAttributes.MediaSourceSupported
	};

	/// <summary>
	/// The attributes read for <see cref="PrinterCapabilities.VendorOptions"/>: every job template attribute, through
	/// the group keyword, and the list of attributes a job may set (ADR 0032).
	/// </summary>
	internal static readonly string[] VendorOptionAttributes = new string[]
	{
		"job-template",
		"job-creation-attributes-supported",
	};

	/// <summary>
	/// The job attributes Latiprimere reads to build a <c>PrintJobStatus</c>.
	/// </summary>
	internal static readonly string[] JobStatusAttributes = new string[]
	{
		IppRequestedAttributes.JobId,
		IppRequestedAttributes.JobState,
		IppRequestedAttributes.JobStateMessage,
		IppRequestedAttributes.JobImpressionsCompleted
	};
}
