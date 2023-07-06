#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY AudioSocket.Net AudioSocket.Net
COPY AudioSocket.Net.sln AudioSocket.Net.sln
RUN dotnet restore "AudioSocket.Net/AudioSocket.Net.csproj"
WORKDIR "/src/AudioSocket.Net"
RUN dotnet build "AudioSocket.Net.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AudioSocket.Net.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AudioSocket.Net.dll"]
