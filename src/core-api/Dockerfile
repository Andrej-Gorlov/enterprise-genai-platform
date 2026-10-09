FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/core-api/EnterpriseGenAI.Core.Api.csproj src/core-api/

RUN dotnet restore src/core-api/EnterpriseGenAI.Core.Api.csproj

COPY src/core-api/ src/core-api/

RUN dotnet publish src/core-api/EnterpriseGenAI.Core.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "EnterpriseGenAI.Core.Api.dll"]