using System;
using System.Diagnostics;
using System.IO;
using CursorPaging;
using Spectre.Console;

var stopwatch = new Stopwatch();

try
{
    DeleteIfExists("pictures.db");
    stopwatch.Restart();
    await Database.SeedPicturesWithSql(new Database(), "With raw sql");
    stopwatch.Stop();
    AnsiConsole.WriteLine($"With wrapping transaction: {stopwatch.Elapsed.TotalSeconds} seconds");
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
}

try
{
    DeleteIfExists("pictures.db");
    stopwatch.Restart();
    var db = new Database();
    await Database.SeedPicturesWithCommand(db, "With command caches parameters");
    stopwatch.Stop();
    AnsiConsole.WriteLine($"With dbcommand and cache parameters transaction: {stopwatch.Elapsed.TotalSeconds} seconds");
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
}

try
{
    DeleteIfExists("pictures.db");
    stopwatch.Start();
    var db = new Database();
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    
    await Database.SeedPictures(db, "without transaction scope");
    stopwatch.Stop();
    AnsiConsole.WriteLine($"Without wrapping transaction: {stopwatch.Elapsed.TotalSeconds} seconds");
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
}

static void DeleteIfExists(string filename)
{
    if (File.Exists(filename))
    {
        File.Delete(filename);
    }    
}
