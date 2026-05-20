# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files
COPY ["IAM.API/IAM.API.csproj", "IAM.API/"]
COPY ["IAM.Application/IAM.Application.csproj", "IAM.Application/"]
COPY ["IAM.Domain/IAM.Domain.csproj", "IAM.Domain/"]
COPY ["IAM.Infrastructure/IAM.Infrastructure.csproj", "IAM.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "IAM.API/IAM.API.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/IAM.API"
RUN dotnet build "IAM.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "IAM.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Create keys directory for RSA key storage
RUN mkdir -p /app/keys

# Copy published application
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Run application
ENTRYPOINT ["dotnet", "IAM.API.dll"]
