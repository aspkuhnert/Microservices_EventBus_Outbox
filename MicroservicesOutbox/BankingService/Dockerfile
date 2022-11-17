#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 63088

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["BankingService/BankingService.csproj", "BankingService/"]
RUN dotnet restore "BankingService/BankingService.csproj"
COPY . .
WORKDIR "/src/BankingService"
RUN dotnet build "BankingService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BankingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BankingService.dll", "--server.urls", "http://+:80;https://+:443"]