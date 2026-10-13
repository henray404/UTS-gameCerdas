@echo off
set PYTHONUTF8=1
set "MCP_SERVER=%~dp0Tools\MCPServer"
uv run --project "%MCP_SERVER%" mcp-for-unity --transport http --http-url http://127.0.0.1:8080
