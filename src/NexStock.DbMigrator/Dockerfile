FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/NexStock.Domain.Shared/NexStock.Domain.Shared.csproj", "src/NexStock.Domain.Shared/"]
COPY ["src/NexStock.Domain/NexStock.Domain.csproj", "src/NexStock.Domain/"]
COPY ["src/NexStock.Application.Contracts/NexStock.Application.Contracts.csproj", "src/NexStock.Application.Contracts/"]
COPY ["src/NexStock.Application/NexStock.Application.csproj", "src/NexStock.Application/"]
COPY ["src/NexStock.EntityFrameworkCore/NexStock.EntityFrameworkCore.csproj", "src/NexStock.EntityFrameworkCore/"]
COPY ["src/NexStock.DbMigrator/NexStock.DbMigrator.csproj", "src/NexStock.DbMigrator/"]

RUN dotnet restore "src/NexStock.DbMigrator/NexStock.DbMigrator.csproj"

# Copy the remaining source code
COPY . .

# Build the DbMigrator project
WORKDIR "/src/src/NexStock.DbMigrator"
RUN dotnet build "NexStock.DbMigrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NexStock.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NexStock.DbMigrator.dll"]
