using Microsoft.ML.OnnxRuntime;

namespace FaceAntiSpoof.Core.Infrastructure;

/// <summary>
/// Управляет жизненным циклом ONNX InferenceSession.
/// Сессия создаётся однократно и переиспользуется для всех вызовов инференса.
/// </summary>
public sealed class OnnxSessionManager : IDisposable
{
    private readonly InferenceSession session;

    public InferenceSession Session => session;

    public OnnxSessionManager(string modelPath, bool useDirectML = false)
    {
        var options = new SessionOptions();
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        if (useDirectML)
        {
            options.AppendExecutionProvider_DML(0);
        }

        session = new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Имя входного тензора.
    /// </summary>
    public string InputName => session.InputNames[0];

    /// <summary>
    /// Имена выходных тензоров.
    /// </summary>
    public IReadOnlyList<string> OutputNames => session.OutputNames;

    public void Dispose()
    {
        session.Dispose();
    }
}
