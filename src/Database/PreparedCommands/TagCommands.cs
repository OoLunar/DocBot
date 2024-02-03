using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace OoLunar.DocBot.Database
{
    public static partial class PreparedCommands
    {
        private static FrozenDictionary<TagOperations, SqliteCommand> PrepareTags()
        {
            SqliteCommand createTableCommand = Connection.CreateCommand();
            createTableCommand.CommandText = "CREATE TABLE IF NOT EXISTS tags (name TEXT PRIMARY KEY, content TEXT NOT NULL, aliases TEXT NOT NULL, history TEXT NOT NULL)";
            createTableCommand.ExecuteNonQuery();

            return new Dictionary<TagOperations, SqliteCommand>()
            {
                [TagOperations.Create] = CreateTagCommand(),
                [TagOperations.ReadContent] = ReadContentTagCommand(),
                [TagOperations.ReadAll] = ReadAllTagCommand(),
                [TagOperations.Update] = UpdateTagCommand(),
                [TagOperations.Delete] = DeleteTagCommand(),
                [TagOperations.List] = ListTagCommand(),
            }.ToFrozenDictionary();
        }

        private static SqliteCommand CreateTagCommand()
        {
            SqliteCommand command = Connection.CreateCommand();
            command.CommandText = "INSERT INTO tags (name, content, aliases, history) VALUES (@name, @content, @aliases, @history)";
            command.Parameters.Add(CreateParameter(command, "@name", SqliteType.Text));
            command.Parameters.Add(CreateParameter(command, "@content", SqliteType.Text));
            command.Parameters.Add(CreateParameter(command, "@aliases", SqliteType.Text));
            command.Parameters.Add(CreateParameter(command, "@history", SqliteType.Text));
            command.Prepare();
            return command;
        }

        private static SqliteCommand ReadContentTagCommand()
        {
            SqliteCommand command = Connection.CreateCommand();
            command.CommandText = "SELECT content FROM tags WHERE name = @name";
            command.Parameters.Add(CreateParameter(command, "@name", SqliteType.Text));
            command.Prepare();
            return command;
        }

        private static SqliteCommand ReadAllTagCommand()
        {
            SqliteCommand command = Connection.CreateCommand();
            command.CommandText = "SELECT * FROM tags WHERE name = @name";
            command.Parameters.Add(CreateParameter(command, "@name", SqliteType.Text));
            command.Prepare();
            return command;
        }

        private static SqliteCommand UpdateTagCommand()
        {
            SqliteCommand command = Connection.CreateCommand();
            command.CommandText = "UPDATE tags SET content = @content, aliases = @aliases, history = @history WHERE name = @name";
            command.Parameters.Add(CreateParameter(command, "@content", SqliteType.Text));
            command.Parameters.Add(CreateParameter(command, "@aliases", SqliteType.Text));
            command.Parameters.Add(CreateParameter(command, "@history", SqliteType.Text));
            command.Parameters.Add(CreateParameter(command, "@name", SqliteType.Text));
            command.Prepare();
            return command;
        }

        private static SqliteCommand DeleteTagCommand()
        {
            SqliteCommand command = Connection.CreateCommand();
            command.CommandText = "DELETE FROM tags WHERE name = @name";
            command.Parameters.Add(CreateParameter(command, "@name", SqliteType.Text));
            command.Prepare();
            return command;
        }

        private static SqliteCommand ListTagCommand()
        {
            SqliteCommand command = Connection.CreateCommand();
            command.CommandText = "SELECT name FROM tags";
            command.Prepare();
            return command;
        }
    }

    public enum TagOperations
    {
        Create,
        ReadContent,
        ReadAll,
        Update,
        Delete,
        List
    }
}
