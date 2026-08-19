FROM node:20-bookworm-slim AS web
WORKDIR /workspace/src/WebhookStudio.Web
COPY src/WebhookStudio.Web/package*.json ./
RUN npm ci
COPY src/WebhookStudio.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS publish
WORKDIR /src
COPY NuGet.Config WebhookStudio.sln ./
COPY src/WebhookStudio.Api/WebhookStudio.Api.csproj src/WebhookStudio.Api/
RUN dotnet restore src/WebhookStudio.Api/WebhookStudio.Api.csproj --configfile NuGet.Config
COPY src/WebhookStudio.Api/ src/WebhookStudio.Api/
COPY --from=web /workspace/src/WebhookStudio.Api/wwwroot/ src/WebhookStudio.Api/wwwroot/
RUN dotnet publish src/WebhookStudio.Api/WebhookStudio.Api.csproj -c Release -o /app --no-restore --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && useradd --system --uid 10001 --create-home webhookstudio
WORKDIR /app
COPY --from=publish --chown=webhookstudio:webhookstudio /app ./
USER webhookstudio
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 ConnectionStrings__Studio="Data Source=/data/webhook-studio.db;Foreign Keys=True;Default Timeout=5"
VOLUME ["/data"]
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 CMD ["curl","--fail","--silent","http://127.0.0.1:8080/health/ready"]
ENTRYPOINT ["dotnet","WebhookStudio.dll"]
