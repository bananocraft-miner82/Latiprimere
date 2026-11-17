using System.Text;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Raw job data for the scenarios.
/// </summary>
public static class RawData
{
	/// <summary>
	/// Creates raw job data that is different every time, so only the job that sent it can match it, with Windows
	/// line endings that must arrive unchanged.
	/// </summary>
	/// <param name="scenarioId">The scenario sending it, written into the data.</param>
	/// <returns>The data.</returns>
	public static byte[] Create(string scenarioId)
	{
		return Encoding.ASCII.GetBytes($"Latiprimere end-to-end scenario {scenarioId} {Guid.NewGuid():N}\r\nSecond line\r\n");
	}
}
