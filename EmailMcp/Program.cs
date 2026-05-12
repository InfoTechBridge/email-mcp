using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using EmailMcp;

var useHttp = args.Contains("--http");

if (useHttp)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
    builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    var app = builder.Build();
    app.MapMcp();
    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
    builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    await builder.Build().RunAsync();
}
