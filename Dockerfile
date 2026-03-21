FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        ca-certificates \
        openssh-client \
    && rm -rf /var/lib/apt/lists/*

# Runtime-only image: copy the pre-built publish output prepared before docker build.
COPY publish/ ./

ENV GIT_EXECUTABLE_PATH=/usr/bin/git \
    REPO_SANDBOX_ROOT=/tmp/fleet-sandboxes \
    DATA_PROTECTION_KEYS_PATH=/home/aspnet/DataProtection-Keys \
    ASPNETCORE_HTTP_PORTS=8080

RUN mkdir -p /tmp/fleet-sandboxes /home/aspnet/DataProtection-Keys

EXPOSE 8080

ENTRYPOINT ["dotnet", "Fleet.Server.dll"]
