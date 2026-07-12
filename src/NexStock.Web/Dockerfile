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
COPY ["src/NexStock.Domain.Shared/NexStock.Domain.Shared.csproj", "src/NexStock.Domain.Shared/"]
COPY ["src/NexStock.Domain/NexStock.Domain.csproj", "src/NexStock.Domain/"]
COPY ["src/NexStock.Application.Contracts/NexStock.Application.Contracts.csproj", "src/NexStock.Application.Contracts/"]
COPY ["src/NexStock.Application/NexStock.Application.csproj", "src/NexStock.Application/"]
COPY ["src/NexStock.EntityFrameworkCore/NexStock.EntityFrameworkCore.csproj", "src/NexStock.EntityFrameworkCore/"]
COPY ["src/NexStock.HttpApi/NexStock.HttpApi.csproj", "src/NexStock.HttpApi/"]
COPY ["src/NexStock.Web/NexStock.Web.csproj", "src/NexStock.Web/"]

# NuGet paketlerini restore ediyoruz
RUN dotnet restore "src/NexStock.Web/NexStock.Web.csproj"

# Tüm kaynak kodları içeri alıyoruz
COPY . .
WORKDIR "/src/src/NexStock.Web"

# ADAMAKILLI OTOMASYON: 
# ABP CLI'ı .NET aracı olarak yerel bir klasöre kuruyoruz. (NPM/Yarn global izin hataları tamamen bypass edilir)
RUN dotnet tool install Volo.Abp.Cli --tool-path /app/tools
ENV PATH="${PATH}:/app/tools"

# Ön yüz kütüphanelerini çakışmaları ve lock kilitlenmelerini engelleyerek sıfırdan indiriyoruz
RUN npm install -g yarn \
    && yarn install --ignore-engines \
    && abp install-libs

# Projeyi derleme aşamasına alıyoruz
RUN dotnet build "NexStock.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NexStock.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# 2. AŞAMA: NİHAİ ÇALIŞMA ORTAMI (Runtime)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NexStock.Web.dll"]