FROM microsoft/dotnet:2.1-sdk-alpine AS build

ARG BUILDCONFIG=RELEASE
ARG VERSION=1.0.0

# copy csproj and restore as distinct layers
COPY ./Ditto.csproj ./Ditto/
RUN dotnet restore Ditto/Ditto.csproj

# copy everything else and build
COPY ./ ./Ditto/
WORKDIR /Ditto/
RUN dotnet publish -c $BUILDCONFIG -o out /p:Version=$VERSION

# build runtime image
FROM microsoft/dotnet:2.1-runtime-alpine
WORKDIR /app
COPY --from=build /Ditto/out ./

ENTRYPOINT ["dotnet", "Ditto.dll"]