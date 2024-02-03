using System.Collections.Frozen;
using Microsoft.Data.Sqlite;

namespace OoLunar.DocBot.Database
{
    public static partial class PreparedCommands
    {
        private static readonly SqliteConnection Connection;
        public static readonly FrozenDictionary<TagOperations, SqliteCommand> Tags;

        static PreparedCommands()
        {
            Connection = new(new SqliteConnectionStringBuilder()
            {
                Cache = SqliteCacheMode.Shared,
                DataSource = "res/database.db",
                Mode = SqliteOpenMode.Memory,
                Pooling = true
            }.ConnectionString);
            Connection.Open();

            Tags = PrepareTags();
        }

        private static SqliteParameter CreateParameter(SqliteCommand command, string name, SqliteType type, int size = default)
        {
            SqliteParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.SqliteType = type;
            parameter.Size = size;
            return parameter;
        }
    }
}
