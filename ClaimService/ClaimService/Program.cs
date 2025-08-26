using ClaimService.Application.Common.Errors;
using ClaimService.Application.Interfaces;
using ClaimService.Application.Middlewares;
using ClaimService.Infrastructure.Clients;
using ClaimService.Infrastructure.Helpers;
using ClaimService.Infrastructure.Options;
using ClaimService.Infrastructure.Persistence;
using ClaimService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// ---------- EF Core ----------
var cs = builder.Configuration.GetConnectionString("Default")
         ?? builder.Configuration["ConnectionStrings:Default"];

if (string.IsNullOrWhiteSpace(cs))
    throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

// DB Connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(cs));


// ---------- HTTP Context + Auth forwarding handler ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthForwardingHandler>();

// ---------- Typed HttpClients (forward JWT to downstream) ----------
builder.Services.AddHttpClient<IEmployeeServiceClient, EmployeeServiceClient>(c =>
{
    // e.g., https://employee.local/api/
    c.BaseAddress = new Uri(builder.Configuration["EmployeeService:BaseUrl"]!);
}).AddHttpMessageHandler<AuthForwardingHandler>();

// If/when you add a DocumentService client, wire it similarly:
// builder.Services.AddHttpClient<IDocumentServiceClient, DocumentServiceClient>(c =>
// {
//     c.BaseAddress = new Uri(builder.Configuration["DocumentService:BaseUrl"]!);
// }).AddHttpMessageHandler<AuthForwardingHandler>();

// ---------- App Services ----------
builder.Services.AddScoped<IIdGenerator, IdGenerator>();                  // uses DbContext (scoped)
builder.Services.AddScoped<IClaimService, ClaimService.Infrastructure.Services.ClaimService>();                // uses DbContext (scoped)

// ---------- AuthN / AuthZ (JWT issued by your AuthService) ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    }); ;

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyConstants.MedicalRead, p => p.RequireRole(
        "Medical.Admin", "MedicalClaims:somedical1", "MedicalClaims:somedical2", "Hospital.User", "SMB.User"));

    options.AddPolicy(PolicyConstants.MedicalWrite, p => p.RequireRole(
        "Medical.Admin", "MedicalClaims:somedical1", "MedicalClaims:somedical2"));

    options.AddPolicy(PolicyConstants.HospitalReview, p => p.RequireRole(
        "Hospital.User", "Medical.Admin"));

    options.AddPolicy(PolicyConstants.SmbDecide, p => p.RequireRole(
        "SMB.User", "Medical.Admin"));
});


builder.Services.Configure<CachingOptions>(builder.Configuration.GetSection("Caching"));
// Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"];
    options.InstanceName = builder.Configuration["Redis:InstanceName"]; // prefixes keys, e.g., ClaimSvc:
});

// HttpContext + auth forwarding
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthForwardingHandler>();

// Typed HttpClient for EmployeeService
builder.Services.AddHttpClient<IEmployeeServiceClient, EmployeeServiceClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["EmployeeService:BaseUrl"]!); // ensure trailing slash
})
.AddHttpMessageHandler<AuthForwardingHandler>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ClaimService", Version = "v1" });
    var jwt = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Description = "JWT Authorization header using the Bearer scheme.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", jwt);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwt, Array.Empty<string>() } });
});

builder.Services.AddProblemDetails();
builder.Services.AddTransient<ExceptionMiddleware>();

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetService<AppDbContext>(); // e.g., AuthDbContext
    db?.Database.Migrate();
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseHttpsRedirection();

app.UseRouting();
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
