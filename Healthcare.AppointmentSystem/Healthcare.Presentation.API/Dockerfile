FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Central TFM + package versions must be present before restore (projects omit them).
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Healthcare.Presentation.API/Healthcare.Presentation.API.csproj", "Healthcare.Presentation.API/"]
COPY ["Healthcare.Adapters/Healthcare.Adapters.csproj", "Healthcare.Adapters/"]
COPY ["Healthcare.Application/Healthcare.Application.csproj", "Healthcare.Application/"]
COPY ["Healthcare.Domain/Healthcare.Domain.csproj", "Healthcare.Domain/"]
RUN dotnet restore "Healthcare.Presentation.API/Healthcare.Presentation.API.csproj"
COPY . .
RUN dotnet publish "Healthcare.Presentation.API/Healthcare.Presentation.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# curl used by compose healthchecks and ops probes
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker
COPY --from=build /app/publish .
# Optional Docker secrets → env (see docker-entrypoint.sh)
COPY Healthcare.Presentation.API/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh
ENTRYPOINT ["/app/docker-entrypoint.sh"]
