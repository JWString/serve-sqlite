using Dapper;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;
using System.Net.Mime;
using System.Reflection;

var returnCode = 0;
bool allowAddressVerify = true;

try {

    #region CLI
    var cli = new CommandLineApplication<Program>();
    cli.HelpOption();
    var dbPathInput = cli
        .Option<string>("-p|--path", "Path to sqlite database file", CommandOptionType.SingleValue)
        .IsRequired();
    var httpInput = cli
        .Option<int?>("--http", "Port for binding to a local http address", CommandOptionType.SingleValue);
    var httpsInput = cli
        .Option<int?>("--https", "Port for binding to a local https address", CommandOptionType.SingleValue);
    httpInput.DefaultValue = null;
    httpsInput.DefaultValue = null;
    cli.Execute(args);
    if (!dbPathInput.HasValue()) {
        return 0;
    }
    #endregion

    #region INIT
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
    {
        ContentRootPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.FullName
    });
    builder.Services.AddHttpClient();

    if (!File.Exists(dbPathInput.ParsedValue)) {
        Console.WriteLine("Unable to locate database file at: " + dbPathInput.ParsedValue);
        return 1;
    }

    Console.Write(Environment.NewLine);
    Console.WriteLine("****************************************************************************");
    Console.WriteLine("**                             SQLite To REST                             **");
    Console.WriteLine("**              A read-only OData REST interface for SQLite               **");
    Console.WriteLine("****************************************************************************");
    Console.WriteLine("**  Note: This utility is intended for personal and development use only  **");
    Console.WriteLine("****************************************************************************");
    Console.Write(Environment.NewLine);
    Console.WriteLine("Data source:\t" + dbPathInput.ParsedValue);

    if (!httpInput.ParsedValue.HasValue && !httpsInput.ParsedValue.HasValue) {
        builder.WebHost.UseUrls(
            "http://[::1]:0",
            "https://[::1]:0"
        );
    } else {
        var urls = new List<string>();
        if (httpInput.ParsedValue.HasValue) {
            urls.Add($"http://[::1]:{httpInput.ParsedValue.Value}");
        }
        if (httpsInput.ParsedValue.HasValue) {
            urls.Add($"https://[::1]:{httpsInput.ParsedValue.Value}");
        }
        builder.WebHost.UseUrls(urls.ToArray());
    }

    var app = builder.Build();
    
    if (allowAddressVerify) {
        Task.Run(async () =>
        {
            await Task.Delay(1000);
            var http = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
            string? example = null;
            if (app.Urls.Any(s => s.StartsWith("http://"))) {
                var uri = new Uri(app.Urls.Where(s => s.StartsWith("http://")).Single());
                var root = $"http://localhost:{uri.Port}";
                var url = $"{root}/addressverification";
                var result = await http.PostAsync(url, null);
                if (!result.IsSuccessStatusCode) {
                    Console.WriteLine($"Unable to bind to address: {root}. Exiting application.");
                    await app.StopAsync();
                    returnCode = 1;
                    return;
                }
                example = root;
            }
            if (app.Urls.Any(s => s.StartsWith("https://"))) {
                var uri = new Uri(app.Urls.Where(s => s.StartsWith("https://")).Single());
                var root = $"https://localhost:{uri.Port}";
                var url = $"{root}/addressverification";
                var result = await http.PostAsync(url, null);
                if (!result.IsSuccessStatusCode) {
                    Console.WriteLine($"Unable to bind to address: {root}. Exiting application.");
                    await app.StopAsync();
                    returnCode = 1;
                    return;
                }
                example = root;
            }
            allowAddressVerify = false;
            Console.Write(Environment.NewLine);
            Console.WriteLine($"Usage:\t\t{example}/<entity-name>[?<query-string>]");
            Console.Write(Environment.NewLine);
            Console.WriteLine("Params:\t\t$select, $filter, $orderby, $top, $skip, $count");
            Console.Write(Environment.NewLine);
            Console.WriteLine("More info:\thttps://www.odata.org/getting-started/basic-tutorial/#queryData");
            Console.Write(Environment.NewLine);
        });
    }

    #endregion

    #region API
    app.MapPost("/addressverification", (
        HttpContext context
    ) => {
        if (!allowAddressVerify) {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }
        Console.WriteLine("Listening on:\t" + context.Request.GetUriRoot());
        context.Response.StatusCode = (int)HttpStatusCode.Created;
    });

    app.MapGet("/{table}", async (
        [FromRoute(Name = "table")] string table,
        HttpContext context
    ) => {
        try {
            Console.Write("Request:\t" + context.Request.GetDisplayUrl() + Environment.NewLine);
            using var connection = Util.CreateConnection(dbPathInput.ParsedValue);
            var tableName = await connection.SanitizeTableNameAsync(table);
            if (string.IsNullOrWhiteSpace(tableName)) {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            IEnumerable<object> result;
            try {
                var query = context.Request.ToSql(tableName);
                result = await connection.QueryAsync<object>(query.Sql, query.Params);
            } catch (Exception ex) {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = MediaTypeNames.Text.Plain;
                await context.Response.WriteAsync(ex.Message);
                return;
            }
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
        } catch (Exception ex) {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = MediaTypeNames.Text.Plain;
            await context.Response.WriteAsync(ex.Message);
        } finally {
            Console.Write("Response:\t" + context.Response.StatusCode.ToString() + Environment.NewLine);
            Console.Write(Environment.NewLine);
        }
    });
    #endregion

    app.Run();

} catch (Exception ex) {
    Console.WriteLine(ex.Message);
    return 1;
}

return returnCode;