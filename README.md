# Email MCP Server

A Model Context Protocol (MCP) server built with C# .NET and MailKit that provides email tools.

## Tools

| Tool | Description |
|------|-------------|
| **SendEmail** | Send an email with optional attachments, CC/BCC, HTML body |
| **ReadEmails** | Read emails from any folder with pagination (JSON) |
| **GetEmail** | Get full email details including body |
| **SearchEmails** | Search by subject, sender, recipient, body, date, flags with pagination (JSON) |
| **ReplyToEmail** | Reply with threading headers, preserves original CC |
| **ForwardEmail** | Forward with original content inline |
| **DownloadAttachments** | Download attachments to a local directory |
| **CreateDraft** | Save a draft email with optional attachments |
| **CreateFolder** | Create an IMAP mailbox folder |
| **DeleteFolder** | Delete an IMAP mailbox folder |
| **RenameFolder** | Rename an IMAP mailbox folder |
| **MoveToFolder** | Move an email to a different folder |
| **ListFolders** | List all mailbox folders |
| **MessageCounts** | Get total and unread counts for a folder |
| **AddFlags** | Add flags to an email (Seen, Flagged, Deleted, etc.) |
| **RemoveFlags** | Remove flags from an email |

## Configuration

Edit `EmailMcp/appsettings.json` with your email credentials:

```json
{
  "Email": {
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "ImapHost": "imap.example.com",
    "ImapPort": 993,
    "Username": "your-email@example.com",
    "Password": "your-password",
    "UseSsl": true
  }
}
```

## Build & Run

```bash
cd EmailMcp
dotnet build
dotnet run
```

### HTTP mode

```bash
dotnet run --project EmailMcp -- --http
```

The MCP endpoint will be available at `http://localhost:5000/`.

### Stdio mode (default)

```bash
dotnet run --project EmailMcp
```

## Docker

```bash
docker build -t email-mcp .
docker run -i email-mcp
# The -i flag is important since the MCP server communicates over stdio.
```

For HTTP mode:

```bash
docker run -p 5000:8080 email-mcp -- --http
```

To override settings, mount your own config:

```bash
docker run -i -v ./appsettings.json:/app/appsettings.json email-mcp
```

## Docker Compose (with GreenMail for testing)

```bash
docker compose up -d
```

This starts:
- **GreenMail** – SMTP (port 3025) and IMAP (port 3143) test server
- **email-mcp** – MCP server in HTTP mode (port 5000)

## Testing

```bash
# Run all tests
dotnet test

# Unit tests only
dotnet test --filter "Category!=Integration"

# Integration tests only (requires GreenMail running)
docker compose up -d
dotnet test --filter "Category=Integration"
```

Integration tests use `EmailMcp.Tests/appsettings.test.json` and automatically skip when the mail server is not reachable.

## MCP Client Configuration

Add to your MCP client config (e.g. `mcp.json`):

```json
{
  "mcpServers": {
    "email": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/email-mcp/EmailMcp"]
    }
  }
}
```

VS Code:

```json
{
  "servers": {
		"email-mcp": {
			"url": "http://127.0.0.1:5000/",
			"type": "http"
		}
	}
}
```

## License

MIT
