namespace FaceAntiSpoof.Core.Models;

/// <summary>
/// Результат классификации лица: реальное или спуфинг.
/// </summary>
public sealed class AntiSpoofResult
{
    /// <summary>
    /// True — реальное лицо, False — спуфинг-атака.
    /// </summary>
    public bool IsReal { get; init; }

    /// <summary>
    /// Вероятность того, что лицо реальное (0.0 – 1.0).
    /// </summary>
    public float RealProbability { get; init; }

    /// <summary>
    /// Вероятность спуфинга (0.0 – 1.0).
    /// </summary>
    public float SpoofProbability { get; init; }

    /// <summary>
    /// Текстовая метка результата.
    /// </summary>
    public string Label => IsReal ? "Real" : "Spoof";

    public AntiSpoofResult(bool isReal, float realProbability, float spoofProbability)
    {
        IsReal = isReal;
        RealProbability = realProbability;
        SpoofProbability = spoofProbability;
    }
}
