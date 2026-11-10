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

        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);

        string[] paths = Directory.GetFiles(dataDirectory, "*.txt");

        if (paths.Length == 0)
        {
            Console.WriteLine("Нет файлов для обработки.");
            return;
        }

        //--------П2--------
        // Формируем тестовый набор заданий, включая заведомо отсутствующий файл для проверки обработки ошибок
        var testTasks = new List<FileTask>
        {
            new(1, Path.Combine(dataDirectory, "file1.txt")),
            new(2, Path.Combine(dataDirectory, "file2.txt")),
            new(3, Path.Combine(dataDirectory, "missing_file.txt")), // Ошибочное задание
            new(4, Path.Combine(dataDirectory, "file3.txt")),
            new(5, Path.Combine(dataDirectory, "file4.txt"))
        };

        Console.WriteLine($"Найдено уникальных файлов: {paths.Length}");
        Console.WriteLine($"Сформировано тестовых заданий: {testTasks.Count} (включая 1 отсутствующий файл)");
        Console.WriteLine();

        Console.WriteLine("1. Последовательная обработка");
        Console.WriteLine("2. Параллельная обработка (Конвейер)");
        //--------П2--------
        Console.WriteLine("3. Сравнение производительности (Benchmark 1, 2, 4, 8 воркеров)");
        Console.Write("Выберите режим: ");
        string? choice = Console.ReadLine();

        AnalysisReport report;
        switch (choice)
        {
            case "1":
                report = await SequentialProcessor.ProcessAsync(testTasks);
                PrintReport(report);
                await SaveReportAsync(report);
                break;
            case "2":
                Console.Write("Количество обработчиков: ");
                if (!int.TryParse(Console.ReadLine(), out int workerCount) || workerCount < 1)
                {
                    Console.WriteLine("Некорректное значение.");
                    return;
                }
                report = await PipelineProcessor.ProcessAsync(testTasks, workerCount);
                PrintReport(report);
                //--------П2--------
                PrintWorkerStatistics(report);
                await SaveReportAsync(report);
                break;
            //--------П2--------
            case "3":
                await RunBenchmarkAsync(testTasks);
                break;
            default:
                Console.WriteLine("Неизвестный режим.");
                return;
        }
    }

    //--------П2--------
    private static async Task RunBenchmarkAsync(List<FileTask> tasks)
    {
        Console.WriteLine("\n========== СРАВНЕНИЕ ПРОИЗВОДИТЕЛЬНОСТИ ==========");
        Console.WriteLine($"| {"Режим",-12} | {"Обработчики",11} | {"Время (мс)",10} | {"Успешно",7} | {"Ошибок",6} |");
        Console.WriteLine(new string('-', 58));

        // Последовательная обработка
        var seqReport = await SequentialProcessor.ProcessAsync(tasks);
        Console.WriteLine($"| {"Sequential",-12} | {1,11} | {seqReport.ProcessingTimeMs,10} | {seqReport.SuccessfulFiles,7} | {seqReport.FailedFiles,6} |");

        // Параллельная обработка с разным количеством воркеров
        foreach (var workers in new[] { 1, 2, 4, 8 })
        {
            var parReport = await PipelineProcessor.ProcessAsync(tasks, workers);
            Console.WriteLine($"| {"Parallel",-12} | {workers,11} | {parReport.ProcessingTimeMs,10} | {parReport.SuccessfulFiles,7} | {parReport.FailedFiles,6} |");

            PrintWorkerStatistics(parReport);
        }
        Console.WriteLine("==================================================");

        //--------П2--------
        Console.WriteLine("\nОТВЕТЫ НА ВОПРОСЫ:");
        Console.WriteLine("1. Параллельная обработка с 1 воркером может быть медленнее последовательной из-за накладных расходов на создание Channel, Task, переключение контекста и синхронизацию ConcurrentBag.");
        Console.WriteLine("2. Увеличение количества обработчиков не всегда ускоряет программу из-за ограничений пропускной способности диска (I/O bound), конкуренции за ресурсы и роста накладных расходов.");
        Console.WriteLine("3. Больший объём данных делает выигрыш от параллелизма более заметным, так как постоянные накладные расходы 'растворяются' в длительной полезной работе.");
        Console.WriteLine("4. Контроль количества одновременно выполняемых операций важен для предотвращения исчерпания оперативной памяти, дескрипторов файлов и деградации производительности (thrashing).");
    }

    //--------П2--------
    private static void PrintWorkerStatistics(AnalysisReport report)
    {
        Console.WriteLine("  --- Статистика исполнителей ---");
        foreach (var stat in report.WorkerStatistics.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  Worker {stat.Key}: {stat.Value} заданий");
        }
        Console.WriteLine($"  Всего успешно обработано: {report.SuccessfulFiles}");
        Console.WriteLine("  -------------------------------");
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
            Console.WriteLine($"{result.FileName} | Worker: {result.WorkerId} | Строк: {result.Lines} | Слов: {result.Words}");
        }
        if (report.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("ОШИБКИ:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"{error.FileName}: {error.ErrorType} - {error.Message}");
            }
        }
    }

    private static async Task SaveReportAsync(AnalysisReport report)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(report, options);
        string path = Path.Combine(AppContext.BaseDirectory, "report.json");
        await File.WriteAllTextAsync(path, json);
        Console.WriteLine();
        Console.WriteLine($"Отчёт сохранён: {path}");
    }
}