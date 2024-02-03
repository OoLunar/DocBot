using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OoLunar.DocBot.Entities;

namespace OoLunar.DocBot.Database
{
    public static class DatabaseTracker
    {
        public static bool CreateTag(string name, string content, IReadOnlyList<string>? aliases = null, IReadOnlyList<TagHistory>? history = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentException.ThrowIfNullOrEmpty(content, nameof(content));
            aliases ??= Array.Empty<string>();
            history ??= Array.Empty<TagHistory>();

            SqliteCommand command = PreparedCommands.Tags[TagOperations.Create];
            command.Parameters["@name"].Value = name;
            command.Parameters["@content"].Value = content;
            command.Parameters["@aliases"].Value = SerializeToJson(aliases);
            command.Parameters["@history"].Value = SerializeToJson(history);
            return command.ExecuteNonQuery() > 0;
        }

        public static string? GetTagContent(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

            SqliteCommand command = PreparedCommands.Tags[TagOperations.ReadContent];
            command.Parameters["@name"].Value = name;
            using SqliteDataReader reader = command.ExecuteReader();
            return !reader.Read() ? null : reader.GetString(0);
        }

        public static TagEntity? GetTag(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

            SqliteCommand command = PreparedCommands.Tags[TagOperations.ReadAll];
            command.Parameters["@name"].Value = name;
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            string content = reader.GetString(1);
            string aliasesJson = reader.GetString(2);
            string historyJson = reader.GetString(3);

            List<string> aliases = DeserializeFromJson<List<string>>(aliasesJson) ?? new();
            List<TagHistory> history = DeserializeFromJson<List<TagHistory>>(historyJson) ?? new();

            return new TagEntity(name, content, aliases, history);
        }

        public static bool UpdateTag(string name, string content, IReadOnlyList<string> aliases, IReadOnlyList<TagHistory> history)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentException.ThrowIfNullOrEmpty(content, nameof(content));
            ArgumentNullException.ThrowIfNull(aliases, nameof(aliases));
            ArgumentNullException.ThrowIfNull(history, nameof(history));

            SqliteCommand command = PreparedCommands.Tags[TagOperations.Update];
            command.Parameters["@name"].Value = name;
            command.Parameters["@content"].Value = content;
            command.Parameters["@aliases"].Value = SerializeToJson(aliases);
            command.Parameters["@history"].Value = SerializeToJson(history);
            return command.ExecuteNonQuery() > 0;
        }

        public static bool DeleteTag(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

            SqliteCommand command = PreparedCommands.Tags[TagOperations.Delete];
            command.Parameters["@name"].Value = name;
            return command.ExecuteNonQuery() > 0;
        }

        public static IEnumerable<string> GetAllTags()
        {
            SqliteCommand command = PreparedCommands.Tags[TagOperations.List];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                yield return reader.GetString(0);
            }
        }

        private static string SerializeToJson<T>(T obj) => JsonSerializer.Serialize(obj);
        private static T? DeserializeFromJson<T>(string json) => JsonSerializer.Deserialize<T>(json);
    }
}
