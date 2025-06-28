# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/SqlMcpPoc.ConsoleApp/SqlMcpPoc.ConsoleApp.csproj", "src/SqlMcpPoc.ConsoleApp/"]
COPY ["src/SqlMcpPoc.Core/SqlMcpPoc.Core.csproj", "src/SqlMcpPoc.Core/"]
COPY ["src/SqlMcpPoc.Infrastructure/SqlMcpPoc.Infrastructure.csproj", "src/SqlMcpPoc.Infrastructure/"]
COPY ["src/SqlMcpPoc.Models/SqlMcpPoc.Models.csproj", "src/SqlMcpPoc.Models/"]
COPY ["src/SqlMcpPoc.Configuration/SqlMcpPoc.Configuration.csproj", "src/SqlMcpPoc.Configuration/"]

# Restore dependencies
RUN dotnet restore "src/SqlMcpPoc.ConsoleApp/SqlMcpPoc.ConsoleApp.csproj"

# Copy all source code
COPY . .

# Build and publish the application
WORKDIR "/src/src/SqlMcpPoc.ConsoleApp"
RUN dotnet publish "SqlMcpPoc.ConsoleApp.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Create the final, runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Create directories for logs
RUN mkdir -p /app/logs

# Set the entry point
ENTRYPOINT ["dotnet", "SqlMcpPoc.ConsoleApp.dll"]
