using EmailMcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;
using MailKit.Net.Imap;

namespace EmailMcp.Tests;

/// <summary>
/// Integration tests that run against a real IMAP/SMTP server.
/// Configure via appsettings.test.json or environment variables.
/// Start GreenMail with: docker compose up -d
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    private readonly IOptions<EmailSettings> _options;
    private readonly bool _canRun;
    private readonly string? _skipReason;

    public IntegrationTests()
    {
        var basePath = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.test.json", optional: true)
            .Build();

        var settings = new EmailSettings();
        config.GetSection("Email").Bind(settings);

        _options = Options.Create(settings);
        var (reachable, error) = IsServerReachable(settings);
        _canRun = reachable;
        _skipReason = error ?? "Email test server not configured";
    }

    private static (bool reachable, string? error) IsServerReachable(EmailSettings settings)
    {
        if (string.IsNullOrEmpty(settings.ImapHost)) return (false, "ImapHost is empty");
        try
        {
            using var client = new ImapClient();
            client.Connect(settings.ImapHost, settings.ImapPort, settings.SecureSocketOption);
            client.Authenticate(settings.Username, settings.Password);
            client.Disconnect(true);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task SendEmail_And_ReadEmails_RoundTrip()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        var subject = $"Test-{Guid.NewGuid():N}";
        var sendResult = await EmailTools.SendEmail(
            _options!, "test@localhost", "test@localhost",
            subject, "Integration test body");

        Assert.Contains("sent successfully", sendResult);

        // Give server a moment to deliver
        await Task.Delay(2000);

        var readResult = await EmailTools.ReadEmails(_options, "INBOX", 5, 0);
        Assert.Contains(subject, readResult);
    }

    [SkippableFact]
    public async Task SearchEmails_ReturnsJson()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        var result = await EmailTools.SearchEmails(_options!, null, null, null, null, "INBOX", null, null, null, 5, 0);
        Assert.Contains("\"total\"", result);
        Assert.Contains("\"emails\"", result);
    }

    [SkippableFact]
    public async Task GetEmail_ReturnsBodyContent()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        // Ensure there's at least one email
        await EmailTools.SendEmail(_options!, "test@localhost", "test@localhost", "GetEmail Test", "body content");
        await Task.Delay(1000);

        var result = await EmailTools.GetEmail(_options!, 0, "INBOX");
        Assert.Contains("\"body\"", result);
        Assert.Contains("\"subject\"", result);
    }

    [SkippableFact]
    public async Task ListFolders_ReturnsFolderNames()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        var result = await EmailTools.ListFolders(_options!);
        Assert.False(string.IsNullOrEmpty(result));
    }

    [SkippableFact]
    public async Task MessageCounts_ReturnsTotalAndUnread()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        var result = await EmailTools.MessageCounts(_options!, "INBOX");
        Assert.Contains("Total:", result);
        Assert.Contains("Unread:", result);
    }

    [SkippableFact]
    public async Task CreateFolder_And_DeleteFolder()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        var folderName = $"Test_{Guid.NewGuid():N}"[..20];

        var createResult = await EmailTools.CreateFolder(_options!, folderName);
        Assert.Contains("created", createResult);

        var deleteResult = await EmailTools.DeleteFolder(_options!, folderName);
        Assert.Contains("deleted", deleteResult);
    }

    [SkippableFact]
    public async Task CreateDraft_SavesToDrafts()
    {
        Skip.If(!_canRun, _skipReason ?? "Email test server not configured");

        // Ensure Drafts folder exists
        try { await EmailTools.CreateFolder(_options!, "Drafts"); } catch { }

        var subject = $"Draft-{Guid.NewGuid():N}";
        var result = await EmailTools.CreateDraft(
            _options!, _options!.Value.Username, _options.Value.Username,
            subject, "Draft body");

        Assert.Contains("Draft saved", result);
    }
}
