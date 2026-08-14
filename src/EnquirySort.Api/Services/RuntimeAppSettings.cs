using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;

namespace EnquirySort.Api.Services;

/// <summary>
/// Cached runtime admin settings (response mode + email signature).
/// </summary>
public sealed class RuntimeAppSettings
{
    private readonly AppSettingsRepository _repo;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSetting? _cached;

    public RuntimeAppSettings(AppSettingsRepository repo) => _repo = repo;

    public async Task<AppSetting> GetAsync(CancellationToken cancellationToken = default)
    {
        AppSetting? local = _cached;
        if (local is not null)
        {
            return local;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            _cached = await _repo.GetAppSettingsAsync(cancellationToken);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ReplaceCache(AppSetting settings) => _cached = settings;

    public void Invalidate() => _cached = null;
}
