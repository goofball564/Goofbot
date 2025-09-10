namespace Goofbot.Utils;

using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading;

internal abstract class GoofbotModule : IDisposable
{
    protected readonly string moduleDataFolder;
    protected readonly Bot bot;
    protected readonly SqliteConnection sqliteConnection;


    private const string ModuleDatabaseFile = "data.db";

    protected GoofbotModule(Bot bot, string moduleDataFolder)
    {
        this.bot = bot;

        this.moduleDataFolder = Path.Join(this.bot.StuffFolder, moduleDataFolder);
        Directory.CreateDirectory(this.moduleDataFolder);

        SqliteConnectionStringBuilder connectionStringBuilder = [];
        connectionStringBuilder.DataSource = Path.Join(this.moduleDataFolder, ModuleDatabaseFile);

        this.sqliteConnection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        this.sqliteConnection.Open();
    }

    public virtual void Dispose()
    {
        this.sqliteConnection.Dispose();
    }
}
