FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/EHub.Api/EHub.Api.csproj src/EHub.Api/
COPY src/EHub.Application/EHub.Application.csproj src/EHub.Application/
COPY src/EHub.Contracts/EHub.Contracts.csproj src/EHub.Contracts/
COPY src/EHub.Domain/EHub.Domain.csproj src/EHub.Domain/
COPY src/EHub.Infrastructure/EHub.Infrastructure.csproj src/EHub.Infrastructure/
COPY src/EHub.Shared/EHub.Shared.csproj src/EHub.Shared/

RUN dotnet restore src/EHub.Api/EHub.Api.csproj

COPY . .

# The deploy scripts check out with umask 077, so copied files such as the mentor model
# arrive as 0600. The runtime user is not root and must still be able to read them.
RUN dotnet publish src/EHub.Api/EHub.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    && chmod -R a+rX /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        fonts-dejavu-core \
        fonts-liberation \
        libreoffice-core \
        libreoffice-impress \
        libreoffice-writer \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV HEALTHCHECK_HOST=localhost
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error --header "Host: ${HEALTHCHECK_HOST}" --header "X-Forwarded-Proto: https" http://localhost:8080/health/ready || exit 1

USER $APP_UID

ENTRYPOINT ["dotnet", "EHub.Api.dll"]
