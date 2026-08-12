using System.Text.Json;
using System.Text.RegularExpressions;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.KnowledgeArticles.UpdateKnowledgeArticle;

public sealed partial class UpdateKnowledgeArticleEndpoint : Endpoint<UpdateKnowledgeArticleRequest, KnowledgeArticle>
{
    private readonly KnowledgeArticlesRepository _repo;

    public UpdateKnowledgeArticleEndpoint(KnowledgeArticlesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/knowledgeArticles/update");
        SerializerContext(UpdateKnowledgeArticleContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateKnowledgeArticleRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        (SqlQueryResult queryResult, KnowledgeArticle? entity) =
            await _repo.UpdateKnowledgeArticleAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(UpdateKnowledgeArticleRequest req)
    {
        if (!req.id.HasValue)
            AddError(m => m.id!, "Id is required.", "error.knowledgeArticle.idIsRequired");

        req.Title = req.Title?.Trim();
        if (string.IsNullOrWhiteSpace(req.Title))
            AddError(m => m.Title!, "Title is required.", "error.knowledgeArticle.titleIsRequired");
        else if (req.Title.Length > 200)
            AddError(m => m.Title!, "Title must be 200 characters or less.",
                "error.knowledgeArticle.titleLength|{\"length\":\"200\"}");

        req.Slug = req.Slug?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(req.Slug))
            AddError(m => m.Slug!, "Slug is required.", "error.knowledgeArticle.slugIsRequired");
        else if (req.Slug.Length > 200)
            AddError(m => m.Slug!, "Slug must be 200 characters or less.",
                "error.knowledgeArticle.slugLength|{\"length\":\"200\"}");
        else if (!SlugRegex().IsMatch(req.Slug))
            AddError(m => m.Slug!, "Slug may only contain lowercase letters, numbers, and hyphens.",
                "error.knowledgeArticle.slugFormat");

        req.Content = req.Content?.Trim();
        if (string.IsNullOrWhiteSpace(req.Content))
            AddError(m => m.Content!, "Content is required.", "error.knowledgeArticle.contentIsRequired");
        else if (req.Content.Length > 100000)
            AddError(m => m.Content!, "Content must be 100000 characters or less.",
                "error.knowledgeArticle.contentLength|{\"length\":\"100000\"}");

        if (req.ConcurrencyKey is null)
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.knowledgeArticle.concurrencyKeyIsRequired");
        else if (req.ConcurrencyKey.Length > 4)
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.knowledgeArticle.concurrencyKeyLength|{\"length\":\"4\"}");
    }

    private void ValidateOutput(SqlQueryResult queryResult, KnowledgeArticle? entity)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                if (entity is null) AddError("An unknown error occurred.", "error.unknown");
                return;
            case SqlQueryResult.RecordDidNotExist:
                HttpContext.Items["FatalError"] = true;
                AddError("The knowledge article was deleted since you last accessed this page.",
                    "error.knowledgeArticle.deletedSinceAccessedPage");
                break;
            case SqlQueryResult.RecordAlreadyExists:
                AddError(m => m.Slug!, "Another knowledge article already exists with the specified slug.",
                    "error.knowledgeArticle.slugExists");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                HttpContext.Items["ConcurrencyKeyInvalid"] = true;
                HttpContext.Items["ErrorAdditionalData"] =
                    JsonSerializer.Serialize(entity, UpdateKnowledgeArticleContext.Default.KnowledgeArticle);
                AddError(
                    "The knowledge article's data has changed since you last accessed this page. Please review " +
                    "the current updated version of the data below, then submit your changes again if you " +
                    "wish to overwrite.",
                    "error.knowledgeArticle.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }

    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
