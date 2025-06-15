var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

int progress = 0;
bool jobStarted = false;

app.MapPost("/start", () =>
{
    progress = 0;
    jobStarted = true;
    return Results.Ok(new { started = true });
});

app.MapGet("/progress", () =>
{
    if (!jobStarted)
        return Results.Json(new { started = false, progress = 0 });

    // 疑似的に進捗を進める
    if (progress < 100)
        progress += 10; // 呼ばれるたび10%ずつ進む

    if (progress >= 100)
    {
        progress = 100;
        jobStarted = false; // 完了したら自動でリセット
    }

    return Results.Json(new { started = jobStarted, progress });
});

app.Run();