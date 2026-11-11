# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the .csproj files for the projects this server actually needs
# (McpServer -> Infrastructure -> Common) so `dotnet restore` is cached as
# its own layer and doesn't re-run just because source files changed.
COPY ReminderAgent.McpServer/ReminderAgent.McpServer.csproj ReminderAgent.McpServer/
COPY ReminderAgent.Common/ReminderAgent.Common.csproj ReminderAgent.Common/
COPY ReminderAgent.Infrastructure/ReminderAgent.Infrastructure.csproj ReminderAgent.Infrastructure/

RUN dotnet restore ReminderAgent.McpServer/ReminderAgent.McpServer.csproj

# Now bring in the rest of the source for those same three projects only.
# ReminderAgent.Cloud/ and Test/ are never copied into the build context
# (see .dockerignore) and are not referenced here.
COPY ReminderAgent.McpServer/ ReminderAgent.McpServer/
COPY ReminderAgent.Common/ ReminderAgent.Common/
COPY ReminderAgent.Infrastructure/ ReminderAgent.Infrastructure/

RUN dotnet publish ReminderAgent.McpServer/ReminderAgent.McpServer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Bind Kestrel to all interfaces inside the container, not just localhost,
# so the host/orchestrator can actually reach port 8080.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ReminderAgent.McpServer.dll"]
