FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DocApi.csproj", "./"]
RUN dotnet restore "DocApi.csproj"

COPY . .
RUN dotnet publish "DocApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health check
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Run as non-root for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

COPY --from=build /app/publish .
RUN mkdir -p StorageFiles && chown -R appuser:appgroup /app

USER appuser

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:10000/health || exit 1

ENTRYPOINT ["dotnet", "DocApi.dll"]
