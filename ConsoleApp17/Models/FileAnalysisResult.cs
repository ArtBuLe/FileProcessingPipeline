namespace FileProcessingPipeline.Models;

// Дополнительное свойство `WorkerId` позволяет определить, какой исполнитель обработал файл.
public sealed record FileAnalysisResult(
    int TaskId,
    string FileName,
    int Lines,
    int Words,
    long Characters,
    long ProcessingTimeMs,
    int WorkerId
);