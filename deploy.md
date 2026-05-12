
## Docker

```bash
docker build -t aarayeshi/email-mcp .
docker push aarayeshi/email-mcp
```

For Stdio mode:

```bash
docker run -i aarayeshi/email-mcp
# The -i flag is important since the MCP server communicates over stdio.
```

For HTTP mode:

```bash
docker run -p 5050:8080 aarayeshi/email-mcp -- --http
```

To override settings, mount your own config:

```bash
docker run -i -v ./appsettings.json:/app/appsettings.json aarayeshi/email-mcp
```

## MCP Client Configuration

Add to your MCP client config (e.g. VS Code):

```json
{
  "servers": {
		"email-mcp": {
			"url": "http://127.0.0.1:5050/",
			"type": "http"
		}
	}
}
```
