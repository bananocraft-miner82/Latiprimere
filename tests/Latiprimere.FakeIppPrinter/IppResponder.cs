using System.Text.RegularExpressions;
using SharpIpp.Protocol;
using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// Answers IPP requests the way a printer would, keeping its jobs. Not thread-safe: the printer calls it under a lock.
/// </summary>
internal sealed partial class IppResponder
{
	/// <summary>
	/// The printer attributes that depend on how the printer runs, which replace the profile's.
	/// </summary>
	private static readonly HashSet<string> RunningAttributeNames =
	[
		"printer-uri-supported",
		"uri-security-supported",
		"uri-authentication-supported",
		"printer-state",
		"printer-state-reasons",
		"printer-state-message",
		"printer-is-accepting-jobs",
		"queued-job-count",
		"printer-up-time",
		"operations-supported"
	];

	/// <summary>
	/// The attributes Get-Jobs returns for each job unless others are asked for (RFC 8011 section 4.2.6.1).
	/// </summary>
	private static readonly string[] DefaultGetJobsAttributes = ["job-uri", "job-id"];

	private readonly FakePrinterProfile profile;     // The printer's description attributes
	private readonly FakePrinterBehaviour behaviour; // What the printer does
	private readonly bool usesTls;                   // Whether the printer is reached over ipps
	private readonly bool requiresPassword;          // Whether it asks for Basic authentication
	private readonly List<FakeJob> jobs = new List<FakeJob>(); // Every job accepted, in order
	private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow; // For printer-up-time and the job times
	private int nextJobId = 1;                       // The job-id the next job gets

	/// <summary>
	/// Initialises a new instance of the <see cref="IppResponder"/> class.
	/// </summary>
	/// <param name="profile">The printer's description attributes.</param>
	/// <param name="behaviour">What the printer does.</param>
	/// <param name="usesTls">Whether the printer is reached over ipps.</param>
	/// <param name="requiresPassword">Whether it asks for Basic authentication.</param>
	public IppResponder(FakePrinterProfile profile, FakePrinterBehaviour behaviour, bool usesTls, bool requiresPassword)
	{
		this.profile = profile;
		this.behaviour = behaviour;
		this.usesTls = usesTls;
		this.requiresPassword = requiresPassword;
	}

	/// <summary>
	/// Gets or sets the printer's URI, known once the server has started.
	/// </summary>
	public Uri PrinterUri { get; set; } = new Uri("ipp://127.0.0.1/ipp/print");

	/// <summary>
	/// Answers a request.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="document">The document sent with it.</param>
	/// <param name="now">The time now.</param>
	/// <returns>The response.</returns>
	public IppResponseMessage Answer(IIppRequestMessage request, byte[] document, DateTimeOffset now)
	{
		IppResponseMessage response = new IppResponseMessage
		{
			Version = request.Version,
			RequestId = request.RequestId,
			StatusCode = IppStatusCode.SuccessfulOk
		};

		foreach (FakeJob job in this.jobs)
		{
			this.Advance(job, now);
		}

		response.StatusCode = IppResponder.CheckRequest(request) ?? request.IppOperation switch
		{
			IppOperation.GetPrinterAttributes => this.AnswerGetPrinterAttributes(request, response, now),
			IppOperation.PrintJob => this.AnswerPrintJob(request, response, now),
			IppOperation.ValidateJob => this.Validate(request, response),
			IppOperation.GetJobAttributes => this.AnswerGetJobAttributes(request, response, now),
			IppOperation.GetJobs => this.AnswerGetJobs(request, response, now),
			IppOperation.CancelJob => this.AnswerCancelJob(request, now),
			_ => IppStatusCode.ServerErrorOperationNotSupported
		};

		List<IppAttribute> operationAttributes =
		[
			new IppAttribute(Tag.Charset, "attributes-charset", "utf-8"),
			new IppAttribute(Tag.NaturalLanguage, "attributes-natural-language", "en")
		];

		if (response.StatusCode != IppStatusCode.SuccessfulOk)
		{
			operationAttributes.Add(new IppAttribute(Tag.TextWithoutLanguage, "status-message", response.StatusCode.ToString()));
		}

		response.OperationAttributes.Insert(0, operationAttributes);

		return response;
	}

	/// <summary>
	/// Checks what every request needs (RFC 8011 sections 4.1.1 to 4.1.8): a version this printer speaks, a request
	/// ID, <c>attributes-charset</c> and <c>attributes-natural-language</c> first, and <c>printer-uri</c>.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>The error status, or <see langword="null"/> if the request is well formed.</returns>
	private static IppStatusCode? CheckRequest(IIppRequestMessage request)
	{
		if (request.Version.Major is not (1 or 2))
		{
			return IppStatusCode.ServerErrorVersionNotSupported;
		}

		List<IppAttribute> operation = request.OperationAttributes;
		bool startsWithCharsetAndLanguage = operation.Count >= 2
			&& operation[0].Name == "attributes-charset"
			&& operation[1].Name == "attributes-natural-language";

		if (request.RequestId <= 0 || !startsWithCharsetAndLanguage || !operation.Any(attribute => attribute.Name == "printer-uri"))
		{
			return IppStatusCode.ClientErrorBadRequest;
		}

		return null;
	}

	/// <summary>
	/// Answers Get-Printer-Attributes with the profile and the printer's state, keeping to the attributes asked for.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="response">The response to fill.</param>
	/// <param name="now">The time now.</param>
	/// <returns>The status.</returns>
	private IppStatusCode AnswerGetPrinterAttributes(IIppRequestMessage request, IppResponseMessage response, DateTimeOffset now)
	{
		List<IppAttribute> attributes = this.profile.CopyAttributes(IppResponder.RunningAttributeNames);
		int activeJobs = this.jobs.Count(job => !job.IsTerminal);
		bool isProcessing = this.jobs.Any(job => job.State == IppValues.JobProcessing);
		int state = this.behaviour.IsStopped ? IppValues.PrinterStopped : isProcessing ? IppValues.PrinterProcessing : IppValues.PrinterIdle;
		string reason = this.behaviour.IsStopped ? this.behaviour.StoppedReason : "none";

		attributes.Add(new IppAttribute(Tag.Uri, "printer-uri-supported", this.PrinterUri.AbsoluteUri));
		attributes.Add(new IppAttribute(Tag.Keyword, "uri-security-supported", this.usesTls ? "tls" : "none"));
		attributes.Add(new IppAttribute(Tag.Keyword, "uri-authentication-supported", this.requiresPassword ? "basic" : "none"));
		attributes.Add(new IppAttribute(Tag.Enum, "printer-state", state));
		attributes.Add(new IppAttribute(Tag.Keyword, "printer-state-reasons", reason));
		attributes.Add(new IppAttribute(Tag.Boolean, "printer-is-accepting-jobs", true));
		attributes.Add(new IppAttribute(Tag.Integer, "queued-job-count", activeJobs));
		attributes.Add(new IppAttribute(Tag.Integer, "printer-up-time", this.GetUpTime(now)));
		attributes.AddRange(IppValues.SupportedOperations.Select(operation => new IppAttribute(Tag.Enum, "operations-supported", operation)));

		if (this.behaviour.StateMessage is not null)
		{
			attributes.Add(new IppAttribute(Tag.TextWithoutLanguage, "printer-state-message", this.behaviour.StateMessage));
		}

		IReadOnlyList<string>? requested = IppResponder.GetRequestedAttributes(request);
		response.PrinterAttributes.Add(IppAttributeLists.ForWriting(IppResponder.Select(attributes, requested, "printer-description")));

		return IppStatusCode.SuccessfulOk;
	}

	/// <summary>
	/// Answers Print-Job: busy while the behaviour says so, refused when the job asks for something the profile
	/// doesn't support, and otherwise accepted.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="response">The response to fill.</param>
	/// <param name="now">The time now.</param>
	/// <returns>The status.</returns>
	private IppStatusCode AnswerPrintJob(IIppRequestMessage request, IppResponseMessage response, DateTimeOffset now)
	{
		if (this.behaviour.BusyPrintJobs > 0)
		{
			this.behaviour.BusyPrintJobs--;

			return IppStatusCode.ServerErrorBusy;
		}

		IppStatusCode status = this.Validate(request, response);

		if (status != IppStatusCode.SuccessfulOk)
		{
			return status;
		}

		string? jobName = IppResponder.FindValue(request.OperationAttributes, "job-name");
		string userName = IppResponder.FindValue(request.OperationAttributes, "requesting-user-name") ?? "anonymous";
		FakeJob job = new FakeJob(this.nextJobId++, jobName, userName, now);
		this.jobs.Add(job);
		this.Advance(job, now);

		// Print-Job answers with the job's ID, URI and state only (RFC 8011 section 4.2.1.2)
		string[] answered = ["job-uri", "job-id", "job-state", "job-state-reasons", "job-state-message"];
		response.JobAttributes.Add(IppResponder.Select(this.DescribeJob(job, now), answered, defaultGroup: null));

		return IppStatusCode.SuccessfulOk;
	}

	/// <summary>
	/// Answers Get-Job-Attributes, keeping to the attributes asked for.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="response">The response to fill.</param>
	/// <param name="now">The time now.</param>
	/// <returns>The status.</returns>
	private IppStatusCode AnswerGetJobAttributes(IIppRequestMessage request, IppResponseMessage response, DateTimeOffset now)
	{
		FakeJob? job = this.FindJob(request);

		if (job is null)
		{
			return IppStatusCode.ClientErrorNotFound;
		}

		IReadOnlyList<string>? requested = IppResponder.GetRequestedAttributes(request);
		response.JobAttributes.Add(IppResponder.Select(this.DescribeJob(job, now), requested, "job-description"));

		return IppStatusCode.SuccessfulOk;
	}

	/// <summary>
	/// Answers Get-Jobs: the jobs <c>which-jobs</c> asks for (not completed unless it says <c>completed</c> or
	/// <c>all</c>), with <c>job-uri</c> and <c>job-id</c> unless other attributes are asked for.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="response">The response to fill.</param>
	/// <param name="now">The time now.</param>
	/// <returns>The status.</returns>
	private IppStatusCode AnswerGetJobs(IIppRequestMessage request, IppResponseMessage response, DateTimeOffset now)
	{
		string whichJobs = IppResponder.FindValue(request.OperationAttributes, "which-jobs") ?? "not-completed";
		IReadOnlyList<string> requested = IppResponder.GetRequestedAttributes(request) ?? [.. IppResponder.DefaultGetJobsAttributes];

		foreach (FakeJob job in this.jobs)
		{
			bool isWanted = whichJobs switch
			{
				"completed" => job.IsTerminal,
				"all" => true,
				_ => !job.IsTerminal
			};

			if (isWanted)
			{
				response.JobAttributes.Add(IppResponder.Select(this.DescribeJob(job, now), requested, "job-description"));
			}
		}

		return IppStatusCode.SuccessfulOk;
	}

	/// <summary>
	/// Answers Cancel-Job: a job that hasn't ended is cancelled; one that has can't be.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="now">The time now.</param>
	/// <returns>The status.</returns>
	private IppStatusCode AnswerCancelJob(IIppRequestMessage request, DateTimeOffset now)
	{
		FakeJob? job = this.FindJob(request);

		if (job is null)
		{
			return IppStatusCode.ClientErrorNotFound;
		}

		if (job.IsTerminal)
		{
			return IppStatusCode.ClientErrorNotPossible;
		}

		job.State = IppValues.JobCanceled;
		job.StateReason = "job-canceled-by-user";
		job.StateMessage = null;
		job.CompletedAt = now;

		return IppStatusCode.SuccessfulOk;
	}

	/// <summary>
	/// Checks a job's document format and job template attributes against the profile, as Print-Job and
	/// Validate-Job do.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="response">The response, which gets the attributes refused.</param>
	/// <returns>The status.</returns>
	private IppStatusCode Validate(IIppRequestMessage request, IppResponseMessage response)
	{
		if (!this.behaviour.ValidateJobs)
		{
			return IppStatusCode.SuccessfulOk;
		}

		string? format = IppResponder.FindValue(request.OperationAttributes, "document-format");
		IReadOnlyList<string> supportedFormats = this.profile.GetValues("document-format-supported");

		if (format is not null && supportedFormats.Count > 0 && !supportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
		{
			response.UnsupportedAttributes.Add(request.OperationAttributes.Where(attribute => attribute.Name == "document-format").ToList());

			return IppStatusCode.ClientErrorDocumentFormatNotSupported;
		}

		List<IppAttribute> unsupported = new List<IppAttribute>();

		foreach (List<IppAttribute> entry in IppAttributeLists.Group(request.JobAttributes))
		{
			if (!this.IsSupported(IppAttributeLists.Describe(entry)))
			{
				unsupported.AddRange(entry);
			}
		}

		if (unsupported.Count > 0)
		{
			response.UnsupportedAttributes.Add(IppAttributeLists.ForWriting(unsupported));

			return IppStatusCode.ClientErrorAttributesOrValuesNotSupported;
		}

		return IppStatusCode.SuccessfulOk;
	}

	/// <summary>
	/// Checks one job template attribute against the profile: copies against <c>copies-supported</c>, the tray in
	/// <c>media-col</c> against <c>media-source-supported</c>, and keywords and enums against the matching
	/// <c>*-supported</c> attribute. Anything else is accepted.
	/// </summary>
	/// <param name="requested">The attribute.</param>
	/// <returns><see langword="true"/> if the printer supports it.</returns>
	private bool IsSupported(IppAttributeRecord requested)
	{
		if (requested.Name == "copies")
		{
			IReadOnlyList<string> copiesSupported = this.profile.GetValues("copies-supported");
			string[] range = copiesSupported.Count > 0 ? copiesSupported[0].Split('-') : [];

			return range.Length != 2
				|| !int.TryParse(requested.Value, out int copies)
				|| (copies >= int.Parse(range[0], System.Globalization.CultureInfo.InvariantCulture)
					&& copies <= int.Parse(range[1], System.Globalization.CultureInfo.InvariantCulture));
		}

		if (requested.Name == "media-col")
		{
			Match source = IppResponder.MediaSourcePattern().Match(requested.Value);
			IReadOnlyList<string> sources = this.profile.GetValues("media-source-supported");

			return !source.Success || sources.Count == 0 || sources.Contains(source.Groups[1].Value);
		}

		if (requested.Tag is not (Tag.Keyword or Tag.Enum))
		{
			return true;
		}

		IReadOnlyList<string> supported = this.profile.GetValues(requested.Name + "-supported");

		return supported.Count == 0 || requested.Values.All(supported.Contains);
	}

	/// <summary>
	/// Moves a job on: it waits while the printer is stopped, processes for the behaviour's processing time, then
	/// ends as the behaviour says.
	/// </summary>
	/// <param name="job">The job.</param>
	/// <param name="now">The time now.</param>
	private void Advance(FakeJob job, DateTimeOffset now)
	{
		if (job.IsTerminal)
		{
			return;
		}

		if (this.behaviour.IsStopped)
		{
			job.State = job.ProcessingStartedAt is null ? IppValues.JobPending : IppValues.JobProcessingStopped;
			job.StateReason = "printer-stopped";

			return;
		}

		job.ProcessingStartedAt ??= now;

		if (now - job.ProcessingStartedAt.Value < this.behaviour.ProcessingTime)
		{
			job.State = IppValues.JobProcessing;
			job.StateReason = "job-printing";

			return;
		}

		bool isAborted = this.behaviour.JobOutcome == FakeJobOutcome.Aborted;
		job.State = isAborted ? IppValues.JobAborted : IppValues.JobCompleted;
		job.StateReason = isAborted ? "aborted-by-system" : "job-completed-successfully";
		job.StateMessage = this.behaviour.JobOutcomeMessage;
		job.CompletedAt = now;
	}

	/// <summary>
	/// Describes a job: every attribute RFC 8011 requires of a printer, plus <c>job-name</c> and
	/// <c>job-state-message</c> when it has them.
	/// </summary>
	/// <param name="job">The job.</param>
	/// <param name="now">The time now.</param>
	/// <returns>Its attributes.</returns>
	private List<IppAttribute> DescribeJob(FakeJob job, DateTimeOffset now)
	{
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Integer, "job-id", job.Id),
			new IppAttribute(Tag.Uri, "job-uri", $"{this.PrinterUri.AbsoluteUri}/{job.Id}"),
			new IppAttribute(Tag.Uri, "job-printer-uri", this.PrinterUri.AbsoluteUri),
			new IppAttribute(Tag.Enum, "job-state", job.State),
			new IppAttribute(Tag.Keyword, "job-state-reasons", job.StateReason),
			new IppAttribute(Tag.NameWithoutLanguage, "job-originating-user-name", job.UserName),
			new IppAttribute(Tag.Integer, "job-printer-up-time", this.GetUpTime(now)),
			new IppAttribute(Tag.Integer, "time-at-creation", this.GetUpTime(job.CreatedAt)),
			this.CreateTime("time-at-processing", job.ProcessingStartedAt),
			this.CreateTime("time-at-completed", job.CompletedAt)
		];

		if (job.StateMessage is not null)
		{
			attributes.Add(new IppAttribute(Tag.TextWithoutLanguage, "job-state-message", job.StateMessage));
		}

		if (job.Name is not null)
		{
			attributes.Add(new IppAttribute(Tag.NameWithoutLanguage, "job-name", job.Name));
		}

		return attributes;
	}

	/// <summary>
	/// Gets the printer's up time at a moment, in seconds since it started, as IPP's job times count.
	/// </summary>
	/// <param name="moment">The moment.</param>
	/// <returns>The seconds, at least 1.</returns>
	private int GetUpTime(DateTimeOffset moment)
	{
		return Math.Max(1, (int)(moment - this.startedAt).TotalSeconds + 1);
	}

	/// <summary>
	/// Creates a job time attribute, which has no value until the moment it records has happened.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <param name="moment">The moment, or <see langword="null"/> if it hasn't happened.</param>
	/// <returns>The attribute.</returns>
	private IppAttribute CreateTime(string name, DateTimeOffset? moment)
	{
		return moment is null
			? new IppAttribute(Tag.NoValue, name, NoValue.Instance)
			: new IppAttribute(Tag.Integer, name, this.GetUpTime(moment.Value));
	}

	/// <summary>
	/// Gets the names in a request's <c>requested-attributes</c>.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>The names, or <see langword="null"/> if the request didn't ask for any.</returns>
	private static List<string>? GetRequestedAttributes(IIppRequestMessage request)
	{
		List<string> names = request.OperationAttributes
			.Where(attribute => attribute.Name == "requested-attributes")
			.Select(attribute => IppAttributeLists.FormatValue(attribute.Value))
			.ToList();

		return names.Count > 0 ? names : null;
	}

	/// <summary>
	/// Keeps the attributes asked for: all of them when nothing was asked for, or <c>all</c> or the given group was;
	/// the <c>job-template</c> group's <c>-default</c>, <c>-supported</c> and <c>-ready</c> attributes when that was;
	/// and otherwise those named.
	/// </summary>
	/// <param name="attributes">The attributes, flat.</param>
	/// <param name="requested">The names asked for, or <see langword="null"/>.</param>
	/// <param name="defaultGroup">The group that means every attribute here, such as <c>printer-description</c>, or
	/// <see langword="null"/>.</param>
	/// <returns>The attributes kept, flat.</returns>
	private static List<IppAttribute> Select(List<IppAttribute> attributes, IReadOnlyList<string>? requested, string? defaultGroup)
	{
		if (requested is null || requested.Contains("all") || (defaultGroup is not null && requested.Contains(defaultGroup)))
		{
			return attributes;
		}

		bool wantsJobTemplate = requested.Contains("job-template");

		return IppAttributeLists.Group(attributes)
			.Where
			(
				entry => requested.Contains(entry[0].Name)
					|| (wantsJobTemplate && (entry[0].Name.EndsWith("-default", StringComparison.Ordinal)
						|| entry[0].Name.EndsWith("-supported", StringComparison.Ordinal)
						|| entry[0].Name.EndsWith("-ready", StringComparison.Ordinal)))
			)
			.SelectMany(entry => entry)
			.ToList();
	}

	/// <summary>
	/// Finds the job a request names, by <c>job-id</c> or by the number at the end of <c>job-uri</c>.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>The job, or <see langword="null"/> if there is none.</returns>
	private FakeJob? FindJob(IIppRequestMessage request)
	{
		string? jobUri = IppResponder.FindValue(request.OperationAttributes, "job-uri");
		string? idText = IppResponder.FindValue(request.OperationAttributes, "job-id")
			?? jobUri?[(jobUri.LastIndexOf('/') + 1)..];

		return int.TryParse(idText, out int id) ? this.jobs.FirstOrDefault(job => job.Id == id) : null;
	}

	/// <summary>
	/// Finds an attribute's first value as text.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The value, or <see langword="null"/> if absent.</returns>
	private static string? FindValue(IEnumerable<IppAttribute> attributes, string name)
	{
		foreach (IppAttribute attribute in attributes)
		{
			if (attribute.Name == name)
			{
				return IppAttributeLists.FormatValue(attribute.Value);
			}
		}

		return null;
	}

	/// <summary>
	/// Finds the tray in a <c>media-col</c> value as <see cref="IppAttributeLists"/> formats it.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex(@"media-source=([^ }]+)")]
	private static partial Regex MediaSourcePattern();
}
