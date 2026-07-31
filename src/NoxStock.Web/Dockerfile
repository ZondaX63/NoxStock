# ==========================================
# 1. AŞAMA: ALTYAPI KURULUMU VE DERLEME (Build & Publish)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Projenin Docker içinde kütüphaneleri çekebilmesi için resmi Debian Node 24 (LTS) kuruyoruz
# (Böylece select2 kütüphanesinin Node >=24 kısıtlaması sıfır riskle aşılır)
RUN apt-get update && apt-get install -y curl \
    && curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y nodejs

# Katmanlı mimarideki tüm .csproj tanımlarını kopyalıyoruz
COPY ["src/NoxStock.Domain.Shared/NoxStock.Domain.Shared.csproj", "src/NoxStock.Domain.Shared/"]
COPY ["src/NoxStock.Domain/NoxStock.Domain.csproj", "src/NoxStock.Domain/"]
COPY ["src/NoxStock.Application.Contracts/NoxStock.Application.Contracts.csproj", "src/NoxStock.Application.Contracts/"]
COPY ["src/NoxStock.Application/NoxStock.Application.csproj", "src/NoxStock.Application/"]
COPY ["src/NoxStock.EntityFrameworkCore/NoxStock.EntityFrameworkCore.csproj", "src/NoxStock.EntityFrameworkCore/"]
COPY ["src/NoxStock.HttpApi/NoxStock.HttpApi.csproj", "src/NoxStock.HttpApi/"]
COPY ["src/NoxStock.Web/NoxStock.Web.csproj", "src/NoxStock.Web/"]

# NuGet paketlerini restore ediyoruz
RUN dotnet restore "src/NoxStock.Web/NoxStock.Web.csproj"

# Tüm kaynak kodları içeri alıyoruz
COPY . .
WORKDIR "/src/src/NoxStock.Web"

# ADAMAKILLI OTOMASYON: 
# ABP CLI'ı .NET aracı olarak yerel bir klasöre kuruyoruz. (NPM/Yarn global izin hataları tamamen bypass edilir)
RUN dotnet tool install Volo.Abp.Cli --tool-path /app/tools
ENV PATH="${PATH}:/app/tools"

# Ön yüz kütüphanelerini çakışmaları ve lock kilitlenmelerini engelleyerek sıfırdan indiriyoruz
RUN npm install -g yarn \
    && yarn install --ignore-engines --frozen-lockfile --ignore-scripts \
    && abp install-libs

# Projeyi derleme aşamasına alıyoruz
RUN dotnet build "NoxStock.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NoxStock.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# 2. AŞAMA: NİHAİ ÇALIŞMA ORTAMI (Runtime)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 80
EXPOSE 443
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NoxStock.Web.dll"]
