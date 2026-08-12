using EnquirySort.Api.Configuration;
using Microsoft.Extensions.Options;

namespace EnquirySort.Api.Services;

public sealed class EnquiryInboxWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly AppSettings _settings;
    private readonly ILogger<EnquiryInboxWorker> _logger;

    public EnquiryInboxWorker(IServiceProvider services, IOptions<AppSettings> settings, ILogger<EnquiryInboxWorker> logger)
    {
        _services = services;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnquiryWorker.Enabled)
        {
            _logger.LogInformation("Enquiry inbox worker disabled (EnquiryWorker:Enabled=false)");
            return;
        }

        _logger.LogInformation(
            "Enquiry inbox worker started. Poll interval={Seconds}s dryRun={DryRun}",
            _settings.EnquiryWorker.PollIntervalSeconds,
            _settings.Mail.DryRun);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EnquiryPipeline pipeline = _services.GetRequiredService<EnquiryPipeline>();
                await pipeline.ProcessInboxAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Inbox poll cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _settings.EnquiryWorker.PollIntervalSeconds)), stoppingToken);
        }
    }
}
