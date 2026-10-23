FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/document-ingestion-worker/EnterpriseGenAI.DocumentIngestion.Worker.csproj src/document-ingestion-worker/

RUN dotnet restore src/document-ingestion-worker/EnterpriseGenAI.DocumentIngestion.Worker.csproj

COPY src/document-ingestion-worker/ src/document-ingestion-worker/

RUN dotnet publish src/document-ingestion-worker/EnterpriseGenAI.DocumentIngestion.Worker.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "EnterpriseGenAI.DocumentIngestion.Worker.dll"]