using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FileProcessingPipeline.Models;

namespace FileProcessingPipeline.Services;

public static class PipelineProcessor
{
    public static async Task<AnalysisReport> ProcessAsync(
        IEnumerable<FileTask> tasks,
        int workerCount)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (workerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount));
        }

        var taskList = tasks.ToList();

        //-------П1-------
        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<FileAnalysisResult>();
        var errors = new ConcurrentBag<FileProcessingError>();
        var channel = Channel.CreateUnbounded<FileTask>();

        // Производитель: записывает все задания в канал
        var producer = Task.Run(async () =>
        {
            foreach (var task in taskList)
            {
                await channel.Writer.WriteAsync(task);
            }
            // Сообщаем обработчикам, что новых заданий больше не будет
            channel.Writer.TryComplete();
        });

        //-------П1-------
        var workers = Enumerable
            .Range(1, workerCount)
            .Select(id => WorkerAsync(id, channel.Reader, results, errors))
            .ToArray();

        //-------П1-------
        await Task.WhenAll(workers);
        await producer;

        stopwatch.Stop();

        return ReportService.Create(
            $"Pipeline ({workerCount} workers)",
            taskList.Count,
            results.ToList(),
            errors.ToList(),
            workerCount,
            stopwatch.ElapsedMilliseconds);
    }

    //-------П1-------
    private static async Task WorkerAsync(
        int workerId,
        ChannelReader<FileTask> reader,
        ConcurrentBag<FileAnalysisResult> results,
        ConcurrentBag<FileProcessingError> errors)
    {
        await foreach (var task in reader.ReadAllAsync())
        {
            try
            {
                Console.WriteLine($"[WORKER {workerId}] Начал: {task.FilePath}");
                var result = await FileAnalyzer.AnalyzeAsync(task, workerId);
                results.Add(result);

                Console.WriteLine($"[WORKER {workerId}] Завершил: {result.FileName}");
            }
            catch (IOException ex)
            {
                errors.Add(new FileProcessingError(
                    task.Id,
                    Path.GetFileName(task.FilePath),
                    nameof(IOException),
                    ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(new FileProcessingError(
                    task.Id,
                    Path.GetFileName(task.FilePath),
                    nameof(UnauthorizedAccessException),
                    ex.Message));
            }
        }
    }
}