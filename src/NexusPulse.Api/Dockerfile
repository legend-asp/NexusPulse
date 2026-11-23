# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies for layer caching
COPY ["src/NexusPulse.Domain/NexusPulse.Domain.csproj", "src/NexusPulse.Domain/"]
COPY ["src/NexusPulse.Application/NexusPulse.Application.csproj", "src/NexusPulse.Application/"]
COPY ["src/NexusPulse.Infrastructure/NexusPulse.Infrastructure.csproj", "src/NexusPulse.Infrastructure/"]
COPY ["src/NexusPulse.Api/NexusPulse.Api.csproj", "src/NexusPulse.Api/"]

RUN dotnet restore "src/NexusPulse.Api/NexusPulse.Api.csproj"

# Copy full source and publish
COPY src/ src/
WORKDIR "/src/src/NexusPulse.Api"
RUN dotnet publish "NexusPulse.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5199
EXPOSE 5199
ENTRYPOINT ["dotnet", "NexusPulse.Api.dll"]
