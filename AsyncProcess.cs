namespace libreoffice_test;

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Process helper with asynchronous interface
/// - Based on https://gist.github.com/georg-jung/3a8703946075d56423e418ea76212745
/// - And on https://stackoverflow.com/questions/470256/process-waitforexit-asynchronously
/// </summary>
public static class AsyncProcess
{
    /// <summary>
    /// Run a process asynchronously
    /// <para>To capture STDOUT, set StartInfo.RedirectStandardOutput to TRUE</para>
    /// <para>To capture STDERR, set StartInfo.RedirectStandardError to TRUE</para>
    /// </summary>
    /// <param name="startInfo">ProcessStartInfo object</param>
    /// <param name="timeoutMs">The timeout in milliseconds (null for no timeout)</param>
    /// <returns>Result object</returns>
    public static async Task<Result> RunAsync(ProcessStartInfo startInfo, int? timeoutMs = null)
    {
        var result = new Result();

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        // List of tasks to wait for a whole process exit
        var processTasks = new List<Task>();

        // === EXITED Event handling ===
        var processExitEvent = new TaskCompletionSource<object>();
        process.Exited += (_, _) =>
        {
            processExitEvent.TrySetResult(true);
        };
        processTasks.Add(processExitEvent.Task);

        // === STDOUT handling ===
        var stdOutBuilder = new StringBuilder();
        if (process.StartInfo.RedirectStandardOutput)
        {
            var stdOutCloseEvent = new TaskCompletionSource<bool>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    stdOutCloseEvent.TrySetResult(true);
                }
                else
                {
                    stdOutBuilder.AppendLine(e.Data);
                }
            };

            processTasks.Add(stdOutCloseEvent.Task);
        }

        // === STDERR handling ===
        var stdErrBuilder = new StringBuilder();
        if (process.StartInfo.RedirectStandardError)
        {
            var stdErrCloseEvent = new TaskCompletionSource<bool>();

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    stdErrCloseEvent.TrySetResult(true);
                }
                else
                {
                    stdErrBuilder.AppendLine(e.Data);
                }
            };

            processTasks.Add(stdErrCloseEvent.Task);
        }

        // === START OF PROCESS ===
        if (!process.Start())
        {
            result.ExitCode = process.ExitCode;
            return result;
        }

        // Reads the output stream first as needed and then waits because deadlocks are possible
        if (process.StartInfo.RedirectStandardOutput)
        {
            process.BeginOutputReadLine();
        }

        if (process.StartInfo.RedirectStandardError)
        {
            process.BeginErrorReadLine();
        }

        // === ASYNC WAIT OF PROCESS ===

        // Process completion = exit AND stdout (if defined) AND stderr (if defined)
        var processCompletionTask = Task.WhenAll(processTasks);

        // Task to wait for exit OR timeout (if defined)
        var awaitingTask = timeoutMs.HasValue
            ? Task.WhenAny(Task.Delay(timeoutMs.Value), processCompletionTask)
            : Task.WhenAny(processCompletionTask);

        // Let's now wait for something to end...
        if (await awaitingTask.ConfigureAwait(false) == processCompletionTask)
        {
            // -> Process exited cleanly
            result.ExitCode = process.ExitCode;
        }
        else
        {
            // -> Timeout, let's kill the process
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }
        }

        // Read stdout/stderr
        result.StdOut = stdOutBuilder.ToString();
        result.StdErr = stdErrBuilder.ToString();
        result.Process = process;
        
        return result;
    }

    /// <summary>
    /// Run process result
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Exit code
        /// <para>If NULL, process exited due to timeout</para>
        /// </summary>
        public int? ExitCode { get; set; }

        /// <summary>
        /// Standard error stream
        /// </summary>
        public string StdErr { get; set; } = "";

        /// <summary>
        /// Standard output stream
        /// </summary>
        public string StdOut { get; set; } = "";

        public Process? Process { get; set; }
    }
}