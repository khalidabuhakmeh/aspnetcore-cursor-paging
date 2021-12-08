using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bogus;
using EFCore.BulkExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace CursorPaging
{
    public class Database : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite(ConnectionString);
        }

        public DbSet<Picture> Pictures { get; set; }
        public const string ConnectionString = "Data Source=pictures.db";

        public static async Task<SeedOperationResult> SeedPictures(Database database, string label = "normal")
        {
            try
            {
                await database.Database.MigrateAsync();

                // already pre-loaded
                if (await database.Pictures.AnyAsync())
                    return SeedOperationResult.Skip;

                // hold onto your butts
                var data = new Faker<Picture>()
                    .RuleFor(p => p.Url, f => f.Image.PicsumUrl())
                    .GenerateForever();

                await AnsiConsole
                    .Progress()
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        const int max = 1_000_000;
                        var task = ctx.AddTask($"Loading pictures - {label}", true, max);
                        task.StartTask();
                        while (task.Value < task.MaxValue)
                        {
                            const int amount = 10_000;
                            var page = data.Take(amount);
                            await database.Pictures.AddRangeAsync(page);
                            await database.SaveChangesAsync();
                            database.ChangeTracker.Clear();
                            task.Increment(amount);
                        }

                        task.StopTask();
                    });

                return SeedOperationResult.Seeded;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return SeedOperationResult.Error;
            }
        }

        public static async Task<SeedOperationResult> SeedPicturesWithBulkExtensions(Database database, string label = "normal")
        {
            try
            {
                await database.Database.MigrateAsync();

                // already pre-loaded
                if (await database.Pictures.AnyAsync())
                    return SeedOperationResult.Skip;

                // hold onto your butts
                var data = new Faker<Picture>()
                    .RuleFor(p => p.Url, f => f.Image.PicsumUrl())
                    .GenerateForever();

                await AnsiConsole
                    .Progress()
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        const int max = 1_000_000;
                        var task = ctx.AddTask($"Loading pictures - {label}", true, max);
                        await database.BulkInsertAsync(data.Take(max).ToList(), bulkAction: c => c.NotifyAfter = 100_000, progress: p => task.Value = (double)p * max);
                    });

                return SeedOperationResult.Seeded;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return SeedOperationResult.Error;
            }
        }

        public static async Task<SeedOperationResult> SeedPicturesWithSql(Database database, string label = "normal")
        {
            try
            {
                await database.Database.MigrateAsync();

                // already pre-loaded
                if (await database.Pictures.AnyAsync())
                    return SeedOperationResult.Skip;
                
                // hold onto your butts
                var data = new Faker<Picture>()
                    .RuleFor(p => p.Url, f => f.Image.PicsumUrl())
                    .GenerateForever();

                await AnsiConsole
                    .Progress()
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        const int max = 1_000_000;
                        var task = ctx.AddTask($"Loading pictures - {label}", true, max);
                        task.StartTask();
                        var id = 0;

                        var sqlConnection = new SqliteConnection(CursorPaging.Database.ConnectionString);
                        await sqlConnection.OpenAsync();
                        
                        while (task.Value < task.MaxValue)
                        {
                            const int amount = 250_000;
                            var values = new StringBuilder();

                            foreach (var picture in data.Take(amount))
                            {
                                values.AppendFormat("\n({0}, '{1}', '{2:yyyy-MM-dd HH:mm:ss}'),",
                                    ++id,
                                    picture.Url,
                                    picture.Created
                                );
                            }

                            // remove the comma
                            values.Remove(values.Length - 1, 1);

                            var command = sqlConnection.CreateCommand();
                            command.CommandText = $"insert into Pictures (Id, Url, Created) values {values};";
                            command.CommandType = CommandType.Text;

                            await command.ExecuteNonQueryAsync();
                            
                            task.Increment(amount);
                        }

                        task.StopTask();
                    });

                return SeedOperationResult.Seeded;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return SeedOperationResult.Error;
            }
        }
        
        public static async Task<SeedOperationResult> SeedPicturesWithCommand(Database database, string label = "normal")
        {
            try
            {
                await database.Database.MigrateAsync();

                // already pre-loaded
                if (await database.Pictures.AnyAsync())
                    return SeedOperationResult.Skip;

                // hold onto your butts
                var data = new Faker<Picture>()
                    .RuleFor(p => p.Url, f => f.Image.PicsumUrl())
                    .GenerateForever();

                await AnsiConsole
                    .Progress()
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        const int max = 1_000_000;
                        var task = ctx.AddTask($"Loading pictures - {label}", true, max);
                        task.StartTask();
                        var id = 0;
                        while (task.Value < task.MaxValue)
                        {
                            const int amount = 250_000;

                            using (var transaction = await database.Database.BeginTransactionAsync())
                            {
                                var command = database.Database.GetDbConnection().CreateCommand();
                                command.CommandText =
                                    $"insert into Pictures (Id, Url, Created) values ($id, $url, $created);";
                                var parameterId = command.CreateParameter();

                                parameterId.ParameterName = "$id";
                                command.Parameters.Add(parameterId);

                                var parameterUrl = command.CreateParameter();
                                parameterUrl.ParameterName = "$url";
                                command.Parameters.Add(parameterUrl);

                                var parameterCreated = command.CreateParameter();
                                parameterCreated.ParameterName = "$created";
                                command.Parameters.Add(parameterCreated);

                                for (var i = 0; i < amount; i++)
                                {
                                    var picture = data.First();

                                    parameterId.Value = id++;
                                    parameterUrl.Value = picture.Url;
                                    parameterCreated.Value = picture.Created.ToString("yyyy-MM-dd HH:mm:ss");
                                    await command.ExecuteNonQueryAsync();
                                }

                                await transaction.CommitAsync();
                            }

                            task.Increment(amount);
                        }

                        task.StopTask();
                    });

                return SeedOperationResult.Seeded;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return SeedOperationResult.Error;
            }
        }
    }

    public enum SeedOperationResult
    {
        Skip,
        Seeded,
        Error
    }

    [Index(nameof(Created))]
    public class Picture
    {
        public int Id { get; set; }
        public string Url { get; set; }

        public DateTimeOffset Created { get; set; }
            = DateTimeOffset.Now;
    }
}