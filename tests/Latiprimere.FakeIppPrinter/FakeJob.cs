namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// A job the fake printer accepted. Its state moves on when the printer is asked about it.
/// </summary>
internal sealed class FakeJob
{
	/// <summary>
	/// Initialises a new instance of the <see cref="FakeJob"/> class.
	/// </summary>
	/// <param name="id">The <c>job-id</c>.</param>
	/// <param name="name">The <c>job-name</c>, or <see langword="null"/>.</param>
	/// <param name="userName">The <c>requesting-user-name</c> it was sent with.</param>
	/// <param name="createdAt">When it arrived.</param>
	public FakeJob(int id, string? name, string userName, DateTimeOffset createdAt)
	{
		this.Id = id;
		this.Name = name;
		this.UserName = userName;
		this.CreatedAt = createdAt;
	}

	/// <summary>
	/// Gets the <c>job-id</c>.
	/// </summary>
	public int Id { get; }

	/// <summary>
	/// Gets the <c>job-name</c>, or <see langword="null"/>.
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Gets the user who sent it, as <c>job-originating-user-name</c>.
	/// </summary>
	public string UserName { get; }

	/// <summary>
	/// Gets when it arrived.
	/// </summary>
	public DateTimeOffset CreatedAt { get; }

	/// <summary>
	/// Gets or sets when it ended, or <see langword="null"/> while it hasn't.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the <c>job-state</c>.
	/// </summary>
	public int State { get; set; } = IppValues.JobPending;

	/// <summary>
	/// Gets or sets the <c>job-state-reasons</c> keyword.
	/// </summary>
	public string StateReason { get; set; } = "none";

	/// <summary>
	/// Gets or sets the <c>job-state-message</c>, or <see langword="null"/>.
	/// </summary>
	public string? StateMessage { get; set; }

	/// <summary>
	/// Gets or sets when the job started processing, or <see langword="null"/> while it waits.
	/// </summary>
	public DateTimeOffset? ProcessingStartedAt { get; set; }

	/// <summary>
	/// Gets a value indicating whether the job has ended: completed, aborted or cancelled.
	/// </summary>
	public bool IsTerminal
	{
		get
		{
			return this.State is IppValues.JobCanceled or IppValues.JobAborted or IppValues.JobCompleted;
		}
	}
}
