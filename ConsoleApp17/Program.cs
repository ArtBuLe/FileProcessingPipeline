using System.Text.Json;
using FileProcessingPipeline.Models;
using FileProcessingPipeline.Services;

namespace FileProcessingPipeline;

internal class Program
{
    private static async Task Main()
    {
        Console.WriteLine("================================");
        Console.WriteLine("     FILE PROCESSING PIPELINE");
        Console.WriteLine("================================");

        string dataDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data");
        Directory.CreateDirectory(dataDirectory);

        string[] paths = Directory.GetFiles(
            dataDirectory,
            "*.txt");

        if (paths.Length == 0)
        {
            Console.WriteLine("Нет файлов для обработки.");
            return;
        }

        //-------П1-------
        var tasks = Enumerable.Range(1, 10)
            .Select(i => new FileTask(i, paths[(i - 1) % paths.Length]))
            .ToList();

        Console.WriteLine($"Найдено уникальных файлов: {paths.Length}");
        Console.WriteLine($"Сформировано заданий для обработки: {tasks.Count}");
        Console.WriteLine();

        Console.WriteLine("1. Последовательная обработка");
        Console.WriteLine("2. Параллельная обработка");
        Console.Write("Выберите режим: ");
        string? choice = Console.ReadLine();

        AnalysisReport report;
        switch (choice)
        {
            case "1":
                report = await SequentialProcessor.ProcessAsync(tasks);
                break;
            case "2":
                Console.Write("Количество обработчиков: ");
                if (!int.TryParse(Console.ReadLine(), out int workerCount) || workerCount < 1)
                {
                    Console.WriteLine("Некорректное значение.");
                    return;
                }
                report = await PipelineProcessor.ProcessAsync(tasks, workerCount);
                break;
            default:
                Console.WriteLine("Неизвестный режим.");
                return;
        }

        PrintReport(report);
        await SaveReportAsync(report);
    }

    private static void PrintReport(AnalysisReport report)
    {
        Console.WriteLine();
        Console.WriteLine("========== ОТЧЁТ ==========");
        Console.WriteLine($"Режим: {report.Mode}");
        Console.WriteLine($"Всего файлов: {report.TotalFiles}");
        Console.WriteLine($"Успешно: {report.SuccessfulFiles}");
        Console.WriteLine($"Ошибок: {report.FailedFiles}");
        Console.WriteLine($"Обработчиков: {report.WorkerCount}");
        Console.WriteLine($"Строк: {report.TotalLines}");
        Console.WriteLine($"Слов: {report.TotalWords}");
        Console.WriteLine($"Символов: {report.TotalCharacters}");
        Console.WriteLine($"Время: {report.ProcessingTimeMs} мс");
        Console.WriteLine();
        Console.WriteLine("РЕЗУЛЬТАТЫ:");
        foreach (var result in report.Results)
        {
            Console.WriteLine(
                $"{result.FileName} | " +
                $"Worker: {result.WorkerId} | " +
                $"Строк: {result.Lines} | " +
                $"Слов: {result.Words}");
        }
        if (report.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("ОШИБКИ:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine(
                    $"{error.FileName}: {error.ErrorType} - {error.Message}");
            }
        }
    }

    private static async Task SaveReportAsync(AnalysisReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(report, options);
        string path = Path.Combine(AppContext.BaseDirectory, "report.json");
        await File.WriteAllTextAsync(path, json);
        Console.WriteLine();
        Console.WriteLine($"Отчёт сохранён: {path}");
    }
}