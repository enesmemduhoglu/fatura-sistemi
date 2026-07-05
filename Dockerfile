# Derleme aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Isbasi.Web/Isbasi.Web.csproj Isbasi.Web/
RUN dotnet restore Isbasi.Web
COPY Isbasi.Web/ Isbasi.Web/
RUN dotnet publish Isbasi.Web -c Release -o /app/publish

# Çalışma aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Isbasi.Web.dll"]
