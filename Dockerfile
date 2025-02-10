FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG VERSION=1.0.0
WORKDIR /src

COPY . .
RUN apk add git \
    && git submodule update --init --recursive \
    && sed -i "s/<Version>.*<\/Version>/<Version>${VERSION}<\/Version>/" src/DocBot.csproj \
    && dotnet publish -c Release -r linux-musl-x64

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine
WORKDIR /src

COPY --from=build /src/src/bin/Release/net8.0/linux-musl-x64/publish /src
RUN apk upgrade --update-cache --available \
    && apk add openssl icu-libs git \
    && rm -rf /var/cache/apk/*

ENTRYPOINT /src/DocBot