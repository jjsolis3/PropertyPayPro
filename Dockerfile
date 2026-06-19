# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
WORKDIR /src

COPY PropertyPayPro.sln ./
COPY src/PropertyPayPro/PropertyPayPro.csproj src/PropertyPayPro/
RUN dotnet restore src/PropertyPayPro/PropertyPayPro.csproj

COPY src/PropertyPayPro/ src/PropertyPayPro/
RUN dotnet publish src/PropertyPayPro/PropertyPayPro.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "PropertyPayPro.dll"]
