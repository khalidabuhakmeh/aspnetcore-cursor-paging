using System;
using System.Data;
using System.Linq;
using CursorPaging;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Dapper
SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<Database>();


var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetService<Database>();
    var logger = scope.ServiceProvider.GetService<ILogger<WebApplication>>();
    var result = await Database.SeedPicturesWithSql(db);
    logger.LogInformation($"Seed operation returned with code {result}");
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app
    .UseDefaultFiles()
    .UseStaticFiles();

app.MapGet("/pictures/paging", async http =>
{
    var page = http.Request.Query.TryGetValue("page", out var pages)
        ? int.Parse(pages.FirstOrDefault() ?? string.Empty)
        : 1;

    var size = http.Request.Query.TryGetValue("size", out var sizes)
        ? int.Parse(sizes.FirstOrDefault() ?? string.Empty)
        : 10;

    await using var db = http.RequestServices.GetRequiredService<Database>();
    var total = await db.Pictures.CountAsync();
    var query = db
        .Pictures
        .OrderBy(x => x.Id)
        .Skip((page - 1) * size)
        .Take(size);

    var logger = http.RequestServices.GetRequiredService<ILogger<Database>>();
    logger.LogInformation($"Using Paging:\n{query.ToQueryString()}");

    var results = await query.ToListAsync();
    await http.Response.WriteAsJsonAsync(new PagingResult
    {
        Page = page,
        Size = size,
        Pictures = results.ToList(),
        TotalCount = total,
        Sql = query.ToQueryString()
    });
});

app.MapGet("/pictures/cursor", async http =>
{
    var after = http.Request.Query.TryGetValue("after", out var afters)
        ? int.Parse(afters.FirstOrDefault() ?? string.Empty)
        : 0;

    var size = http.Request.Query.TryGetValue("size", out var sizes)
        ? int.Parse(sizes.FirstOrDefault() ?? string.Empty)
        : 10;

    await using var db = http.RequestServices.GetRequiredService<Database>();
    var logger = http.RequestServices.GetRequiredService<ILogger<Database>>();

    var total = await db.Pictures.CountAsync();
    var query = db
        .Pictures
        .OrderBy(x => x.Id)
        // will use the index
        .Where(x => x.Id > after)
        .Take(size);

    logger.LogInformation($"Using Cursor:\n{query.ToQueryString()}");

    var results = await query.ToListAsync();

    await http.Response.WriteAsJsonAsync(new CursorResult
    {
        TotalCount = total,
        Pictures = results,
        Cursor = new CursorResult.CursorItems
        {
            After = results.Select(x => (int?) x.Id).LastOrDefault(),
            Before = results.Select(x => (int?) x.Id).FirstOrDefault()
        },
        Sql = query.ToQueryString()
    });
});

app.MapGet("/pictures/dapper", async http =>
{
    var after = http.Request.Query.TryGetValue("after", out var afters)
        ? int.Parse(afters.FirstOrDefault() ?? string.Empty)
        : 0;

    var size = http.Request.Query.TryGetValue("size", out var sizes)
        ? int.Parse(sizes.FirstOrDefault() ?? string.Empty)
        : 10;

    var connection = new SqliteConnection(Database.ConnectionString);
    await connection.OpenAsync();
    var logger = http.RequestServices.GetRequiredService<ILogger<Database>>();

    var total = await connection.QuerySingleOrDefaultAsync<int>("select count(id) from Pictures");
    var sql = 
@"SELECT p.Id, p.Created, p.Url
FROM Pictures AS p
WHERE p.Id > @after
ORDER BY p.Id LIMIT @size";
    
    var results = (await connection
        .QueryAsync<Picture>(sql, new {size, after}))
        .ToList();

    await http.Response.WriteAsJsonAsync(new CursorResult
    {
        TotalCount = total,
        Pictures = results.ToList(),
        Cursor = new CursorResult.CursorItems
        {
            After = results.Select(x => (int?) x.Id).LastOrDefault(),
            Before = results.Select(x => (int?) x.Id).FirstOrDefault()
        },
        Sql = sql
    });
});

app.Run();

class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = $"{value:yyyy-MM-dd HH:mm:ss}";
        parameter.DbType = DbType.String;
    }

    public override DateTimeOffset Parse(object value)
        => DateTimeOffset.Parse((string)value);
}