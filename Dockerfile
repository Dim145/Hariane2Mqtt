FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Hariane2Mqtt.csproj", "./"]
RUN dotnet restore "Hariane2Mqtt.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Hariane2Mqtt.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Hariane2Mqtt.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:SelfContained=true

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS final
WORKDIR /app

RUN apk add libc6-compat

COPY --from=publish /app/publish .

COPY entrypoint.sh /entrypoint.sh

RUN chmod -R +x /entrypoint.sh /app

CMD ["/entrypoint.sh"]
