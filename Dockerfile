# -----------------------------------------------------------------------------
# STAGE 1: Build & Publish Backend Application
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj and restore dependencies first for caching
COPY ["backend/TodoListApp3.csproj", "backend/"]
RUN dotnet restore "backend/TodoListApp3.csproj"

# Copy full backend source
COPY backend/ backend/
WORKDIR "/src/backend"
RUN dotnet publish "TodoListApp3.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# STAGE 2: Production Runtime
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Copy published backend binaries
COPY --from=build /app/publish .

# Copy dedicated frontend directory into /frontend for static file serving
COPY frontend/ /frontend/

# Environment defaults for Render
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000

# Entrypoint
ENTRYPOINT ["dotnet", "TodoListApp3.dll"]
