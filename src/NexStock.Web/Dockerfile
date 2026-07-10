FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/NexStock.Domain.Shared/NexStock.Domain.Shared.csproj", "src/NexStock.Domain.Shared/"]
COPY ["src/NexStock.Domain/NexStock.Domain.csproj", "src/NexStock.Domain/"]
COPY ["src/NexStock.Application.Contracts/NexStock.Application.Contracts.csproj", "src/NexStock.Application.Contracts/"]
COPY ["src/NexStock.Application/NexStock.Application.csproj", "src/NexStock.Application/"]
COPY ["src/NexStock.EntityFrameworkCore/NexStock.EntityFrameworkCore.csproj", "src/NexStock.EntityFrameworkCore/"]
COPY ["src/NexStock.HttpApi/NexStock.HttpApi.csproj", "src/NexStock.HttpApi/"]
COPY ["src/NexStock.Web/NexStock.Web.csproj", "src/NexStock.Web/"]

RUN dotnet restore "src/NexStock.Web/NexStock.Web.csproj"

# Copy the remaining source code
COPY . .

# Build the Web project
WORKDIR "/src/src/NexStock.Web"
RUN dotnet build "NexStock.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NexStock.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NexStock.Web.dll"]