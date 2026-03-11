using JobAggregator.Application.Interfaces;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddTransient<IJobScrapperService, LinkedInScrapperService>();
builder.Services.AddTransient<IJobScrapperService, StepStoneScrapperService>();

builder.Services.AddScoped<IJobRepository, JobRepository>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IJobScrapperFactory, JobScrapperFactory>();