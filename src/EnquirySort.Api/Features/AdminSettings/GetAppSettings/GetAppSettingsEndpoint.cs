using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.AdminSettings.GetAppSettings;

public sealed class GetAppSettingsEndpoint : EndpointWithoutRequest<AppSetting>
{
    private readonly AppSettingsRepository _repo;
    private readonly Services.RuntimeAppSettings _runtime;

    public GetAppSettingsEndpoint(AppSettingsRepository repo, Services.RuntimeAppSettings runtime)
    {
        _repo = repo;
        _runtime = runtime;
    }

    public override void Configure()
    {
        Get("/appSettings/get");
        SerializerContext(GetAppSettingsContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        AppSetting entity = await _repo.GetAppSettingsAsync(ct);
        _runtime.ReplaceCache(entity);
        await Send.OkAsync(entity);
    }
}
