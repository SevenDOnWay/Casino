# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (layer cache optimization)
COPY TienLen.Server.csproj .
RUN dotnet restore TienLen.Server.csproj

# Copy remaining source files and publish
COPY . .
RUN dotnet publish TienLen.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Expose default HTTP port
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Copy compiled application from build stage
COPY --from=build /app/publish .

# Run under secure non-root app user
USER $APP_UID

ENTRYPOINT ["dotnet", "TienLen.Server.dll"]
