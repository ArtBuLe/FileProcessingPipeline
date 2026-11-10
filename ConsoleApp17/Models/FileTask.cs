namespace FileProcessingPipeline.Models;

// Каждое задание будет содержать идентификатор и путь к файлу
// Это позволяет передавать между компонентами самостоятельные объекты заданий, а не просто строки.
public sealed record FileTask(
    int Id,
    string FilePath
);