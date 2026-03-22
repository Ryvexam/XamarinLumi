FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LumiContact.Backend/LumiContact.Backend.csproj LumiContact.Backend/
RUN dotnet restore LumiContact.Backend/LumiContact.Backend.csproj

COPY LumiContact.Backend/. LumiContact.Backend/
WORKDIR /src/LumiContact.Backend
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
VOLUME /data

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LumiContact.Backend.dll"]
