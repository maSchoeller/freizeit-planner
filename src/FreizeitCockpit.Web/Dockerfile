FROM node:24.11.1-bookworm-slim AS frontend
WORKDIR /workspace
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY src/Web/package.json src/Web/package.json
COPY src/Help/package.json src/Help/package.json
COPY src/Web src/Web
COPY src/Help src/Help
RUN rm -rf node_modules src/Web/node_modules src/Help/node_modules \
    && npm exec --yes pnpm@11.20.0 -- install --frozen-lockfile --prod=false
RUN npm exec --yes pnpm@11.20.0 -- --filter @freizeit-cockpit/web build \
    && npm exec --yes pnpm@11.20.0 -- --filter @freizeit-cockpit/help build

FROM mcr.microsoft.com/dotnet/sdk:10.0.100-noble AS build
WORKDIR /workspace
COPY . .
COPY --from=frontend /workspace/src/FreizeitCockpit.Web/wwwroot src/FreizeitCockpit.Web/wwwroot
RUN dotnet restore src/FreizeitCockpit.Web/FreizeitCockpit.Web.csproj --locked-mode
RUN dotnet publish src/FreizeitCockpit.Web/FreizeitCockpit.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FreizeitCockpit.Web.dll"]
