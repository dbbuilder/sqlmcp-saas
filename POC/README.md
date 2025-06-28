# SQLMCP.net Proof of Concept

A containerized C# console application that demonstrates natural language to T-SQL translation using Large Language Models (LLMs), executes queries against SQL Server, and maintains comprehensive audit logging.

## Prerequisites

- .NET 8 SDK
- Docker Desktop
- SQL Server instance (local or remote)
- OpenAI API key

## Quick Start

1. **Configure the application:**
   - Update the OpenAI API key in `config/config.json`
   - Update the SQL Server connection string in `config/config.json`

2. **Build the Docker image:**
   ```bash
   docker build -t sqlmcp-poc -f docker/Dockerfile .
   ```

3. **Run a basic test:**
   ```bash
   docker run --rm -it \
     -v "$(pwd)/config/config.json:/app/config.json:ro" \
     -v "$(pwd)/logs:/app/logs" \
     sqlmcp-poc "Show me all customers from California"
   ```

## Development

1. **Restore packages:**
   ```bash
   dotnet restore
   ```

2. **Build the solution:**
   ```bash
   dotnet build
   ```

3. **Run locally:**
   ```bash
   dotnet run --project src/SqlMcpPoc.ConsoleApp -- "your query here"
   ```
