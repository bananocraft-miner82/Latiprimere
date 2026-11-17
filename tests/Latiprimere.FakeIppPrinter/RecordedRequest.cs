using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// A request the fake printer received, and how it answered.
/// </summary>
public sealed class RecordedRequest
{
	/// <summary>
	/// Gets the IPP operation.
	/// </summary>
	public required IppOperation Operation { get; init; }

	/// <summary>
	/// Gets the request's ID.
	/// </summary>
	public required int RequestId { get; init; }

	/// <summary>
	/// Gets the operation attributes, in the order sent.
	/// </summary>
	public required IReadOnlyList<IppAttributeRecord> OperationAttributes { get; init; }

	/// <summary>
	/// Gets the job template attributes, in the order sent.
	/// </summary>
	public required IReadOnlyList<IppAttributeRecord> JobAttributes { get; init; }

	/// <summary>
	/// Gets the document sent with the request; empty for requests without one.
	/// </summary>
	public required byte[] Document { get; init; }

	/// <summary>
	/// Gets a value indicating whether the request carried an HTTP <c>Authorization</c> header.
	/// </summary>
	public bool HasAuthorization { get; init; }

	/// <summary>
	/// Gets the HTTP status of the answer: 200 for an IPP answer, 401 for a request without the right credentials,
	/// or 0 when the connection was dropped without an answer.
	/// </summary>
	public int HttpStatus { get; init; }

	/// <summary>
	/// Gets the IPP status of the answer, or <see langword="null"/> when there was no IPP answer.
	/// </summary>
	public IppStatusCode? StatusCode { get; init; }

	/// <summary>
	/// Gets when the request arrived.
	/// </summary>
	public DateTimeOffset ReceivedAt { get; init; }

	/// <summary>
	/// Finds an attribute by name, among the operation attributes and then the job attributes.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The attribute, or <see langword="null"/> if the request didn't have it.</returns>
	public IppAttributeRecord? Find(string name)
	{
		return this.OperationAttributes.FirstOrDefault(attribute => attribute.Name == name)
			?? this.JobAttributes.FirstOrDefault(attribute => attribute.Name == name);
	}

	/// <summary>
	/// Shows the request on one line: operation, answer and attributes.
	/// </summary>
	/// <returns>The text.</returns>
	public override string ToString()
	{
		string answer = this.StatusCode?.ToString() ?? (this.HttpStatus == 0 ? "connection dropped" : $"HTTP {this.HttpStatus}");
		string document = this.Document.Length > 0 ? $"; {this.Document.Length} document bytes" : string.Empty;
		string jobAttributes = this.JobAttributes.Count > 0 ? "; " + string.Join("; ", this.JobAttributes) : string.Empty;

		return $"{this.Operation} -> {answer}{jobAttributes}{document}";
	}
}
