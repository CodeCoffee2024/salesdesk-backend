# Build context is the repository root.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Restore against the project graph first so layers cache on dependency changes only.
COPY SalesDesk.Backend.sln ./
COPY src/SalesDesk.Domain/SalesDesk.Domain.csproj src/SalesDesk.Domain/
COPY src/SalesDesk.Application/SalesDesk.Application.csproj src/SalesDesk.Application/
COPY src/SalesDesk.Infrastructure/SalesDesk.Infrastructure.csproj src/SalesDesk.Infrastructure/
COPY src/SalesDesk.Api/SalesDesk.Api.csproj src/SalesDesk.Api/
RUN dotnet restore src/SalesDesk.Api/SalesDesk.Api.csproj

COPY src/ src/
RUN dotnet publish src/SalesDesk.Api/SalesDesk.Api.csproj \
      -c Release \
      -o /app \
      --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Container terminates TLS upstream, so serve plain HTTP on 8080.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

USER $APP_UID

ENTRYPOINT ["dotnet", "SalesDesk.Api.dll"]
