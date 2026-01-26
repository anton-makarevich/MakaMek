# Use the official .NET 10 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/MakaMek.Tools/BotAgent.Models/BotAgent.Models.csproj", "src/MakaMek.Tools/BotAgent.Models/"]
COPY ["src/MakaMek.Tools/BotContainer/BotContainer.csproj", "src/MakaMek.Tools/BotContainer/"]
COPY ["src/MakaMek.Core/MakaMek.Core.csproj", "src/MakaMek.Core/"]
COPY ["src/MakaMek.Bots/MakaMek.Bots.csproj", "src/MakaMek.Bots/"]
COPY ["src/MakaMek.SourceGenerators/MakaMek.SourceGenerators.csproj", "src/MakaMek.SourceGenerators/"]
RUN dotnet restore "src/MakaMek.Tools/BotContainer/BotContainer.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
WORKDIR "/src/src/MakaMek.Tools/BotContainer"
RUN dotnet build "BotContainer.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "BotContainer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build the final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for the application
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "BotContainer.dll"]
