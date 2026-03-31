FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final
WORKDIR /app

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        git \
        ca-certificates \
        openssh-client \
        curl \
        gnupg \
        python3 \
        python3-pip \
        python3-venv \
        openjdk-21-jdk-headless; \
    rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    mkdir -p /etc/apt/keyrings; \
    curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
        | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg; \
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_20.x nodistro main" \
        > /etc/apt/sources.list.d/nodesource.list; \
    apt-get update; \
    apt-get install -y --no-install-recommends nodejs; \
    rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    ln -sf /usr/bin/python3 /usr/local/bin/python; \
    ln -sf /usr/bin/pip3 /usr/local/bin/pip; \
    java_bin="$(readlink -f "$(command -v javac)")"; \
    java_home="$(dirname "$(dirname "$java_bin")")"; \
    mkdir -p /usr/lib/jvm; \
    ln -sfn "$java_home" /usr/lib/jvm/default-java

# Copy the pre-built publish output prepared before docker build.
COPY publish/ ./

ENV GIT_EXECUTABLE_PATH=/usr/bin/git \
    JAVA_HOME=/usr/lib/jvm/default-java \
    REPO_SANDBOX_ROOT=/tmp/fleet-sandboxes \
    DATA_PROTECTION_KEYS_PATH=/home/aspnet/DataProtection-Keys \
    ASPNETCORE_HTTP_PORTS=8080

RUN mkdir -p /tmp/fleet-sandboxes /home/aspnet/DataProtection-Keys

EXPOSE 8080

ENTRYPOINT ["dotnet", "Fleet.Server.dll"]
