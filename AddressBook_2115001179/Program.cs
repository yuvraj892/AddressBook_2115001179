using AutoMapper;
using BusinessLayer.Validation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using RepositoryLayer.Context;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();

// Register AutoMapper
builder.Services.AddSingleton<IMapper>(sp =>
{
    var config = new MapperConfiguration(cfg =>
    {
        cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies());
    });

    return config.CreateMapper();
});

// Add FluentValidation
builder.Services.AddControllers()
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<AddressBookValidator>());



// Add OpenAPI (Swagger) support
builder.Services.AddEndpointsApiExplorer();

var xmlFile = "AddressBookApplication.xml";
var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

if (File.Exists(xmlPath))
{
    builder.Services.AddSwaggerGen(options =>
    {
        options.IncludeXmlComments(xmlPath);
    });
}
else
{
    Console.WriteLine($"?? Warning: XML documentation file not found at {xmlPath}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AddressBookContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 41))));

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
