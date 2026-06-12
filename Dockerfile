FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TelegramAdminPanel.csproj", "./"]
RUN dotnet restore "TelegramAdminPanel.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "TelegramAdminPanel.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TelegramAdminPanel.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TelegramAdminPanel.dll"]
