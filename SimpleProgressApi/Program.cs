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

    // �^���I�ɐi����i�߂�
    if (progress < 100)
        progress += 10; // �Ă΂�邽��10%���i��

    if (progress >= 100)
    {
        progress = 100;
        jobStarted = false; // ���������玩���Ń��Z�b�g
    }

    return Results.Json(new { started = jobStarted, progress });
});

app.Run();