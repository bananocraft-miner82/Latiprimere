namespace Latiprimere.Tests.Builders;

/// <summary>
/// Builds <see cref="PrinterInfo"/> instances with sensible defaults for tests.
/// </summary>
internal sealed class PrinterInfoBuilder
{
	private string id = "printer-1";          // Printer identifier
	private string name = "Test Printer";     // Printer display name
	private bool isDefault = false;           // Whether the printer is its print system's default

	/// <summary>
	/// Sets the identifier of the printer being built.
	/// </summary>
	/// <param name="id">The printer identifier.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterInfoBuilder WithId(string id)
	{
		this.id = id;

		return this;
	}

	/// <summary>
	/// Sets the display name of the printer being built.
	/// </summary>
	/// <param name="name">The printer name.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrinterInfoBuilder WithName(string name)
	{
		this.name = name;

		return this;
	}

	/// <summary>
	/// Marks the printer being built as its print system's default printer.
	/// </summary>
	/// <returns>This builder, for chaining.</returns>
	public PrinterInfoBuilder AsDefault()
	{
		this.isDefault = true;

		return this;
	}

	/// <summary>
	/// Builds and returns the configured <see cref="PrinterInfo"/>.
	/// </summary>
	/// <returns>The printer information.</returns>
	public PrinterInfo Build()
	{
		return new PrinterInfo
		{
			Id        = this.id,
			Name      = this.name,
			State     = PrinterState.Idle,
			IsDefault = this.isDefault
		};
	}
}
