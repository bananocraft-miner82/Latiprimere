using System.Globalization;
using System.Runtime.InteropServices;
using Latiprimere.Rendering.Pdfium.Interop;
using Latiprimere.Rendering.Pdfium.Resources;

namespace Latiprimere.Rendering.Pdfium;

/// <summary>
/// Rasterises PDF documents with PDFium.
/// </summary>
/// <remarks>
/// PDFium is not thread-safe, so all PDFium work in the process is serialised: documents can be used from several
/// threads, but only one renders at a time.
/// </remarks>
public sealed unsafe class PdfiumRasterizer : IPdfRasterizer
{
	private const int MaxDocumentBytes = int.MaxValue;   // The documents' length is kept as an int; 2 GB is plenty
	private const int InitialBufferBytes = 64 * 1024;   // For streams whose length isn't known

	/// <inheritdoc/>
	public IRasterDocument Open(Stream content)
	{
		return PdfiumRasterizer.Open(content, PdfiumRasterizer.MaxDocumentBytes);
	}

	/// <summary>
	/// Opens a PDF document for rendering, refusing one longer than the given size.
	/// </summary>
	/// <param name="content">The PDF document, read from its current position.</param>
	/// <param name="maxDocumentBytes">The longest document accepted, in bytes.</param>
	/// <returns>The open document. The caller disposes it.</returns>
	/// <exception cref="PrintException">The document is too large, or PDFium can't open it.</exception>
	/// <remarks>Tests pass a small limit, as reaching 2 GB isn't practical.</remarks>
	internal static IRasterDocument Open(Stream content, int maxDocumentBytes)
	{
		ArgumentNullException.ThrowIfNull(content);

		// PDFium reads the document from memory until it is closed, so copy it somewhere that won't move
		(IntPtr data, int length) = PdfiumRasterizer.CopyToNativeMemory(content, maxDocumentBytes);

		try
		{
			lock (PdfiumLibrary.Lock)
			{
				PdfiumLibrary.EnsureInitialised();
				PdfiumLibrary.ReleasePending();
				IntPtr document = PdfiumNative.LoadMemDocument((byte*)data, (nuint)length, null);

				if (document == IntPtr.Zero)
				{
					throw PdfiumRasterizer.CreateLoadException(PdfiumNative.GetLastError());
				}

				return new PdfiumDocument(document, data, PdfiumNative.GetPageCount(document));
			}
		}
		catch
		{
			// The document never took ownership of the bytes
			NativeMemory.Free((void*)data);

			throw;
		}
	}

	/// <summary>
	/// Reads a stream to the end into newly allocated native memory.
	/// </summary>
	/// <param name="content">The stream.</param>
	/// <param name="maxDocumentBytes">The longest document accepted, in bytes.</param>
	/// <returns>The memory and its length. The caller must free it with <see
	/// cref="NativeMemory.Free(void*)"/>.</returns>
	/// <exception cref="PrintException">The document is longer than <paramref name="maxDocumentBytes"/>.</exception>
	/// <remarks>
	/// The data is read straight into native memory, so the document is held once, not also in a managed buffer.
	/// A seekable stream's remaining length sizes the buffer at once; otherwise it starts small and doubles.
	/// </remarks>
	private static (IntPtr Data, int Length) CopyToNativeMemory(Stream content, int maxDocumentBytes)
	{
		long expected = content.CanSeek ? Math.Max(0, content.Length - content.Position) : 0;

		if (expected > maxDocumentBytes)
		{
			throw PdfiumRasterizer.CreateTooLargeException(maxDocumentBytes);
		}

		// One byte more than a seekable stream's data, so reaching the end needs no second allocation
		nuint capacity = (nuint)Math.Max(expected + 1, PdfiumRasterizer.InitialBufferBytes);
		byte* data = (byte*)NativeMemory.Alloc(capacity);
		long length = 0;

		try
		{
			while (true)
			{
				if ((nuint)length == capacity)
				{
					// Full at the limit is fine: growing to one byte past it shows whether more follows
					if (length > maxDocumentBytes)
					{
						throw PdfiumRasterizer.CreateTooLargeException(maxDocumentBytes);
					}

					// Realloc leaves the old block intact if it fails, so the catch below still frees it
					nuint grown = (nuint)Math.Min((long)capacity * 2, maxDocumentBytes + 1L);
					data = (byte*)NativeMemory.Realloc(data, grown);
					capacity = grown;
				}

				int chunk = (int)Math.Min((long)capacity - length, int.MaxValue);
				int read = content.Read(new Span<byte>(data + length, chunk));

				if (read == 0)
				{
					break;
				}

				length += read;
			}

			if (length > maxDocumentBytes)
			{
				throw PdfiumRasterizer.CreateTooLargeException(maxDocumentBytes);
			}

			return ((IntPtr)data, (int)length);
		}
		catch
		{
			NativeMemory.Free(data);

			throw;
		}
	}

	/// <summary>
	/// Creates the exception for a document too large to open from memory.
	/// </summary>
	/// <param name="maxDocumentBytes">The longest document accepted, in bytes.</param>
	/// <returns>The exception.</returns>
	private static PrintException CreateTooLargeException(int maxDocumentBytes)
	{
		string limit = maxDocumentBytes.ToString("N0", CultureInfo.CurrentCulture);

		return new PrintException(ExceptionMessages.FormatPdfTooLarge(limit));
	}

	/// <summary>
	/// Creates the exception for a document PDFium could not open.
	/// </summary>
	/// <param name="error">The FPDF_ERR_* code.</param>
	/// <returns>The exception, with the PDFium error as its native error code.</returns>
	private static PrintException CreateLoadException(uint error)
	{
		string message = error == PdfiumConstants.ErrorPassword
			? ExceptionMessages.PdfPasswordProtected
			: ExceptionMessages.FormatPdfNotReadable(error);

		return new PrintException(message) { NativeErrorCode = (int)error };
	}
}
