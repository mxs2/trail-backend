# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["Trail.Api/Trail.Api.csproj", "Trail.Api/"]
RUN dotnet restore "Trail.Api/Trail.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Trail.Api"
RUN dotnet build "Trail.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "Trail.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway dynamic port
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet Trail.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
