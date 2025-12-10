# Production-optimized Dockerfile
# Alpine base image (100MB smaller)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app

# Install ICU for internationalization support
RUN apk add --no-cache icu-libs

# Create non-root user for security
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["src/EasyAuthTokenstoreProxy.csproj", "."]
RUN dotnet restore "./EasyAuthTokenstoreProxy.csproj"
COPY src/ .
WORKDIR "/src/."
RUN dotnet build "EasyAuthTokenstoreProxy.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "EasyAuthTokenstoreProxy.csproj" \
    -c Release \
    -o /app/publish \
    --self-contained false \
    /p:PublishReadyToRun=true

# Final production stage
FROM base AS final
WORKDIR /app

# Copy application and set ownership
COPY --from=publish /app/publish .
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Sidecar container environment settings
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Sidecar uses HTTP internally - main app handles HTTPS
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EasyAuthTokenstoreProxy.dll"]