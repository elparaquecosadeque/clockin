FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY .editorconfig .editorconfig
COPY backend backend
WORKDIR /src/backend
RUN dotnet publish src/ClockIn.Api/ClockIn.Api.csproj -c Release -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && mkdir -p /app && chown app:app /app
WORKDIR /app
COPY --from=build --chown=app:app /out .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=6 CMD curl -fsS http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "ClockIn.Api.dll"]
