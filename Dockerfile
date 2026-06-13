# amd64 image. Builds a musl-native self-contained binary so it runs directly on Alpine
# (no glibc shim). The CI builds this on a native amd64 runner.
ARG BUILD_CONFIGURATION=Release
ARG RID=linux-musl-x64

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION
ARG RID
WORKDIR /src
COPY ["Hariane2Mqtt.csproj", "./"]
RUN dotnet restore "Hariane2Mqtt.csproj" -r $RID
COPY . .
RUN dotnet publish "Hariane2Mqtt.csproj" -c $BUILD_CONFIGURATION -r $RID --self-contained true --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS final
WORKDIR /app

# tzdata is required for TimeZoneInfo (Energy statistics are dated in the HA local timezone).
RUN apk add --no-cache tzdata

COPY --from=build /app/publish .
COPY entrypoint.sh /entrypoint.sh
RUN chmod -R +x /entrypoint.sh /app

CMD ["/entrypoint.sh"]
