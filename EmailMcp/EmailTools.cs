using System.ComponentModel;
using MailKit.Net.Smtp;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using EmailMcp;

[McpServerToolType]
public static class EmailTools
{
    [McpServerTool, Description("Send an email via SMTP with optional file attachments")]
    public static async Task<string> SendEmail(
        IOptions<EmailSettings> options,
        [Description("Sender email address")] string from,
        [Description("Recipient email address")] string to,
        [Description("Email subject")] string subject,
        [Description("Email body (plain text or HTML)")] string body,
        [Description("Set to true if body is HTML")] bool isHtml = false,
        [Description("Comma-separated CC email addresses")] string? cc = null,
        [Description("Comma-separated BCC email addresses")] string? bcc = null,
        [Description("Comma-separated list of file paths to attach")] string? attachments = null)
    {
        var cfg = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrEmpty(cc))
            foreach (var addr in cc.Split(',', StringSplitOptions.TrimEntries))
                message.Cc.Add(MailboxAddress.Parse(addr));
        if (!string.IsNullOrEmpty(bcc))
            foreach (var addr in bcc.Split(',', StringSplitOptions.TrimEntries))
                message.Bcc.Add(MailboxAddress.Parse(addr));
        message.Subject = subject;

        var textPart = new TextPart(isHtml ? "html" : "plain") { Text = body };

        if (string.IsNullOrEmpty(attachments))
        {
            message.Body = textPart;
        }
        else
        {
            var multipart = new Multipart("mixed") { textPart };
            foreach (var path in attachments.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var attachment = new MimePart()
                {
                    Content = new MimeContent(File.OpenRead(path)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = Path.GetFileName(path)
                };
                multipart.Add(attachment);
            }
            message.Body = multipart;
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(cfg.SmtpHost, cfg.SmtpPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        return $"Email sent successfully to {to}";
    }

    [McpServerTool, Description("Read emails from an IMAP mailbox folder with pagination, returns JSON")]
    public static async Task<string> ReadEmails(
        IOptions<EmailSettings> options,
        [Description("Folder to read from (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX",
        [Description("Number of emails to fetch")] int count = 5,
        [Description("Number of emails to skip (for pagination)")] int skip = 0)
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);

        var emails = new List<object>();
        int end = folder.Count - 1 - skip;
        int start = Math.Max(0, end - count + 1);

        for (int i = end; i >= start; i--)
        {
            var msg = await folder.GetMessageAsync(i);
            emails.Add(new
            {
                index = folder.Count - 1 - i,
                from = msg.From.ToString(),
                to = msg.To.ToString(),
                subject = msg.Subject,
                date = msg.Date.ToString("o"),
                hasAttachments = msg.Attachments.Any()
            });
        }

        await client.DisconnectAsync(true);

        var result = new { folder = folderName, total = folder.Count, skip, count = emails.Count, emails };
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get full email details including body by index, returns JSON")]
    public static async Task<string> GetEmail(
        IOptions<EmailSettings> options,
        [Description("Index of the email (0-based from most recent)")] int emailIndex,
        [Description("Folder to read from (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "{\"error\": \"Invalid email index.\"}";

        var msg = await folder.GetMessageAsync(index);
        await client.DisconnectAsync(true);

        var result = new
        {
            index = emailIndex,
            from = msg.From.ToString(),
            to = msg.To.ToString(),
            cc = msg.Cc.ToString(),
            subject = msg.Subject,
            date = msg.Date.ToString("o"),
            body = msg.TextBody ?? msg.HtmlBody ?? "",
            isHtml = msg.TextBody == null && msg.HtmlBody != null,
            attachments = msg.Attachments.Select(a => a.ContentDisposition?.FileName ?? a.ContentType.Name ?? "unnamed").ToList()
        };
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Search emails in IMAP mailbox by subject, sender, recipient, body, date, or flags, returns JSON")]
    public static async Task<string> SearchEmails(
        IOptions<EmailSettings> options,
        [Description("Search term to look for in subject")] string? subject = null,
        [Description("Search term to look for in sender")] string? from = null,
        [Description("Search term to look for in recipient (useful for searching Sent folder)")] string? to = null,
        [Description("Search for words or phrases in the email body")] string? body = null,
        [Description("Folder to search in (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX",
        [Description("Only emails after this date (yyyy-MM-dd)")] string? after = null,
        [Description("Only emails before this date (yyyy-MM-dd)")] string? before = null,
        [Description("Filter by flag: Seen, Unseen, Flagged, Unflagged, Answered, Deleted")] string? flag = null,
        [Description("Number of emails to fetch")] int count = 5,
        [Description("Number of emails to skip (for pagination)")] int skip = 0)
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var inbox = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await inbox.OpenAsync(FolderAccess.ReadOnly);

        var query = SearchQuery.All;
        if (!string.IsNullOrEmpty(subject))
            query = SearchQuery.And(query, SearchQuery.SubjectContains(subject));
        if (!string.IsNullOrEmpty(from))
            query = SearchQuery.And(query, SearchQuery.FromContains(from));
        if (!string.IsNullOrEmpty(to))
            query = SearchQuery.And(query, SearchQuery.ToContains(to));
        if (!string.IsNullOrEmpty(body))
            query = SearchQuery.And(query, SearchQuery.BodyContains(body));
        if (!string.IsNullOrEmpty(after) && DateTime.TryParse(after, out var afterDate))
            query = SearchQuery.And(query, SearchQuery.DeliveredAfter(afterDate));
        if (!string.IsNullOrEmpty(before) && DateTime.TryParse(before, out var beforeDate))
            query = SearchQuery.And(query, SearchQuery.DeliveredBefore(beforeDate));
        if (!string.IsNullOrEmpty(flag))
        {
            var flagQuery = flag.ToLowerInvariant() switch
            {
                "seen" => SearchQuery.Seen,
                "unseen" => SearchQuery.NotSeen,
                "flagged" => SearchQuery.Flagged,
                "unflagged" => SearchQuery.NotFlagged,
                "answered" => SearchQuery.Answered,
                "deleted" => SearchQuery.Deleted,
                _ => (SearchQuery?)null
            };
            if (flagQuery is not null)
                query = SearchQuery.And(query, flagQuery);
        }

        var uids = await inbox.SearchAsync(query);
        var emails = new List<object>();

        foreach (var uid in uids.Reverse().Skip(skip).Take(count))
        {
            var msg = await inbox.GetMessageAsync(uid);
            emails.Add(new
            {
                index = uids.IndexOf(uid),
                from = msg.From.ToString(),
                to = msg.To.ToString(),
                subject = msg.Subject,
                date = msg.Date.ToString("o"),
                hasAttachments = msg.Attachments.Any()
            });
        }

        await client.DisconnectAsync(true);

        var result = new { total = uids.Count, skip, count = emails.Count, emails };
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Reply to an email with proper threading headers (In-Reply-To, References)")]
    public static async Task<string> ReplyToEmail(
        IOptions<EmailSettings> options,
        [Description("Sender email address")] string from,
        [Description("Index of the email to reply to (0-based from most recent)")] int emailIndex,
        [Description("Reply body (plain text or HTML)")] string body,
        [Description("Set to true if body is HTML")] bool isHtml = false,
        [Description("Comma-separated CC email addresses")] string? cc = null,
        [Description("Comma-separated BCC email addresses")] string? bcc = null,
        [Description("Folder to read from (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var imapClient = new ImapClient();
        await imapClient.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await imapClient.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? imapClient.Inbox!
            : await imapClient.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "Invalid email index.";

        var original = await folder.GetMessageAsync(index);
        await imapClient.DisconnectAsync(true);

        var reply = new MimeMessage();
        reply.From.Add(MailboxAddress.Parse(from));
        reply.To.AddRange(original.ReplyTo.Count > 0 ? original.ReplyTo : original.From);
        reply.Cc.AddRange(original.Cc);
        reply.Bcc.AddRange(original.Bcc);
        if (!string.IsNullOrEmpty(cc))
            foreach (var addr in cc.Split(',', StringSplitOptions.TrimEntries))
                reply.Cc.Add(MailboxAddress.Parse(addr));
        if (!string.IsNullOrEmpty(bcc))
            foreach (var addr in bcc.Split(',', StringSplitOptions.TrimEntries))
                reply.Bcc.Add(MailboxAddress.Parse(addr));
        reply.Subject = original.Subject?.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) == true
            ? original.Subject
            : $"Re: {original.Subject}";
        reply.InReplyTo = original.MessageId;
        foreach (var id in original.References)
            reply.References.Add(id);
        if (original.MessageId is not null)
            reply.References.Add(original.MessageId);
        reply.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        using var smtpClient = new SmtpClient();
        await smtpClient.ConnectAsync(cfg.SmtpHost, cfg.SmtpPort, cfg.UseSsl);
        await smtpClient.AuthenticateAsync(cfg.Username, cfg.Password);
        await smtpClient.SendAsync(reply);
        await smtpClient.DisconnectAsync(true);

        return $"Reply sent to {reply.To}";
    }

    [McpServerTool, Description("Forward an email with original content inline")]
    public static async Task<string> ForwardEmail(
        IOptions<EmailSettings> options,
        [Description("Sender email address")] string from,
        [Description("Recipient email address to forward to")] string to,
        [Description("Index of the email to forward (0-based from most recent)")] int emailIndex,
        [Description("Optional message to prepend")] string? message = null,
        [Description("Comma-separated CC email addresses")] string? cc = null,
        [Description("Comma-separated BCC email addresses")] string? bcc = null,
        [Description("Folder to read from (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var imapClient = new ImapClient();
        await imapClient.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await imapClient.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? imapClient.Inbox!
            : await imapClient.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "Invalid email index.";

        var original = await folder.GetMessageAsync(index);
        await imapClient.DisconnectAsync(true);

        var forward = new MimeMessage();
        forward.From.Add(MailboxAddress.Parse(from));
        forward.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrEmpty(cc))
            foreach (var addr in cc.Split(',', StringSplitOptions.TrimEntries))
                forward.Cc.Add(MailboxAddress.Parse(addr));
        if (!string.IsNullOrEmpty(bcc))
            foreach (var addr in bcc.Split(',', StringSplitOptions.TrimEntries))
                forward.Bcc.Add(MailboxAddress.Parse(addr));
        forward.Subject = $"Fwd: {original.Subject}";

        var forwardBody = $"{message ?? ""}\n\n---------- Forwarded message ----------\nFrom: {original.From}\nDate: {original.Date}\nSubject: {original.Subject}\nTo: {original.To}\n\n{original.TextBody}";
        forward.Body = new TextPart("plain") { Text = forwardBody };

        using var smtpClient = new SmtpClient();
        await smtpClient.ConnectAsync(cfg.SmtpHost, cfg.SmtpPort, cfg.UseSsl);
        await smtpClient.AuthenticateAsync(cfg.Username, cfg.Password);
        await smtpClient.SendAsync(forward);
        await smtpClient.DisconnectAsync(true);

        return $"Email forwarded to {to}";
    }

    [McpServerTool, Description("Download attachments from an email to a local directory")]
    public static async Task<string> DownloadAttachments(
        IOptions<EmailSettings> options,
        [Description("Index of the email (0-based from most recent)")] int emailIndex,
        [Description("Directory path to save attachments to")] string outputDir,
        [Description("Folder to read from (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "Invalid email index.";

        var msg = await folder.GetMessageAsync(index);
        await client.DisconnectAsync(true);

        Directory.CreateDirectory(outputDir);
        var saved = new List<string>();

        foreach (var attachment in msg.Attachments)
        {
            var fileName = attachment.ContentDisposition?.FileName
                ?? attachment.ContentType.Name
                ?? $"attachment_{saved.Count}";
            var filePath = Path.Combine(outputDir, fileName);

            if (attachment is MimePart { Content: not null } part)
            {
                using var stream = File.Create(filePath);
                await part.Content.DecodeToAsync(stream);
                saved.Add(fileName);
            }
        }

        return saved.Count > 0
            ? $"Saved {saved.Count} attachment(s): {string.Join(", ", saved)}"
            : "No attachments found in this email.";
    }

    [McpServerTool, Description("Create a new IMAP mailbox folder")]
    public static async Task<string> CreateFolder(
        IOptions<EmailSettings> options,
        [Description("Name of the folder to create")] string folderName)
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var toplevel = client.GetFolder(client.PersonalNamespaces[0]);
        await toplevel.CreateAsync(folderName, true);
        await client.DisconnectAsync(true);

        return $"Folder '{folderName}' created.";
    }

    [McpServerTool, Description("Delete an IMAP mailbox folder")]
    public static async Task<string> DeleteFolder(
        IOptions<EmailSettings> options,
        [Description("Name of the folder to delete")] string folderName)
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = await client.GetFolderAsync(folderName);
        await folder.DeleteAsync();
        await client.DisconnectAsync(true);

        return $"Folder '{folderName}' deleted.";
    }

    [McpServerTool, Description("Rename an IMAP mailbox folder")]
    public static async Task<string> RenameFolder(
        IOptions<EmailSettings> options,
        [Description("Current folder name")] string oldName,
        [Description("New folder name")] string newName)
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = await client.GetFolderAsync(oldName);
        var toplevel = client.GetFolder(client.PersonalNamespaces[0]);
        await folder.RenameAsync(toplevel, newName);
        await client.DisconnectAsync(true);

        return $"Folder renamed from '{oldName}' to '{newName}'.";
    }

    [McpServerTool, Description("Move an email to a different IMAP folder")]
    public static async Task<string> MoveToFolder(
        IOptions<EmailSettings> options,
        [Description("Index of the email to move (0-based from most recent)")] int emailIndex,
        [Description("Destination folder name")] string destinationFolder,
        [Description("Source folder (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadWrite);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "Invalid email index.";

        var destination = await client.GetFolderAsync(destinationFolder);
        await folder.MoveToAsync(index, destination);
        await client.DisconnectAsync(true);

        return $"Email moved from '{folderName}' to '{destinationFolder}'.";
    }

    [McpServerTool, Description("List all IMAP mailbox folders")]
    public static async Task<string> ListFolders(IOptions<EmailSettings> options)
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var personal = client.GetFolder(client.PersonalNamespaces[0]);
        var folders = await personal.GetSubfoldersAsync(true);
        await client.DisconnectAsync(true);

        return folders.Count > 0
            ? string.Join("\n", folders.Select(f => f.FullName))
            : "No folders found.";
    }

    [McpServerTool, Description("Get message counts (total and unread) for a mailbox folder")]
    public static async Task<string> MessageCounts(
        IOptions<EmailSettings> options,
        [Description("Folder name (defaults to Inbox)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);

        var unread = await folder.SearchAsync(SearchQuery.NotSeen);
        var total = folder.Count;
        await client.DisconnectAsync(true);

        return $"Folder: {folderName}\nTotal: {total}\nUnread: {unread.Count}";
    }

    [McpServerTool, Description("Add flags to an email (e.g. Seen, Flagged, Deleted)")]
    public static async Task<string> AddFlags(
        IOptions<EmailSettings> options,
        [Description("Index of the email (0-based from most recent)")] int emailIndex,
        [Description("Comma-separated flags to add (Seen, Answered, Flagged, Deleted, Draft)")] string flags,
        [Description("Folder (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadWrite);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "Invalid email index.";

        var messageFlags = ParseFlags(flags);
        await folder.AddFlagsAsync(index, messageFlags, true);
        await client.DisconnectAsync(true);

        return $"Flags added: {flags}";
    }

    [McpServerTool, Description("Remove flags from an email (e.g. Seen, Flagged, Deleted)")]
    public static async Task<string> RemoveFlags(
        IOptions<EmailSettings> options,
        [Description("Index of the email (0-based from most recent)")] int emailIndex,
        [Description("Comma-separated flags to remove (Seen, Answered, Flagged, Deleted, Draft)")] string flags,
        [Description("Folder (e.g. INBOX, Sent, Drafts)")] string folderName = "INBOX")
    {
        var cfg = options.Value;
        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox!
            : await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadWrite);

        int index = folder.Count - 1 - emailIndex;
        if (index < 0 || index >= folder.Count)
            return "Invalid email index.";

        var messageFlags = ParseFlags(flags);
        await folder.RemoveFlagsAsync(index, messageFlags, true);
        await client.DisconnectAsync(true);

        return $"Flags removed: {flags}";
    }

    private static MessageFlags ParseFlags(string flags)
    {
        var result = MessageFlags.None;
        foreach (var flag in flags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            result |= flag.ToLowerInvariant() switch
            {
                "seen" => MessageFlags.Seen,
                "answered" => MessageFlags.Answered,
                "flagged" => MessageFlags.Flagged,
                "deleted" => MessageFlags.Deleted,
                "draft" => MessageFlags.Draft,
                _ => MessageFlags.None
            };
        }
        return result;
    }

    [McpServerTool, Description("Create a draft email and save it to the Drafts folder")]
    public static async Task<string> CreateDraft(
        IOptions<EmailSettings> options,
        [Description("Sender email address")] string from,
        [Description("Recipient email address")] string to,
        [Description("Email subject")] string subject,
        [Description("Email body (plain text or HTML)")] string body,
        [Description("Set to true if body is HTML")] bool isHtml = false,
        [Description("Comma-separated CC email addresses")] string? cc = null,
        [Description("Comma-separated BCC email addresses")] string? bcc = null,
        [Description("Comma-separated list of file paths to attach")] string? attachments = null)
    {
        var cfg = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrEmpty(cc))
            foreach (var addr in cc.Split(',', StringSplitOptions.TrimEntries))
                message.Cc.Add(MailboxAddress.Parse(addr));
        if (!string.IsNullOrEmpty(bcc))
            foreach (var addr in bcc.Split(',', StringSplitOptions.TrimEntries))
                message.Bcc.Add(MailboxAddress.Parse(addr));
        message.Subject = subject;

        var textPart = new TextPart(isHtml ? "html" : "plain") { Text = body };

        if (string.IsNullOrEmpty(attachments))
        {
            message.Body = textPart;
        }
        else
        {
            var multipart = new Multipart("mixed") { textPart };
            foreach (var path in attachments.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var attachment = new MimePart()
                {
                    Content = new MimeContent(File.OpenRead(path)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = Path.GetFileName(path)
                };
                multipart.Add(attachment);
            }
            message.Body = multipart;
        }

        using var client = new ImapClient();
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, cfg.UseSsl);
        await client.AuthenticateAsync(cfg.Username, cfg.Password);

        var drafts = client.GetFolder(SpecialFolder.Drafts)
            ?? await client.GetFolderAsync("Drafts");
        await drafts.OpenAsync(FolderAccess.ReadWrite);
        await drafts.AppendAsync(message, MessageFlags.Draft);
        await client.DisconnectAsync(true);

        return $"Draft saved: \"{subject}\" to {to}";
    }
}
