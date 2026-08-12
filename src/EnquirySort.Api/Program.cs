using EnquirySort.Api;
using FastEndpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

builder.Services.MyAddRepositories();
builder.Services.MyAddEnquiryServices(builder.Configuration);
builder.Services.AddFastEndpoints();

WebApplication app = builder.Build();

app.UseCors();
app.MyUseFastEndpoints();

app.Run();
