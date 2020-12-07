FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS base

ARG BUILDCONFIG=RELEASE
ARG VERSION=1.0.0

# copy csproj and restore as distinct layers
COPY ./src/Ditto.Sqs.Consumer/Ditto.Sqs.Consumer.csproj ./Ditto.Sqs.Consumer/
COPY ./src/Ditto.Core/Ditto.Core.csproj ./Ditto.Core/
RUN dotnet restore Ditto.Sqs.Consumer/Ditto.Sqs.Consumer.csproj

# copy everything else and build
COPY ./src/ ./
RUN dotnet publish Ditto.Sqs.Consumer/Ditto.Sqs.Consumer.csproj --runtime linux-musl-x64 -c $BUILDCONFIG -o out /p:Version=$VERSION -p:PublishTrimmed=true

# build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1-alpine
WORKDIR /app
COPY --from=base /out ./

EXPOSE 5000
ENTRYPOINT ["./Ditto.Sqs.Consumer"]
