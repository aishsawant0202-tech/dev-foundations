# Builds ReminderAgent.McpServer only.
#
# Deliberately excludes ReminderAgent.Cloud (the console REPL) and Test —
# only the three projects the MCP server actually needs are ever COPYed into
# the build context, regardless of what .dockerignore allows through:
#   ReminderAgent.McpServer     (this server)
#   ReminderAgent.Infrastructure (Azure SQL/Blob/Queue/Table implementations, ReminderTools)
#   ReminderAgent.Common         (shared interfaces/entities)
#
# Build from the solution root (the folder that contains this Dockerfile's
# parent, ReminderAgent.Common/, ReminderAgent.Infrastructure/, etc.) so the
# COPY paths below can see all three sibling projects:
#
#   docker build -t reminderagent-mcpserver -f ReminderAgent.McpServer/Dockerfile .

# ---- base: what actually ships, becomes the final runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# Azure App Service / any container host talks HTTP to the container and
# terminates TLS itself, so only the plain HTTP port is exposed here.
# ASPNETCORE_HTTP_PORTS defaults to 8080 in this base image, which makes
# Kestrel bind "http://+:8080" — i.e. all interfaces (0.0.0.0), not just
# localhost — so the container is reachable from outside without any extra
# UseUrls/Listen code in Program.cs.
EXPOSE 8080
# Run as the image's built-in non-root user rather than root.
USER $APP_UID

# ---- build: SDK image, compiles the three needed projects ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore first, copying only the .csproj files — lets Docker cache this
# layer and skip `dotnet restore` on rebuilds where only .cs files changed.
COPY ["ReminderAgent.McpServer/ReminderAgent.McpServer.csproj", "ReminderAgent.McpServer/"]
COPY ["ReminderAgent.Infrastructure/ReminderAgent.Infrastructure.csproj", "ReminderAgent.Infrastructure/"]
COPY ["ReminderAgent.Common/ReminderAgent.Common.csproj", "ReminderAgent.Common/"]
RUN dotnet restore "ReminderAgent.McpServer/ReminderAgent.McpServer.csproj"

# Now copy source for just those three projects. ReminderAgent.Cloud and
# Test are never referenced here, so they never enter the image even though
# they sit alongside these folders on disk.
COPY ReminderAgent.McpServer/ ReminderAgent.McpServer/
COPY ReminderAgent.Infrastructure/ ReminderAgent.Infrastructure/
COPY ReminderAgent.Common/ ReminderAgent.Common/

WORKDIR /src/ReminderAgent.McpServer
RUN dotnet build "ReminderAgent.McpServer.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ---- publish: trims to just what's needed to run ----
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ReminderAgent.McpServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# ---- final: base + published output, nothing else ----
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# No appsettings.dev.json is ever copied in (it's gitignored and never
# present in the build context) and there's no committed appsettings.json
# either yet — so ALL configuration (ConnectionStrings__SqlServer,
# ConnectionStrings__BlobStorage, ConnectionStrings__QueueStorage,
# ConnectionStrings__TableStorage, OpenAI__ApiKey) MUST be supplied as
# container environment variables (Azure App Service → Configuration →
# Application settings, using the double-underscore syntax). Program.cs
# throws immediately on startup if OpenAI:ApiKey is missing, so a
# misconfigured deployment fails fast and loudly instead of starting broken.
ENTRYPOINT ["dotnet", "ReminderAgent.McpServer.dll"]
