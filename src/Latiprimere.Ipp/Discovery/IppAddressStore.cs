using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// Keeps where each printer found on the network was last seen, in a JSON file, so a printer that sleeps through
/// the next start-up's search is still found at its last address (ADR 0038).
/// </summary>
/// <remarks>
/// Reading never fails: a missing file means starting empty, and an unreadable one is logged and ignored, as are
/// entries that don't make sense. Writing goes to a temporary file first, which then replaces the store, so a
/// reader never sees half a file. A failed write is logged, and the next change tries again.
/// </remarks>
internal sealed class IppAddressStore
{
	/// <summary>The version of the file's format, written into it.</summary>
	internal const int FormatVersion = 1;

	private readonly string path;          // The file
	private readonly ILogger logger;       // Where to log what goes wrong
	private readonly Lock gate = new Lock(); // One write at a time
	private volatile bool keepsNewerFile;  // Whether the file is a later format's, left as it is

	/// <summary>
	/// Initialises a new instance of the <see cref="IppAddressStore"/> class.
	/// </summary>
	/// <param name="path">The file, which needn't exist yet.</param>
	/// <param name="logger">Where to log a file that can't be read or written.</param>
	public IppAddressStore(string path, ILogger logger)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(logger);

		this.path = Path.GetFullPath(path);
		this.logger = logger;
	}

	/// <summary>
	/// Reads the printers remembered.
	/// </summary>
	/// <returns>The printers; empty when there is no file, it can't be read, or a later format wrote it.</returns>
	/// <remarks>
	/// A file in a later format, from a later version of Latiprimere sharing it, is neither read nor written, so this
	/// version doesn't replace it with what it understands. A file without a version is read as the current format.
	/// </remarks>
	public IReadOnlyList<DiscoveredIppPrinter> Load()
	{
		StoredAddresses? stored;

		try
		{
			using FileStream file = File.OpenRead(this.path);
			stored = JsonSerializer.Deserialize(file, IppAddressStoreJsonContext.Default.StoredAddresses);
		}
		catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
		{
			IppLog.AddressStoreMissing(this.logger, this.path);

			return [];
		}
		catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
		{
			IppLog.AddressStoreUnreadable(this.logger, this.path, exception);

			return [];
		}

		if (stored is { Version: > IppAddressStore.FormatVersion })
		{
			this.keepsNewerFile = true;
			IppLog.AddressStoreNewerFormat(this.logger, this.path, stored.Version, IppAddressStore.FormatVersion);

			return [];
		}

		// JSON's null where an entry belongs, as in "printers": [null], arrives as a null entry
		List<DiscoveredIppPrinter> printers = (stored?.Printers ?? [])
			.OfType<StoredPrinter>()
			.Select(IppAddressStore.ToPrinter)
			.OfType<DiscoveredIppPrinter>()
			.ToList();

		IppLog.AddressStoreLoaded(this.logger, this.path, printers.Count);

		return printers;
	}

	/// <summary>
	/// Writes the printers remembered, replacing the file; unless <see cref="Load"/> found a later format's file, which
	/// is left as it is.
	/// </summary>
	/// <param name="printers">
	/// Every printer to remember, read only once this write's turn comes: pass a live view, such as a query over the
	/// remembered printers, so that of two writes at once the later one has the later changes.
	/// </param>
	public void Save(IEnumerable<DiscoveredIppPrinter> printers)
	{
		ArgumentNullException.ThrowIfNull(printers);

		if (this.keepsNewerFile)
		{
			return;
		}

		lock (this.gate)
		{
			// Read inside the lock: a copy taken before it could be older than the one a waiting write has just saved
			StoredAddresses stored = new StoredAddresses
			{
				Version  = IppAddressStore.FormatVersion,
				Printers = printers.OrderBy(printer => printer.InstanceName, StringComparer.OrdinalIgnoreCase).Select(IppAddressStore.ToStored).ToList()
			};

			string temporary = this.path + "." + Guid.NewGuid().ToString("N") + ".tmp";

			try
			{
				string? directory = Path.GetDirectoryName(this.path);

				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				using (FileStream file = File.Create(temporary))
				{
					JsonSerializer.Serialize(file, stored, IppAddressStoreJsonContext.Default.StoredAddresses);
				}

				File.Move(temporary, this.path, overwrite: true);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
			{
				IppLog.AddressStoreNotSaved(this.logger, this.path, exception);
				IppAddressStore.TryDelete(temporary);
			}
		}
	}

	/// <summary>
	/// Turns a stored entry back into a printer, if it makes sense: a name, and absolute <c>ipp</c> or <c>ipps</c>
	/// URIs at hosts a printer may have (ADR 0058), as an earlier version may have stored an announcement of any.
	/// </summary>
	/// <param name="stored">The entry.</param>
	/// <returns>The printer, or <see langword="null"/> to leave the entry out.</returns>
	private static DiscoveredIppPrinter? ToPrinter(StoredPrinter stored)
	{
		Uri? printerUri = IppAddressStore.ToIppUri(stored.PrinterUri);
		Uri? secureUri = IppAddressStore.ToIppUri(stored.SecureUri);

		if (string.IsNullOrWhiteSpace(stored.Name) || printerUri is null || (stored.SecureUri is not null && secureUri is null))
		{
			return null;
		}

		return new DiscoveredIppPrinter
		{
			InstanceName    = stored.Name,
			PrinterUri      = printerUri,
			SecureUri       = secureUri,
			Uuid            = DnsSdServiceMapper.NormalizeUuid(stored.Uuid),
			MakeAndModel    = stored.MakeAndModel,
			Location        = stored.Location,
			LastSeen        = stored.LastSeen,
			ReachedOverIpps = stored.ReachedOverIpps == true
		};
	}

	/// <summary>
	/// Makes the stored entry for a printer.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The entry.</returns>
	private static StoredPrinter ToStored(DiscoveredIppPrinter printer)
	{
		return new StoredPrinter
		{
			Name            = printer.InstanceName,
			PrinterUri      = printer.PrinterUri.AbsoluteUri,
			SecureUri       = printer.SecureUri?.AbsoluteUri,
			Uuid            = printer.Uuid,
			MakeAndModel    = printer.MakeAndModel,
			Location        = printer.Location,
			LastSeen        = printer.LastSeen,
			ReachedOverIpps = printer.ReachedOverIpps ? true : null
		};
	}

	/// <summary>
	/// Reads a stored URI, if it's an absolute <c>ipp</c> or <c>ipps</c> one at a host a printer may have.
	/// </summary>
	/// <param name="value">The stored text, or <see langword="null"/>.</param>
	/// <returns>The URI, or <see langword="null"/>.</returns>
	private static Uri? ToIppUri(string? value)
	{
		return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
			   && uri.Scheme is "ipp" or "ipps"
			   && Connections.PrinterAddressRules.CanBePrinterHost(uri.Host)
			? uri
			: null;
	}

	/// <summary>
	/// Deletes a file if it can, leaving it otherwise.
	/// </summary>
	/// <param name="file">The file.</param>
	private static void TryDelete(string file)
	{
		try
		{
			File.Delete(file);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// Left behind: harmless, and the next write uses another name
		}
	}
}
