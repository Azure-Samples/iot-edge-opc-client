ARG runtime_base_tag=2.1-runtime-alpine
ARG build_base_tag=2.1-sdk-alpine

FROM microsoft/dotnet:${build_base_tag} AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY opcclient/*.csproj ./opcclient/
WORKDIR /app/opcclient
RUN dotnet restore

# copy and publish app
WORKDIR /app
COPY opcclient/. ./opcclient/
WORKDIR /app/opcclient
RUN dotnet publish -c Release -o out

# start it up
FROM microsoft/dotnet:${runtime_base_tag} AS runtime
WORKDIR /app
COPY --from=build /app/opcclient/out ./
WORKDIR /appdata
ENTRYPOINT ["dotnet", "/app/opcclient.dll"]
