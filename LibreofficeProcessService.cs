using System.Diagnostics;

namespace libreoffice_test;

public class LibreofficeProcessService : ILibreofficeProcessService, IHostedService
{
    private readonly string _libreoffice;
    private readonly string _unoserver;
    private readonly string _unoconvert;
    
    private readonly ILogger<LibreofficeProcessService> _logger;
    private Process? _unoserverProcess;
    private static readonly SemaphoreSlim SemaphoreSlim = new(1,1);
    private bool _terminated;

    public LibreofficeProcessService(
        IConfiguration configuration,
        ILogger<LibreofficeProcessService> logger, 
        IHostApplicationLifetime applicationLifetime)
    {
        _libreoffice = configuration.GetValue<string>("libreoffice:executable");
        _unoserver = configuration.GetValue<string>("libreoffice:unoserver");
        _unoconvert = configuration.GetValue<string>("libreoffice:unoconvert");
        _logger = logger;

        applicationLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Application stopping");
            _terminated = true;
        });
    }
    
    private void CleanupLibreOffice(string source)
    {
        if (_unoserverProcess != null)
        {
            _logger.LogInformation("LibreOffice cleanup from {source}", source);
            _unoserverProcess.Kill();
            _unoserverProcess.Dispose();
            _unoserverProcess = null;
        }
    }
    
    private async Task EnsureLibreoffice(bool forceInit = false)
    {
        await SemaphoreSlim.WaitAsync();
        try
        {
            if (_unoserverProcess == null || forceInit)
            {
                CleanupLibreOffice("EnsureLibreoffice");
                
                _logger.LogInformation("Starting Libreoffice UnoServer");
        
                var startInfo = new ProcessStartInfo(_unoserver, $"--daemon --executable={_libreoffice}")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    EnvironmentVariables =
                    {
                        ["HOME"] = "/tmp",
                        ["TMPDIR"] = "/tmp"
                    }
                };
                
                var unoserverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                unoserverProcess.OutputDataReceived += (_, e) =>
                {
                    _logger.LogInformation(e.Data);
                };
                unoserverProcess.ErrorDataReceived += (_, e) =>
                {
                    _logger.LogInformation(e.Data);
                };
                
                // start the long running Libreoffice unoserver process
                if (!unoserverProcess.Start())
                {
                    _logger.LogError("Failed to start {unoserver} with exitCode {exitCode}", 
                        _unoserver,
                        unoserverProcess.ExitCode);
                }
                else
                {
                    unoserverProcess.BeginOutputReadLine();
                    unoserverProcess.BeginErrorReadLine();
                    _unoserverProcess = unoserverProcess;
                }
            }
        }
        finally
        {
            SemaphoreSlim.Release();
        }
    }

    async Task ILibreofficeProcessService.ConvertFile(int retryCount, string lastError)
    {
        if (!_terminated)
        {
            if (lastError.Contains("Connection refused"))
            {
                await EnsureLibreoffice(true);    
            }
            
            if (retryCount < 10)
            {
                var source = $"/tmp/testdoc{Guid.NewGuid().ToString()}.odt";
                var target = $"/tmp/testdoc{Guid.NewGuid().ToString()}.pdf";
            
                File.Copy("./testdoc.odt",  source, true);
            
                var startInfo = new ProcessStartInfo(_unoconvert, $"--convert-to pdf {source} {target}")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    EnvironmentVariables =
                    {
                        ["HOME"] = "/tmp",
                        ["TMPDIR"] = "/tmp"
                    }
                };

                AsyncProcess.AsyncProcessResult? processResult = null;
                try
                {
                    processResult = await AsyncProcess.RunAsync(startInfo);
                }
                finally
                {
                    processResult?.Process?.Dispose();
                }
                
                if (processResult.ExitCode != 0)
                {
                    File.Delete(source);
                    var retries = retryCount + 1;
                    await Task.Delay(100);
                    await ((ILibreofficeProcessService)this).ConvertFile(retries, processResult.StdErr);
                }
                else
                {
                    _logger.LogInformation("converted {TargetFile}", target);
                    File.Delete(source);
                    File.Delete(target);
                }
            }
            else
            {
                _logger.LogInformation("fail retry: {failure}", lastError);
                throw new Exception("convert failure");
            }    
        }
    }
    
    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartAsync");
        await EnsureLibreoffice();
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        CleanupLibreOffice("StopAsync");
        return Task.CompletedTask;
    }
}