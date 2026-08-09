FROM mcr.microsoft.com/dotnet/sdk:10.0.100-noble AS build
WORKDIR /workspace
COPY . .
RUN dotnet restore src/FreizeitCockpit.Cleanup/FreizeitCockpit.Cleanup.csproj --locked-mode
RUN dotnet publish src/FreizeitCockpit.Cleanup/FreizeitCockpit.Cleanup.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0.10-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
USER $APP_UID
ENTRYPOINT ["dotnet", "FreizeitCockpit.Cleanup.dll"]
