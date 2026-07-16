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

RUN dotnet publish src/EHub.Api/EHub.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EHub.Api.dll"]
