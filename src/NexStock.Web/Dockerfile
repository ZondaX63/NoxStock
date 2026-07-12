FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# === ABP install-libs İçin Gerekli Bağımlılıkların Kurulumu ===
RUN apt-get update && apt-get install -y curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g yarn \
    && dotnet tool install -g Volo.Abp.Cli

# ABP CLI'ın terminal tarafından tanınması için Environment Path tanımlıyoruz
ENV PATH="${PATH}:/root/.dotnet/tools"
# =============================================================

# Copy csproj files and restore
COPY ["src/NexStock.Domain.Shared/NexStock.Domain.Shared.csproj", "src/NexStock.Domain.Shared/"]
COPY ["src/NexStock.Domain/NexStock.Domain.csproj", "src/NexStock.Domain/"]
COPY ["src/NexStock.Application.Contracts/NexStock.Application.Contracts.csproj", "src/NexStock.Application.Contracts/"]
COPY ["src/NexStock.Application/NexStock.Application.csproj", "src/NexStock.Application/"]
COPY ["src/NexStock.EntityFrameworkCore/NexStock.EntityFrameworkCore.csproj", "src/NexStock.EntityFrameworkCore/"]
COPY ["src/NexStock.HttpApi/NexStock.HttpApi.csproj", "src/NexStock.HttpApi/"]
COPY ["src/NexStock.Web/NexStock.Web.csproj", "src/NexStock.Web/"]

# Ön yüzde yarn/npm bağımlılıklarının restore sırasında eşleşmesi için json dosyalarını da çekiyoruz
COPY ["src/NexStock.Web/package.json", "src/NexStock.Web/"]
COPY ["src/NexStock.Web/yarn.lock", "src/NexStock.Web/"]

RUN dotnet restore "src/NexStock.Web/NexStock.Web.csproj"

# Copy the remaining source code
COPY . .

# === ABP Statik Kütüphanelerini İndirme Aşaması ===
WORKDIR "/src/src/NexStock.Web"
RUN abp install-libs
# ==================================================

# Build the Web project
RUN dotnet build "NexStock.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NexStock.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NexStock.Web.dll"]