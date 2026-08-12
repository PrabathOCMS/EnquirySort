using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.GetEnquiry;

public sealed class GetEnquiryEndpoint : Endpoint<GetEnquiryRequest, Enquiry>
{
    private readonly EnquiriesRepository _repo;

    public GetEnquiryEndpoint(EnquiriesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/enquiries/get/{id}");
        SerializerContext(GetEnquiryContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetEnquiryRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        Enquiry? entity = await _repo.GetEnquiryAsync(req.id!.Value, ct);
        ValidateOutput(entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(GetEnquiryRequest req)
    {
        if (!req.id.HasValue)
        {
            AddError(m => m.id!, "Id is required.", "error.enquiry.idIsRequired");
        }
    }

    private void ValidateOutput(Enquiry? entity)
    {
        if (entity is null)
        {
            HttpContext.Items["FatalError"] = true;
            AddError("The selected enquiry did not exist.", "error.enquiry.didNotExist");
        }
    }
}
