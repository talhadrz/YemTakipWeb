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
    await context.Response.SendFileAsync("wwwroot/web/index.html");
    
});

// þifre çekmek için gerekli middleware
app.MapGet("/sifre", async context =>
{
    const string connStr = "Host=ep-icy-leaf-a8u4jcms-pooler.eastus2.azure.neon.tech;Port=5432;Username=neondb_owner;Password=npg_b5VmUWicq6Ph;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;";
    await using var bag = new NpgsqlConnection(connStr);
    await bag.OpenAsync();
    await using var cmd = new NpgsqlCommand("SELECT sifrem FROM sifre WHERE id = 1", bag);
    await using var reader = await cmd.ExecuteReaderAsync();

    if (await reader.ReadAsync())
    {
        var sifre = reader.IsDBNull(0) ? "0" : reader.GetString(0);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { sifre }));
    }
});

app.MapPost("/sifre-degistir", async (HttpContext context) =>
{
    var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(context.Request.Body);
    var eski = body["eski"];
    var yeni = body["yeni"];
    const string connStr = "Host=ep-icy-leaf-a8u4jcms-pooler.eastus2.azure.neon.tech;Port=5432;Username=neondb_owner;Password=npg_b5VmUWicq6Ph;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;";


    await using var bag = new NpgsqlConnection(connStr);
    await bag.OpenAsync();

    // Þifreyi kontrol et
    await using (var cmd = new NpgsqlCommand("SELECT sifrem FROM sifre WHERE id = 1", bag))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        if (await reader.ReadAsync())
        {
            var mevcut = reader.GetString(0);
            if (mevcut != eski)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("? Eski þifre yanlýþ.");
                return;
            }
        }
    }

    // Þifreyi güncelle
    await using (var cmd = new NpgsqlCommand("UPDATE sifre SET sifrem = @yeni WHERE id = 1", bag))
    {
        cmd.Parameters.AddWithValue("yeni", yeni);
        await cmd.ExecuteNonQueryAsync();
    }

    await context.Response.WriteAsync("? Þifre baþarýyla deðiþtirildi.");
});


// Verileri listele
app.MapGet("/veri", async context =>
{
    const string connStr = "Host=ep-icy-leaf-a8u4jcms-pooler.eastus2.azure.neon.tech;Port=5432;Username=neondb_owner;Password=npg_b5VmUWicq6Ph;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;";
    await using var bag = new NpgsqlConnection(connStr);
    await bag.OpenAsync();
    var list = new List<Dictionary<string, object>>();
    await using (var cmd = new NpgsqlCommand("SELECT * FROM yem;", bag))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.GetValue(i);

            list.Add(row);
        }
    }

    decimal toplamUcret = 0;
    int toplamGun = 0;

    // Ýlk sorgu: SUM(ücret)
    await using (var cmd1 = new NpgsqlCommand("SELECT SUM(ücret) FROM yem;", bag))
    await using (var reader1 = await cmd1.ExecuteReaderAsync())
    {
        if (await reader1.ReadAsync())
        {
            toplamUcret = reader1.IsDBNull(0) ? 0 : reader1.GetDecimal(0);
        }
    }

    // Ýkinci sorgu: COUNT(*)
    await using (var cmd2 = new NpgsqlCommand("SELECT COUNT(*) FROM yem;", bag))
    await using (var reader2 = await cmd2.ExecuteReaderAsync())
    {
        if (await reader2.ReadAsync())
        {
            toplamGun = reader2.IsDBNull(0) ? 0 : reader2.GetInt32(0);
        }
    }

    // Tüm verileri birlikte JSON'a çevirip tek seferde gönder
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        toplam = toplamUcret,
        gun = toplamGun,
        list = list
    });

    //var json = JsonSerializer.Serialize(list);
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
