using System.Collections.Concurrent;
using Latiprimere.Ipp.Discovery;
using Latiprimere.Ipp.Mapping;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp;

/// <summary>
/// Prints directly to IPP printers on the network: printers found with DNS-SD, plus any configured by URI.
/// </summary>
/// <remarks>
/// <para>
/// Printer identifiers take two forms. Discovered printers are identified by their announced name, such as
/// <c>ipp:dnssd:Office Laser</c>, which survives the printer getting a new address. Configured printers, and any IPP
/// printer, are identified by URI, such as <c>ipp:ipp://192.168.1.20:631/ipp/print</c>, which needs no discovery.
/// </para>
/// <para>
/// Looking a printer up by name tries the address last seen for that name first, from a listing or an earlier
/// lookup. If nothing is remembered, or that address doesn't answer as the same printer, it searches the network for
/// the name and remembers the address found.
/// </para>
/// <para>
/// Listing doesn't contact the printers, so a printer that is switched off still appears, with an unknown state;
/// looking a printer up reads its current details.
/// </para>
/// </remarks>
internal sealed class IppNetworkPrintSystem : IPrintSystem
{
	/// <summary>The prefix of every printer identifier from this print system.</summary>
	internal const string PrinterIdPrefix = "ipp:";

	/// <summary>The prefix of identifiers that name a discovered printer rather than give its URI.</summary>
	internal const string DnsSdPrinterIdPrefix = "ipp:dnssd:";

	/// <summary>How long a remembered address has to answer before the name is searched for instead.</summary>
	internal static readonly TimeSpan DefaultKnownAddressTimeout = TimeSpan.FromSeconds(3);

	private readonly IppPrintContext context;           // Client and settings shared with printers and jobs
	private readonly IIppPrinterDiscovery discovery;    // Finds printers on the network
	private readonly bool isDiscoveryEnabled;           // Whether listing includes discovered printers
	private readonly TimeSpan discoveryDuration;        // How long each discovery or name lookup listens at most
	private readonly IReadOnlyList<Uri> configuredUris; // Printers listed even if not discovered
	private readonly bool allowsAnyPrinterUri;          // Whether a lookup may name any URI, not only known ones
	private readonly IDisposable? ownedClient;          // The client to dispose with this print system, if owned
	private readonly ConcurrentDictionary<string, DiscoveredIppPrinter> knownPrinters =
		new ConcurrentDictionary<string, DiscoveredIppPrinter>(StringComparer.OrdinalIgnoreCase); // Last seen, by name

	/// <summary>
	/// Initialises a new instance of the <see cref="IppNetworkPrintSystem"/> class.
	/// </summary>
	/// <param name="context">The client and settings shared with printers and jobs.</param>
	/// <param name="discovery">Finds printers on the network.</param>
	/// <param name="options">The configuration; copied, so later changes have no effect.</param>
	/// <param name="ownedClient">The client to dispose with this print system, if it owns one.</param>
	public IppNetworkPrintSystem(IppPrintContext context, IIppPrinterDiscovery discovery, IppNetworkOptions options, IDisposable? ownedClient)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(discovery);
		ArgumentNullException.ThrowIfNull(options);

		this.context = context;
		this.discovery = discovery;
		this.isDiscoveryEnabled = options.EnableDiscovery;
		this.discoveryDuration = options.DiscoveryDuration;
		this.configuredUris = options.PrinterUris.ToList();
		this.allowsAnyPrinterUri = options.AllowAnyPrinterUri;
		this.ownedClient = ownedClient;
	}

	/// <inheritdoc/>
	public string Name
	{
		get
		{
			return "IPP";
		}
	}

	/// <summary>
	/// Gets how long a remembered address has to answer before the name is searched for instead. Settable for tests.
	/// </summary>
	internal TimeSpan KnownAddressTimeout { get; init; } = IppNetworkPrintSystem.DefaultKnownAddressTimeout;

	/// <inheritdoc/>
	/// <remarks>Always <see langword="true"/>: IPP needs only a network.</remarks>
	public bool IsSupported
	{
		get
		{
			return true;
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
	{
		IReadOnlyList<DiscoveredIppPrinter> discovered = Array.Empty<DiscoveredIppPrinter>();

		if (this.isDiscoveryEnabled)
		{
			discovered = await this.discovery
				.DiscoverAsync(this.discoveryDuration, cancellationToken)
				.ConfigureAwait(false);
		}

		// Remember where each name was seen, so looking it up can try that address before searching
		foreach (DiscoveredIppPrinter printer in discovered)
		{
			this.knownPrinters[printer.InstanceName] = printer;
		}

		// Discovered printers by name, except those also configured by URI, which keep the identifier the
		// application chose
		List<PrinterInfo> printers = discovered
			.Where(printer => !this.configuredUris.Contains(printer.PrinterUri))
			.Select(IppNetworkPrintSystem.ToPrinterInfo)
			.ToList();

		// Configured printers by URI, described from their announcement if they made one
		foreach (Uri printerUri in this.configuredUris)
		{
			DiscoveredIppPrinter? announcement = discovered.FirstOrDefault(printer => printer.PrinterUri == printerUri);
			printers.Add(IppNetworkPrintSystem.ToPrinterInfo(printerUri, announcement));
		}

		return printers;
	}

	/// <inheritdoc/>
	public async Task<IPrinter?> FindPrinterAsync(string printerId, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(printerId);

		if (!IppNetworkPrintSystem.TryParsePrinterId(printerId, out Uri? printerUri, out string? instanceName))
		{
			return null;
		}

		LocatedPrinter? located;

		if (instanceName is not null)
		{
			located = await this.FindByNameAsync(instanceName, cancellationToken).ConfigureAwait(false);
		}
		else if (printerUri is not null)
		{
			// Only known URIs are contacted, unless the application allows any: an identifier can name any host
			// and port, so one from outside the application could otherwise reach internal services
			located = this.IsKnownPrinterUri(printerUri)
				? await this.ReadPrinterAsync(printerUri, cancellationToken).ConfigureAwait(false)
				: null;
		}
		else
		{
			// Not reached: parsing gives either a URI or a name
			located = null;
		}

		if (located is null)
		{
			return null;
		}

		try
		{
			return IppNetworkPrintSystem.CreatePrinter(this.context, located, printerId, instanceName);
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(located.PrinterUri, exception);
		}
	}

	/// <inheritdoc/>
	/// <remarks>Network printers have no default, so this always returns <see langword="null"/>.</remarks>
	public Task<IPrinter?> FindDefaultPrinterAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IPrinter?>(null);
	}

	/// <summary>
	/// Releases the HTTP connection, if this print system owns it.
	/// </summary>
	public void Dispose()
	{
		this.ownedClient?.Dispose();
	}

	/// <summary>
	/// Reads an identifier from this print system.
	/// </summary>
	/// <param name="printerId">The identifier.</param>
	/// <param name="printerUri">The printer URI, for a URI identifier; otherwise <see langword="null"/>.</param>
	/// <param name="instanceName">The announced name, for a name identifier; otherwise <see langword="null"/>.</param>
	/// <returns>
	/// Whether the identifier belongs to this print system: <c>ipp:dnssd:</c> and a name, or <c>ipp:</c> and an
	/// <c>ipp</c> or <c>ipps</c> URI.
	/// </returns>
	internal static bool TryParsePrinterId(string printerId, out Uri? printerUri, out string? instanceName)
	{
		printerUri = null;
		instanceName = null;

		if (printerId.StartsWith(IppNetworkPrintSystem.DnsSdPrinterIdPrefix, StringComparison.Ordinal))
		{
			string name = printerId[IppNetworkPrintSystem.DnsSdPrinterIdPrefix.Length..];
			instanceName = name.Length > 0 ? name : null;

			return instanceName is not null;
		}

		if (!printerId.StartsWith(IppNetworkPrintSystem.PrinterIdPrefix, StringComparison.Ordinal))
		{
			return false;
		}

		string uriText = printerId[IppNetworkPrintSystem.PrinterIdPrefix.Length..];
		bool isIppUri = Uri.TryCreate(uriText, UriKind.Absolute, out Uri? uri)
						   && (uri.Scheme == "ipp" || uri.Scheme == "ipps");
		printerUri = isIppUri ? uri : null;

		return isIppUri;
	}

	/// <summary>
	/// Creates the printer for a located printer's attributes.
	/// </summary>
	/// <param name="context">The client and settings shared with the print system.</param>
	/// <param name="located">Where the printer is, and what it reported.</param>
	/// <param name="printerId">The identifier it was looked up by.</param>
	/// <param name="instanceName">The name it was listed under, or <see langword="null"/> for a lookup by URI.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter(IppPrintContext context, LocatedPrinter located, string printerId, string? instanceName)
	{
		Uri locatedUri = located.PrinterUri;
		PrinterDescriptionAttributes attributes = located.Attributes;
		PrinterInfo info = IppPrinterInfoMapper.ToPrinterInfo(attributes, printerId, locatedUri.Host, isDefault: false);

		if (instanceName is not null)
		{
			// Keep the name the printer was listed under, which is also in its identifier
			info = info with { Name = instanceName };
		}

		IReadOnlyCollection<string>? formats = IppValueReader.ReadReference(attributes.DocumentFormatSupported)
			?.Select(format => format.Value)
			.ToList();

		return new IppPrinter(context, locatedUri, info, formats);
	}

	/// <summary>
	/// Puts IPP's default port into a URI that leaves it out, so <c>ipp://host/path</c> and
	/// <c>ipp://host:631/path</c> compare equal. .NET doesn't know the <c>ipp</c> and <c>ipps</c> schemes' port.
	/// </summary>
	/// <param name="printerUri">The URI.</param>
	/// <returns>The URI with an explicit port.</returns>
	private static Uri WithExplicitPort(Uri printerUri)
	{
		const int ippPort = 631; // The default port of both ipp and ipps (RFC 8010, RFC 7472)

		if (printerUri.Port >= 0)
		{
			return printerUri;
		}

		return new UriBuilder(printerUri) { Port = ippPort }.Uri;
	}

	/// <summary>
	/// Checks whether a URI may be looked up: it is configured, a printer announced it, or any URI is allowed.
	/// </summary>
	/// <param name="printerUri">The URI from the printer identifier.</param>
	/// <returns>Whether to contact it.</returns>
	private bool IsKnownPrinterUri(Uri printerUri)
	{
		if (this.allowsAnyPrinterUri)
		{
			return true;
		}

		Uri candidate = IppNetworkPrintSystem.WithExplicitPort(printerUri);

		return this.configuredUris.Any(configured => IppNetworkPrintSystem.WithExplicitPort(configured) == candidate)
			|| this.knownPrinters.Values.Any(known => IppNetworkPrintSystem.WithExplicitPort(known.PrinterUri) == candidate);
	}

	/// <summary>
	/// Checks whether a printer that answered at a remembered address is the one remembered, by comparing its
	/// <c>printer-uuid</c> with the UUID it announced. Printers that don't report both are taken on trust.
	/// </summary>
	/// <param name="known">The remembered announcement.</param>
	/// <param name="attributes">The attributes read at its address.</param>
	/// <returns>Whether the printer is the same, as far as can be told.</returns>
	private static bool IsSamePrinter(DiscoveredIppPrinter known, PrinterDescriptionAttributes attributes)
	{
		string? reportedUuid = DnsSdServiceMapper.NormalizeUuid(IppValueReader.ReadReference(attributes.PrinterUUID));

		return known.Uuid is null
				|| reportedUuid is null
				|| known.Uuid == reportedUuid;
	}

	/// <summary>
	/// Describes a discovered printer from its announcement, identified by its name.
	/// </summary>
	/// <param name="printer">The discovered printer.</param>
	/// <returns>The printer information, with an unknown state.</returns>
	private static PrinterInfo ToPrinterInfo(DiscoveredIppPrinter printer)
	{
		return new PrinterInfo
		{
			Id           = IppNetworkPrintSystem.DnsSdPrinterIdPrefix + printer.InstanceName,
			Name         = printer.InstanceName,
			Location     = printer.Location,
			MakeAndModel = printer.MakeAndModel
		};
	}

	/// <summary>
	/// Describes a configured printer, identified by its URI.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	/// <param name="announcement">The printer's announcement, or <see langword="null"/> if not discovered.</param>
	/// <returns>
	/// The printer information, with an unknown state; named after the announcement if there is one, otherwise the
	/// host.
	/// </returns>
	private static PrinterInfo ToPrinterInfo(Uri printerUri, DiscoveredIppPrinter? announcement)
	{
		return new PrinterInfo
		{
			Id           = IppNetworkPrintSystem.PrinterIdPrefix + printerUri.AbsoluteUri,
			Name         = announcement?.InstanceName ?? printerUri.Host,
			Location     = announcement?.Location,
			MakeAndModel = announcement?.MakeAndModel
		};
	}

	/// <summary>
	/// Finds a printer by name: at the address last seen for that name if it still answers as the same printer,
	/// otherwise wherever a search of the network finds it.
	/// </summary>
	/// <param name="instanceName">The announced name.</param>
	/// <param name="cancellationToken">A token to cancel the lookup.</param>
	/// <returns>The printer's address and attributes, or <see langword="null"/> if it can't be found.</returns>
	/// <exception cref="PrintException">The printer was found by searching but couldn't be read.</exception>
	private async Task<LocatedPrinter?> FindByNameAsync(string instanceName, CancellationToken cancellationToken)
	{
		// The remembered address first: fast, and needs no multicast
		if (this.knownPrinters.TryGetValue(instanceName, out DiscoveredIppPrinter? known))
		{
			PrinterDescriptionAttributes? knownAttributes = await this
				.TryReadKnownAddressAsync(known.PrinterUri, cancellationToken)
				.ConfigureAwait(false);

			if (knownAttributes is not null && IppNetworkPrintSystem.IsSamePrinter(known, knownAttributes))
			{
				return new LocatedPrinter { PrinterUri = known.PrinterUri, Attributes = knownAttributes };
			}
		}

		// Then search the network for the name, and remember where it is now
		DiscoveredIppPrinter? announcement = await this.discovery
			.ResolveAsync(instanceName, this.discoveryDuration, cancellationToken)
			.ConfigureAwait(false);

		if (announcement is null)
		{
			return null;
		}

		this.knownPrinters[instanceName] = announcement;

		return await this.ReadPrinterAsync(announcement.PrinterUri, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads a printer's attributes at a remembered address, giving up quickly if it doesn't answer.
	/// </summary>
	/// <param name="printerUri">The remembered printer URI.</param>
	/// <param name="cancellationToken">A token to cancel the lookup.</param>
	/// <returns>The attributes, or <see langword="null"/> if nothing usable answered in time.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	private async Task<PrinterDescriptionAttributes?> TryReadKnownAddressAsync(Uri printerUri, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(this.KnownAddressTimeout);

		try
		{
			return await IppPrinterQueries.GetAttributesAsync
										   (
											   this.context,
											   printerUri,
											   IppRequestedAttributes.NetworkPrinterAttributes,
											   timeout.Token
										   )
										   .ConfigureAwait(false);
		}
		catch (PrintException)
		{
			// Unreachable, or something there rejected the request: search for the name instead
			return null;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// Too slow to answer: search for the name instead
			return null;
		}
	}

	/// <summary>
	/// Reads a printer's attributes at a URI.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The printer's address and attributes, or <see langword="null"/> if there is no printer there.</returns>
	/// <exception cref="PrintException">The printer couldn't be reached or rejected the request.</exception>
	private async Task<LocatedPrinter?> ReadPrinterAsync(Uri printerUri, CancellationToken cancellationToken)
	{
		PrinterDescriptionAttributes? attributes = await IppPrinterQueries
															.GetAttributesAsync
															(
																this.context,
																printerUri,
																IppRequestedAttributes.NetworkPrinterAttributes,
																cancellationToken
															)
															.ConfigureAwait(false);

		return attributes is null ? null : new LocatedPrinter { PrinterUri = printerUri, Attributes = attributes };
	}
}
