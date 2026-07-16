FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/NoxStock.Domain.Shared/NoxStock.Domain.Shared.csproj", "src/NoxStock.Domain.Shared/"]
COPY ["src/NoxStock.Domain/NoxStock.Domain.csproj", "src/NoxStock.Domain/"]
COPY ["src/NoxStock.Application.Contracts/NoxStock.Application.Contracts.csproj", "src/NoxStock.Application.Contracts/"]
COPY ["src/NoxStock.Application/NoxStock.Application.csproj", "src/NoxStock.Application/"]
COPY ["src/NoxStock.EntityFrameworkCore/NoxStock.EntityFrameworkCore.csproj", "src/NoxStock.EntityFrameworkCore/"]
COPY ["src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj", "src/NoxStock.DbMigrator/"]

RUN dotnet restore "src/NoxStock.DbMigrator/NoxStock.DbMigrator.csproj"

# Copy the remaining source code
COPY . .

# Build the DbMigrator project
WORKDIR "/src/src/NoxStock.DbMigrator"
RUN dotnet build "NoxStock.DbMigrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NoxStock.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NoxStock.DbMigrator.dll"]
