using Dapper;
using DynamicODataToSQL;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Data.Sqlite;
using SqlKata.Compilers;
using System.Collections.Specialized;
using System.Web;

internal static class Util
{
    public static SqliteConnection CreateConnection(string dbPath)
    {
        if (!File.Exists(dbPath)) {
            throw new Exception("Unable to locate db file.");
        }
        var connectionString = "Data Source=" + dbPath;
        return new SqliteConnection(connectionString);
    }

    public static async Task<string?> SanitizeTableNameAsync(this SqliteConnection connection, string table)
    {
        var tables = await connection.QueryAsync<string>("SELECT tbl_name FROM sqlite_schema WHERE type='table'");
        return tables
            .Where(tbl => tbl.ToLower() == table.ToLower())
            .SingleOrDefault();
    }

    public static (string Sql, IDictionary<string, object> Params) ToSql(this HttpRequest request, string tableName)
    {
        var converter = new ODataToSqlConverter(new EdmModelBuilder(), new SqliteCompiler());
        var uri = new Uri(request.GetDisplayUrl());
        var query = QueryToDictionary(uri.Query);
        bool count = false;
        if (query.ContainsKey("$count")) {
            if (!bool.TryParse(query["$count"], out count)) {
                throw new Exception("Invalid $count expression.");
            }
        } 
        var parsed = converter.ConvertToSQL(tableName, query, count);
        return parsed;
    }

    public static string GetUriRoot(this HttpRequest request)
    {
        return new Uri(request.GetDisplayUrl()).GetLeftPart(UriPartial.Authority);
    }

    private static IEnumerable<KeyValuePair<string, string>> AsKVPEnumerable(this NameValueCollection collection)
    {
        foreach (string key in collection.Keys) {
            yield return new KeyValuePair<string, string>(key, collection[key]!);
        }
    }

    private static Dictionary<string, string> QueryToDictionary(string query)
    {
        return HttpUtility.ParseQueryString(query)
            .AsKVPEnumerable()
            .ToDictionary(
                keySelector: p => p.Key.ToLower(),
                elementSelector: p => p.Value
            );
    }
}
