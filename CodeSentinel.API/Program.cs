using CodeSentinel.API.Endpoints;
using CodeSentinel.API.Infrastructure;
using CodeSentinel.API.Json;
using CodeSentinel.API.Services;

#if DEBUG
var builder = WebApplication.CreateBuilder(args); // full builder in Debug for Swagger
#else
var builder = WebApplication.CreateSlimBuilder(args); // slim builder for AOT/production
#endif

// JSON
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

// Dev-only services (Swagger, CORS, ApiExplorer)
#if DEBUG
// API explorer + swagger in debug only
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevAllowLocal", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});
#endif

// HTTP clients (Ollama etc.)
// long timeouts for local LLM / ollama
builder.Services.AddHttpClient<EmbeddingService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<LocalLlmService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Domain / App services
builder.Services.AddSingleton<VectorStore>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<PromptOrchestrator>();
//builder.Services.AddScoped<LocalLlmService>();
//builder.Services.AddScoped<EmbeddingService>();

var app = builder.Build();

// Dev middleware
#if DEBUG
app.UseDeveloperExceptionPage();
app.UseCors("DevAllowLocal");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "CodeSentinel API (dev)";
    c.RoutePrefix = "swagger";
});
#endif

using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<VectorStore>();
    await DatabaseInitializer.InitializeAsync(store);
}

app.MapChatEndpoints();

app.Run();
