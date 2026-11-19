using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Testing.Fakes;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppVendorOptionMapperTests
{
	[Fact]
	public async Task GivenHpEnvyAttributesWhenToVendorOptionsThenListsQualityAndResolutionWithDefaults()
	{
		// Arrange — a real printer's response
		IReadOnlyList<IppAttribute> attributes = await IppVendorOptionMapperTests.LoadAsync("hp-envy-5640");

		// Act
		IReadOnlyList<VendorOption> options = IppVendorOptionMapper.ToVendorOptions(attributes);

		// Assert
		VendorOption quality = Assert.Single(options, option => option.Key == "ipp:print-quality");
		Assert.Equal("Print quality", quality.DisplayName);
		Assert.Equal(VendorOptionKind.Choice, quality.Kind);
		Assert.Equal(["3", "4", "5"], quality.Choices.Select(choice => choice.Value));
		Assert.Equal(["Draft", "Normal", "High"], quality.Choices.Select(choice => choice.DisplayName));
		Assert.Equal("4", quality.DefaultValue);
		Assert.False(quality.AllowsSeveralValues);

		VendorOption resolution = Assert.Single(options, option => option.Key == "ipp:printer-resolution");
		Assert.Equal(["300dpi", "600dpi", "1200dpi"], resolution.Choices.Select(choice => choice.Value));
		Assert.Equal("600dpi", resolution.DefaultValue);

		VendorOption optimise = Assert.Single(options, option => option.Key == "ipp:print-content-optimize");
		Assert.Contains(optimise.Choices, choice => choice is { Value: "text-and-graphics", DisplayName: "Text and graphics" });
	}

	[Theory]
	[InlineData("hp-envy-5640")]
	[InlineData("ippeveprinter")]
	[InlineData("cups-virtual-ipp")]
	[InlineData("cups-raw")]
	public async Task GivenCapturedAttributesWhenToVendorOptionsThenLeavesOutWhatTypedOptionsAndRequestsSend(string capture)
	{
		// Arrange — copies, sides and the rest have typed options; collections and page ranges can't be expressed
		IReadOnlyList<IppAttribute> attributes = await IppVendorOptionMapperTests.LoadAsync(capture);

		// Act
		IReadOnlyList<VendorOption> options = IppVendorOptionMapper.ToVendorOptions(attributes);

		// Assert
		Assert.NotEmpty(options);
		string[] excluded =
		[
			"ipp:copies", "ipp:sides", "ipp:media", "ipp:media-col", "ipp:media-source", "ipp:orientation-requested",
			"ipp:print-color-mode", "ipp:job-name", "ipp:ipp-attribute-fidelity", "ipp:page-ranges", "ipp:finishings-col",
			"ipp:document-format", "ipp:document-name", "ipp:overrides", "ipp:media-type"
		];

		Assert.DoesNotContain(options, option => excluded.Contains(option.Key));
		Assert.Equal(options.Select(option => option.Key).Order(StringComparer.Ordinal), options.Select(option => option.Key));
	}

	[Theory]
	[InlineData("hp-envy-5640")]
	[InlineData("ippeveprinter")]
	[InlineData("cups-virtual-ipp")]
	[InlineData("cups-raw")]
	public async Task GivenListedChoicesWhenSentAsVendorOptionsThenEachIsAcceptedWithTheTagItWasListedFrom(string capture)
	{
		// Arrange — every value listed must go back to the printer as it came
		IReadOnlyList<IppAttribute> attributes = await IppVendorOptionMapperTests.LoadAsync(capture);
		IReadOnlyList<VendorOption> options = IppVendorOptionMapper.ToVendorOptions(attributes);

		foreach (VendorOption option in options.Where(option => option.Kind == VendorOptionKind.Choice))
		{
			string name = option.Key["ipp:".Length..];
			HashSet<Tag> listedTags = attributes.Where(attribute => attribute.Name == name + "-supported").Select(attribute => attribute.Tag).ToHashSet();

			foreach (VendorOptionChoice choice in option.Choices)
			{
				// Act
				IppVendorAttributes sent = IppVendorAttributeMapper.ToAttributes
				(
					new Dictionary<string, string> { [option.Key] = choice.Value },
					IppVendorAttributeMapper.CupsPrefixes,
					new PrintJobRequest()
				);

				// Assert — a name may go as a keyword when it looks like one, which IPP allows for these attributes
				IppAttribute value = Assert.Single(sent.Job.Concat(sent.Operation));
				Assert.Equal(name, value.Name);
				Assert.True
				(
					listedTags.Contains(value.Tag) || (value.Tag == Tag.Keyword && listedTags.Contains(Tag.NameWithoutLanguage)),
					$"{option.Key}={choice.Value} was sent as {value.Tag}, but listed as {string.Join(", ", listedTags)}."
				);
			}
		}
	}

	[Fact]
	public async Task GivenCupsQueueAttributesWhenToVendorOptionsThenPriorityIsANumberFromOneToAHundred()
	{
		// Arrange
		IReadOnlyList<IppAttribute> attributes = await IppVendorOptionMapperTests.LoadAsync("cups-virtual-ipp");

		// Act
		IReadOnlyList<VendorOption> options = IppVendorOptionMapper.ToVendorOptions(attributes);

		// Assert — job-priority-supported is the number of levels, not the highest value
		VendorOption priority = Assert.Single(options, option => option.Key == "ipp:job-priority");
		Assert.Equal(VendorOptionKind.Number, priority.Kind);
		Assert.Equal(1, priority.MinimumValue);
		Assert.Equal(100, priority.MaximumValue);
		Assert.Equal("50", priority.DefaultValue);
		Assert.Empty(priority.Choices);

		VendorOption finishings = Assert.Single(options, option => option.Key == "ipp:finishings");
		Assert.True(finishings.AllowsSeveralValues);
		Assert.Contains(finishings.Choices, choice => choice is { Value: "3", DisplayName: "None" });
	}

	[Fact]
	public void GivenNoJobCreationListWhenToVendorOptionsThenListsAttributesWithSupportedAndDefaultValues()
	{
		// Arrange — an older printer: no job-creation-attributes-supported, a collection with a member named like an
		// attribute, a range, a one-value boolean, and a description attribute with no default
		IppAttribute[] attributes =
		[
			new IppAttribute(Tag.Enum, "finishings-supported", 3),
			new IppAttribute(Tag.Enum, "finishings-supported", 4),
			new IppAttribute(Tag.Enum, "finishings-supported", 5),
			new IppAttribute(Tag.Enum, "finishings-default", 3),
			new IppAttribute(Tag.BegCollection, "media-col-default", NoValue.Instance),
			new IppAttribute(Tag.MemberAttrName, string.Empty, "media-type"),
			new IppAttribute(Tag.Keyword, "media-type", "stationery"),
			new IppAttribute(Tag.EndCollection, string.Empty, NoValue.Instance),
			new IppAttribute(Tag.Keyword, "media-type-supported", "stationery"),
			new IppAttribute(Tag.RangeOfInteger, "x-vendor-darkness-supported", new SharpIpp.Protocol.Models.Range(-5, 5)),
			new IppAttribute(Tag.Integer, "x-vendor-darkness-default", 0),
			new IppAttribute(Tag.Boolean, "x-vendor-flag-supported", true),
			new IppAttribute(Tag.Boolean, "x-vendor-flag-default", true),
			new IppAttribute(Tag.Keyword, "charset-supported", "utf-8"),
		];

		// Act
		IReadOnlyList<VendorOption> options = IppVendorOptionMapper.ToVendorOptions(attributes);

		// Assert — media-type has no top-level default, and the one-value boolean says only that it is supported
		Assert.Equal(["ipp:finishings", "ipp:x-vendor-darkness"], options.Select(option => option.Key));
		Assert.Equal(["None", "Staple", "Punch"], options[0].Choices.Select(choice => choice.DisplayName));
		Assert.Equal(VendorOptionKind.Number, options[1].Kind);
		Assert.Equal(-5, options[1].MinimumValue);
		Assert.Equal(5, options[1].MaximumValue);
		Assert.Equal("0", options[1].DefaultValue);
	}

	[Fact]
	public void GivenBooleanWithBothValuesWhenToVendorOptionsThenOffersYesAndNo()
	{
		// Arrange
		IppAttribute[] attributes =
		[
			new IppAttribute(Tag.Keyword, IppVendorOptionMapper.JobCreationAttributesSupported, "x-vendor-flag"),
			new IppAttribute(Tag.Boolean, "x-vendor-flag-supported", true),
			new IppAttribute(Tag.Boolean, "x-vendor-flag-supported", false),
			new IppAttribute(Tag.Boolean, "x-vendor-flag-default", false),
		];

		// Act
		VendorOption option = Assert.Single(IppVendorOptionMapper.ToVendorOptions(attributes));

		// Assert
		Assert.Equal(["true", "false"], option.Choices.Select(choice => choice.Value));
		Assert.Equal(["Yes", "No"], option.Choices.Select(choice => choice.DisplayName));
		Assert.Equal("false", option.DefaultValue);
	}

	[Fact]
	public void GivenTextValuesWhenToVendorOptionsThenLeavesThemOut()
	{
		// Arrange — sent back, text would go as a name, which a strict printer refuses
		IppAttribute[] attributes =
		[
			new IppAttribute(Tag.Keyword, IppVendorOptionMapper.JobCreationAttributesSupported, "x-vendor-note"),
			new IppAttribute(Tag.TextWithoutLanguage, "x-vendor-note-supported", "Confidential"),
			new IppAttribute(Tag.TextWithoutLanguage, "x-vendor-note-default", "Confidential"),
		];

		// Act
		IReadOnlyList<VendorOption> options = IppVendorOptionMapper.ToVendorOptions(attributes);

		// Assert
		Assert.Empty(options);
	}

	/// <summary>
	/// Decodes a captured Get-Printer-Attributes response through the real IPP client, as raw attributes.
	/// </summary>
	/// <param name="name">The capture's name, without <c>-printer-attributes.ipp</c>.</param>
	/// <returns>The attributes.</returns>
	private static async Task<IReadOnlyList<IppAttribute>> LoadAsync(string name)
	{
		string path = Path.Combine(AppContext.BaseDirectory, "Data", name + "-printer-attributes.ipp");
		byte[] response = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
		using HttpClient httpClient = new HttpClient(new ReplayHttpMessageHandler(response));
		using SharpIppClientAdapter client = new SharpIppClientAdapter(httpClient);

		GetPrinterAttributesRequest request = new GetPrinterAttributesRequest
		{
			OperationAttributes = new GetPrinterAttributesOperationAttributes
			{
				PrinterUri          = new IppValue<Uri>(new Uri("ipp://localhost:631/printers/Replay")),
				RequestingUserName  = new StringWithLanguage("test"),
				RequestedAttributes = new IppValue<string[]>(["all"])
			}
		};

		return await client.GetPrinterAttributeListAsync(request, TestContext.Current.CancellationToken);
	}
}
