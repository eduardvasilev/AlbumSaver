
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000
ENV DENO_INSTALL=/usr/local

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        python3 \
        python3-pip \
        ffmpeg \
        curl \
        ca-certificates \
        unzip && \
    curl -fsSL https://deno.land/install.sh | sh && \
    pip3 install --break-system-packages --no-cache-dir -U "yt-dlp[default]" && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["YTMusicDownloader.WebApi/YTMusicDownloader.WebApi.csproj", "YTMusicDownloader.WebApi/"]
RUN dotnet restore "YTMusicDownloader.WebApi/YTMusicDownloader.WebApi.csproj"

COPY . .
WORKDIR "/src/YTMusicDownloader.WebApi"
RUN dotnet build "YTMusicDownloader.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "YTMusicDownloader.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "YTMusicDownloader.WebApi.dll"]
