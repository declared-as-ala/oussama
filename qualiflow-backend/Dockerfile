FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DocApi.csproj", "./"]
RUN dotnet restore "DocApi.csproj"

COPY . .
RUN dotnet publish "DocApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000
ENV RabbitMqSettings__Enabled=false

EXPOSE 10000

COPY --from=build /app/publish .
RUN mkdir -p StorageFiles

ENTRYPOINT ["dotnet", "DocApi.dll"]
