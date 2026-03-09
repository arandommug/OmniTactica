using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ListBuilder.AppCode.Utilities
{
    internal class SqlMapper
    {
        public static async Task<List<T>> QueryAsync<T>(SqliteCommand cmd) where T : new()
        {
            var list = new List<T>();

            await using var reader = await cmd.ExecuteReaderAsync();

            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var colLookup = Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(reader.GetName, i => i, StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync())
            {
                var obj = new T();

                foreach (var prop in props)
                {
                    if (colLookup.TryGetValue(prop.Name, out int ordinal))
                    {
                        if (!reader.IsDBNull(ordinal))
                        {
                            var val = reader.GetValue(ordinal);
                            prop.SetValue(obj, Convert.ChangeType(val, prop.PropertyType));
                        }
                    }
                }

                list.Add(obj);
            }

            return list;
        }
    }
}
