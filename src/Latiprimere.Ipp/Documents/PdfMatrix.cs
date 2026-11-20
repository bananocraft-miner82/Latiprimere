namespace Latiprimere.Ipp.Documents;

/// <summary>
/// A PDF transformation matrix, <c>[A B C D E F]</c>, mapping a point (x, y) to (A·x + C·y + E, B·x + D·y + F)
/// (ISO 32000-1, 8.3.3).
/// </summary>
/// <param name="A">How x scales into x.</param>
/// <param name="B">How x goes into y.</param>
/// <param name="C">How y goes into x.</param>
/// <param name="D">How y scales into y.</param>
/// <param name="E">The move along x.</param>
/// <param name="F">The move along y.</param>
internal readonly record struct PdfMatrix(double A, double B, double C, double D, double E, double F);
