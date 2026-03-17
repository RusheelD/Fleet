FROM node:20-bookworm-slim AS frontend-build
WORKDIR /src/frontend

ARG VITE_ENTRA_CLIENT_ID
ARG VITE_ENTRA_AUTHORITY
ARG VITE_ENTRA_API_SCOPE
ARG VITE_ENTRA_REDIRECT_URI
ARG VITE_ENTRA_KNOWN_AUTHORITIES
ARG VITE_ENVIRONMENT

ENV VITE_ENTRA_CLIENT_ID=$VITE_ENTRA_CLIENT_ID \
    VITE_ENTRA_AUTHORITY=$VITE_ENTRA_AUTHORITY \
    VITE_ENTRA_API_SCOPE=$VITE_ENTRA_API_SCOPE \
    VITE_ENTRA_REDIRECT_URI=$VITE_ENTRA_REDIRECT_URI \
    VITE_ENTRA_KNOWN_AUTHORITIES=$VITE_ENTRA_KNOWN_AUTHORITIES \
    VITE_ENVIRONMENT=$VITE_ENVIRONMENT

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Fleet.Server/Fleet.Server.csproj Fleet.Server/
RUN dotnet restore Fleet.Server/Fleet.Server.csproj

COPY . .
COPY --from=frontend-build /src/frontend/dist ./Fleet.Server/wwwroot/
RUN dotnet publish Fleet.Server/Fleet.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        ca-certificates \
        openssh-client \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENV GIT_EXECUTABLE_PATH=/usr/bin/git
ENV REPO_SANDBOX_ROOT=/tmp/fleet-sandboxes
ENV ASPNETCORE_URLS=http://+:8080

RUN mkdir -p /tmp/fleet-sandboxes

EXPOSE 8080

ENTRYPOINT ["dotnet", "Fleet.Server.dll"]
