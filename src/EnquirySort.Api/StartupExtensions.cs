using EnquirySort.Api.Configuration;
using EnquirySort.Api.Email;
using EnquirySort.Api.Repositories;
using EnquirySort.Api.Services;
using FastEndpoints;
using RT.Comb;

namespace EnquirySort.Api;

public static class StartupExtensions
{
    public static IServiceCollection MyAddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<ICombProvider>(_ => EnsureOrderedProvider.Sql);
        services.AddSingleton<MailingListsRepository>();
        services.AddSingleton<KnowledgeArticlesRepository>();
        services.AddSingleton<EnquiriesRepository>();
        services.AddSingleton<AppSettingsRepository>();
        services.AddSingleton<RuntimeAppSettings>();
        return services;
    }

    public static IServiceCollection MyAddEnquiryServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration);
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value);
        services.AddHttpClient<OpenRouterClient>();
        services.AddSingleton<ImapEmailClient>();
        services.AddSingleton<EnquiryPipeline>();
        services.AddSingleton<DatabaseBootstrapper>();
        services.AddHostedService<EnquiryInboxWorker>();
        return services;
    }

    public static IApplicationBuilder MyUseFastEndpoints(this IApplicationBuilder app)
    {
        app.UseFastEndpoints(c =>
        {
            c.Errors.ProducesMetadataType = typeof(Models.MyErrorResponse);
            c.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
            {
                Models.MyErrorResponse response = new()
                {
                    TraceId = ctx.TraceIdentifier,
                    FatalError = ctx.Items.ContainsKey("FatalError"),
                    ConcurrencyKeyInvalid = ctx.Items.ContainsKey("ConcurrencyKeyInvalid"),
                    AdditionalData = ctx.Items.TryGetValue("ErrorAdditionalData", out object? data)
                        ? data?.ToString()
                        : null
                };

                foreach (FluentValidation.Results.ValidationFailure failure in failures)
                {
                    string key = string.IsNullOrWhiteSpace(failure.PropertyName) ? "General" : failure.PropertyName;
                    if (!response.ErrorMessages.TryGetValue(key, out List<Models.ErrorMessageItem>? list))
                    {
                        list = [];
                        response.ErrorMessages[key] = list;
                    }

                    list.Add(new Models.ErrorMessageItem
                    {
                        Message = failure.ErrorMessage,
                        ErrorCode = failure.ErrorCode
                    });
                }

                return response;
            };
        });

        return app;
    }
}
