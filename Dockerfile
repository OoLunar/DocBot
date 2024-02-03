FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG VERSION=0.1.0
WORKDIR /src

COPY ./ /src
RUN dotnet publish -c Release -r linux-musl-x64 --self-contained -p:Version=$VERSION -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -p:PublishSingleFile=true \
    && apk upgrade --update-cache --available && apk add openssl icu-libs && rm -rf /var/cache/apk/*

ENTRYPOINT /src/src/bin/Release/net8.0/linux-musl-x64/publish/DocBot