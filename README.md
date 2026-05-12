# Email MCP Server

A Model Context Protocol (MCP) server built with C# .NET and MailKit that provides email tools.

## Tools

- **SendEmail** – Send an email via SMTP
- **ReadEmails** – Read recent emails from an IMAP inbox
- **SearchEmails** – Search emails by subject or sender

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

The MCP endpoint will be available at `http://localhost:5000/mcp`.

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

## MCP Client Configuration

Add to your MCP client config (e.g. `mcp.json`):

```json
{
  "mcpServers": {
    "email": {
      "command": "dotnet",
      "args": ["run", "--project", "/home/arayeshi/email-mcp/EmailMcp"]
    }
  }
}
```
