using EmailMcp;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailMcp.Tests;

/// <summary>
/// Integration tests that run against a real IMAP/SMTP server.
/// Set environment variables to enable:
///   EMAIL_TEST_HOST, EMAIL_TEST_USER, EMAIL_TEST_PASS
/// 
/// For local testing, use MailHog (SMTP:1025, IMAP:1143) or GreenMail.
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    private readonly IOptions<EmailSettings>? _options;
    private readonly bool _canRun;

    public IntegrationTests()
    {
        var host = Environment.GetEnvironmentVariable("EMAIL_TEST_HOST");
        var user = Environment.GetEnvironmentVariable("EMAIL_TEST_USER");
        var pass = Environment.GetEnvironmentVariable("EMAIL_TEST_PASS");

        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user))
        {
            _canRun = true;
            _options = Options.Create(new EmailSettings
            {
                SmtpHost = host,
                SmtpPort = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_TEST_SMTP_PORT"), out var sp) ? sp : 587,
                ImapHost = host,
                ImapPort = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_TEST_IMAP_PORT"), out var ip) ? ip : 993,
                Username = user,
                Password = pass ?? "",
                UseSsl = Environment.GetEnvironmentVariable("EMAIL_TEST_SSL") != "false"
            });
        }
    }

    [SkippableFact]
    public async Task SendEmail_And_ReadEmails_RoundTrip()
    {
        Skip.If(!_canRun, "Email test server not configured");

        var subject = $"Test-{Guid.NewGuid():N}";
        var sendResult = await EmailTools.SendEmail(
            _options!, _options!.Value.Username, _options.Value.Username,
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
        Skip.If(!_canRun, "Email test server not configured");

        var result = await EmailTools.SearchEmails(_options!, null, null, null, "INBOX", null, null, null, 5, 0);
        Assert.Contains("\"total\"", result);
        Assert.Contains("\"emails\"", result);
    }

    [SkippableFact]
    public async Task GetEmail_ReturnsBodyContent()
    {
        Skip.If(!_canRun, "Email test server not configured");

        var result = await EmailTools.GetEmail(_options!, 0, "INBOX");
        Assert.Contains("\"body\"", result);
        Assert.Contains("\"subject\"", result);
    }

    [SkippableFact]
    public async Task ListFolders_ReturnsFolderNames()
    {
        Skip.If(!_canRun, "Email test server not configured");

        var result = await EmailTools.ListFolders(_options!);
        Assert.False(string.IsNullOrEmpty(result));
    }

    [SkippableFact]
    public async Task MessageCounts_ReturnsTotalAndUnread()
    {
        Skip.If(!_canRun, "Email test server not configured");

        var result = await EmailTools.MessageCounts(_options!, "INBOX");
        Assert.Contains("Total:", result);
        Assert.Contains("Unread:", result);
    }

    [SkippableFact]
    public async Task CreateFolder_And_DeleteFolder()
    {
        Skip.If(!_canRun, "Email test server not configured");

        var folderName = $"Test_{Guid.NewGuid():N}"[..20];

        var createResult = await EmailTools.CreateFolder(_options!, folderName);
        Assert.Contains("created", createResult);

        var deleteResult = await EmailTools.DeleteFolder(_options!, folderName);
        Assert.Contains("deleted", deleteResult);
    }

    [SkippableFact]
    public async Task CreateDraft_SavesToDrafts()
    {
        Skip.If(!_canRun, "Email test server not configured");

        var subject = $"Draft-{Guid.NewGuid():N}";
        var result = await EmailTools.CreateDraft(
            _options!, _options!.Value.Username, _options.Value.Username,
            subject, "Draft body");

        Assert.Contains("Draft saved", result);
    }
}
