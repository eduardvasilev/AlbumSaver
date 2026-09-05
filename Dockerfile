#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000

RUN apt-get update && \
    apt-get install -y python3 python3-pip ffmpeg && \
    pip3 install --break-system-packages -U --pre yt-dlp

EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["YTMusicDownloader.WebApi/YTMusicDownloader.WebApi.csproj", "YTMusicDownloader.WebApi/"]
RUN dotnet restore "YTMusicDownloader.WebApi/YTMusicDownloader.WebApi.csproj"
COPY . . 
WORKDIR "/src/YTMusicDownloader.WebApi"
RUN dotnet build "YTMusicDownloader.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "YTMusicDownloader.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Здесь мы root и можем безопасно делать chmod
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .


ENTRYPOINT ["dotnet", "YTMusicDownloader.WebApi.dll"]
