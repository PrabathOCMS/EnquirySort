using EnquirySort.Api.Models;
using EnquirySort.Api.Services;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.ProcessInbox;

public sealed class ProcessInboxEndpoint : Endpoint<ProcessInboxRequest, List<Enquiry>>
{
    private readonly EnquiryPipeline _pipeline;

    public ProcessInboxEndpoint(EnquiryPipeline pipeline) => _pipeline = pipeline;

    public override void Configure()
    {
        Post("/enquiries/processInbox");
        SerializerContext(ProcessInboxContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessInboxRequest req, CancellationToken ct)
    {
        List<Enquiry> results = await _pipeline.ProcessInboxAsync(ct);
        await Send.OkAsync(results);
    }
}
