using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScriptProcessor.Data;
using ScriptProcessor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
// Add Azure Key Vault configuration for all environments
var keyVaultUrl = builder.Configuration["Azure:KeyVault:Url"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential());
}

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddScoped<IFormatter, FormatService>();


builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

//register the SQLite glossary service
builder.Services.AddDbContext<GlossaryDbContext>(options =>
    options.UseSqlite("Data Source=\"C:\\Translations\\Glossaries\\glossary.db\""));

// Register GlossaryDBService
builder.Services.AddScoped<GlossaryDBService>();



// Register Azure Blob Storage
builder.Services.AddSingleton(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var blobConnectionString = configuration["BlobConnectionString"] 
        ?? throw new InvalidOperationException("Blob storage connection string not found.");
    return new BlobServiceClient(blobConnectionString);
});

// Register Azure Blob Service
builder.Services.AddScoped<IFileService, AzureBlobService>();

// Register Azure Vault Service
builder.Services.AddScoped<AzureVaultService>();

// Register API Service with HttpClient
builder.Services.AddHttpClient<IApiService, ApiService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=File}/{action=List}/{id?}");
app.MapRazorPages();



app.Run();
