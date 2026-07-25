# Use the official .NET 10 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj file and restore dependencies
COPY ["src/MakaMek.Hub/MakaMek.Hub.csproj", "src/MakaMek.Hub/"]
RUN dotnet restore "src/MakaMek.Hub/MakaMek.Hub.csproj"

# Copy the rest of the source code
COPY . .

# Publish the application
WORKDIR "/src/src/MakaMek.Hub"
RUN dotnet publish "MakaMek.Hub.csproj" -c Release -o /app/publish

# Build the final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for the application
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MakaMek.Hub.dll"]
