FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY Einvoicing.sln ./
COPY Einvoicing.Domain/Einvoicing.Domain.csproj Einvoicing.Domain/
COPY Einvoicing.Application/Einvoicing.Application.csproj Einvoicing.Application/
COPY Einvoicing.Infrastructure/Einvoicing.Infrastructure.csproj Einvoicing.Infrastructure/
COPY Einvoicing.Api/Einvoicing.Api.csproj Einvoicing.Api/
COPY Einvoicing.Tests/Einvoicing.Tests.csproj Einvoicing.Tests/

RUN dotnet restore Einvoicing.Api/Einvoicing.Api.csproj

COPY . .
RUN dotnet publish Einvoicing.Api/Einvoicing.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Einvoicing.Api.dll"]