using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// wwwroot klasöründen statik dosyalarý sun
app.UseStaticFiles();

// Ana sayfa (index.html) göster
app.MapGet("/", async context =>
{
    await context.Response.SendFileAsync("wwwroot/index.html");
});

// Verileri listele
app.MapGet("/veri", async context =>
{
    const string connStr = "Host=ep-icy-leaf-a8u4jcms-pooler.eastus2.azure.neon.tech;Port=5432;Username=neondb_owner;Password=npg_b5VmUWicq6Ph;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;";
    await using var bag = new NpgsqlConnection(connStr);
    await bag.OpenAsync();

    await using var cmd = new NpgsqlCommand("SELECT * FROM yem;", bag);
    await using var reader = await cmd.ExecuteReaderAsync();

    var list = new List<Dictionary<string, object>>();
    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.GetValue(i);

        list.Add(row);
    }

    var json = JsonSerializer.Serialize(list);
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(json);
});

// Veri ekle
app.MapPost("/ekle", async context =>
{
    var veri = await JsonSerializer.DeserializeAsync<YemVeri>(context.Request.Body);
    const string connStr = "Host=ep-icy-leaf-a8u4jcms-pooler.eastus2.azure.neon.tech;Port=5432;Username=neondb_owner;Password=npg_b5VmUWicq6Ph;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;";
    await using var bag = new NpgsqlConnection(connStr);
    await bag.OpenAsync();

    await using var cmd = new NpgsqlCommand("INSERT INTO yem (tarih, ücret, çalýþan, ton) VALUES (@tarih, @ucret, @calisan, @ton)", bag);
    cmd.Parameters.AddWithValue("@tarih", veri.Tarih);
    cmd.Parameters.AddWithValue("@ucret", veri.Ucret);
    cmd.Parameters.AddWithValue("@calisan", veri.Calisan);
    cmd.Parameters.AddWithValue("@ton", veri.Ton);
    await cmd.ExecuteNonQueryAsync();

    await context.Response.WriteAsync("Ekleme tamam.");
});

// Veri sil
app.MapPost("/sil", async context =>
{
    var form = await context.Request.ReadFormAsync();
    if (!int.TryParse(form["id"], out var id))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Geçersiz ID");
        return;
    }

    const string connStr = "Host=ep-icy-leaf-a8u4jcms-pooler.eastus2.azure.neon.tech;Port=5432;Username=neondb_owner;Password=npg_b5VmUWicq6Ph;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;";
    await using var bag = new NpgsqlConnection(connStr);
    await bag.OpenAsync();

    await using var cmd = new NpgsqlCommand("DELETE FROM yem WHERE id = @id", bag);
    cmd.Parameters.AddWithValue("@id", id);
    await cmd.ExecuteNonQueryAsync();

    await context.Response.WriteAsync("Silme tamam.");
});

app.Run();

record YemVeri(DateTime Tarih, double Ucret, double Calisan, double Ton);
