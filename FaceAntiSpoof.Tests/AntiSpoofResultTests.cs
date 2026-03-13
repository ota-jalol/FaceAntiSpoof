using Xunit;
using FaceAntiSpoof.Core.Models;

namespace FaceAntiSpoof.Tests;

/// <summary>
/// Тесты для модели AntiSpoofResult.
/// </summary>
public class AntiSpoofResultTests
{
    /// <summary>
    /// IsReal=true → Label = "Real".
    /// </summary>
    [Fact]
    public void TestLabel_Real()
    {
        // Arrange
        var result = new AntiSpoofResult(isReal: true, realProbability: 0.95f, spoofProbability: 0.05f);

        // Assert
        Assert.Equal("Real", result.Label);
        Assert.True(result.IsReal);
    }

    /// <summary>
    /// IsReal=false → Label = "Spoof".
    /// </summary>
    [Fact]
    public void TestLabel_Spoof()
    {
        // Arrange
        var result = new AntiSpoofResult(isReal: false, realProbability: 0.1f, spoofProbability: 0.9f);

        // Assert
        Assert.Equal("Spoof", result.Label);
        Assert.False(result.IsReal);
    }
}
