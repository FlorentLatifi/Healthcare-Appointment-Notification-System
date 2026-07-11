FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
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
ENTRYPOINT ["dotnet", "Healthcare.Presentation.API.dll"]
