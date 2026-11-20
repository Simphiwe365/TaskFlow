FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TaskFlow.API/TaskFlow.API.csproj TaskFlow.API/
RUN dotnet restore TaskFlow.API/TaskFlow.API.csproj

COPY TaskFlow.API/ TaskFlow.API/
RUN dotnet publish TaskFlow.API/TaskFlow.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP_UID=1654
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /var/data && chown -R $APP_UID:$APP_UID /var/data
USER $APP_UID

EXPOSE 10000
ENTRYPOINT ["sh", "-c", "exec dotnet TaskFlow.API.dll --urls http://0.0.0.0:${PORT:-10000}"]
