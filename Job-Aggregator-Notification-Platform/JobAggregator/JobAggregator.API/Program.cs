var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ?? Uncomment each line as we build the classes ??
// builder.Services.AddTransient<IJobScraperService, LinkedInScraperService>();
// builder.Services.AddTransient<IJobScraperService, StepStoneScraperService>();
// builder.Services.AddScoped<IJobRepository, JobRepository>();
// builder.Services.AddMemoryCache();
// builder.Services.AddSingleton<IJobScraperFactory, JobScraperFactory>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();