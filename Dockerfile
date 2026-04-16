FROM node:20-bookworm-slim AS node-runtime

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final
WORKDIR /app

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

ENV DEBIAN_FRONTEND=noninteractive

RUN set -eux; \
    for sources in /etc/apt/sources.list /etc/apt/sources.list.d/*.list /etc/apt/sources.list.d/*.sources; do \
        [ -f "$sources" ] || continue; \
        sed -i \
            -e 's|http://archive.ubuntu.com/ubuntu|https://archive.ubuntu.com/ubuntu|g' \
            -e 's|http://security.ubuntu.com/ubuntu|https://security.ubuntu.com/ubuntu|g' \
            "$sources"; \
    done; \
    printf '%s\n' \
        'Acquire::Retries "10";' \
        'Acquire::By-Hash "force";' \
        'Acquire::http::No-Cache "true";' \
        'Acquire::https::No-Cache "true";' \
        'Acquire::http::No-Store "true";' \
        'Acquire::https::No-Store "true";' \
        'Acquire::http::Pipeline-Depth "0";' \
        'Acquire::https::Pipeline-Depth "0";' \
        'Acquire::http::Timeout "30";' \
        'Acquire::https::Timeout "30";' \
        > /etc/apt/apt.conf.d/99fleet-network-resilience; \
    for attempt in 1 2 3 4 5; do \
        rm -rf /var/lib/apt/lists/*; \
        if apt-get update; then \
            break; \
        fi; \
        if [ "$attempt" -eq 5 ]; then \
            exit 1; \
        fi; \
        sleep "$((attempt * 15))"; \
    done; \
    apt-get install -y --no-install-recommends \
        git \
        ca-certificates \
        openssh-client \
        python3 \
        python3-pip \
        python3-venv \
        openjdk-21-jdk-headless; \
    rm -rf /var/lib/apt/lists/*

COPY --from=node-runtime /usr/local/bin/node /usr/local/bin/node
COPY --from=node-runtime /usr/local/bin/npm /usr/local/bin/npm
COPY --from=node-runtime /usr/local/bin/npx /usr/local/bin/npx
COPY --from=node-runtime /usr/local/lib/node_modules /usr/local/lib/node_modules

RUN set -eux; \
    ln -sf /usr/bin/python3 /usr/local/bin/python; \
    ln -sf /usr/bin/pip3 /usr/local/bin/pip; \
    ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm; \
    ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx; \
    java_bin="$(readlink -f "$(command -v javac)")"; \
    java_home="$(dirname "$(dirname "$java_bin")")"; \
    mkdir -p /usr/lib/jvm; \
    ln -sfn "$java_home" /usr/lib/jvm/default-java

ENV FLEET_SHARED_NODE_TOOL_ROOT=/opt/fleet-node-tools \
    FLEET_SHARED_NODE_MODULES_PATH=/opt/fleet-node-tools/node_modules \
    FLEET_SHARED_NODE_BIN_PATH=/opt/fleet-node-tools/node_modules/.bin \
    FLEET_SHARED_PYTHON_SITE_PACKAGES=/opt/fleet-python-tools \
    NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1

RUN set -eux; \
    mkdir -p "$FLEET_SHARED_NODE_TOOL_ROOT" "$FLEET_SHARED_PYTHON_SITE_PACKAGES"; \
    printf '%s\n' \
        '{' \
        '  "name": "fleet-shared-node-tools",' \
        '  "private": true,' \
        '  "dependencies": {' \
        '    "@testing-library/dom": "10.4.1",' \
        '    "@testing-library/jest-dom": "6.9.1",' \
        '    "@testing-library/react": "16.3.2",' \
        '    "@vitest/coverage-v8": "4.1.4",' \
        '    "happy-dom": "20.9.0",' \
        '    "jsdom": "29.0.2",' \
        '    "react": "19.2.1",' \
        '    "react-dom": "19.2.1",' \
        '    "vitest": "4.1.4"' \
        '  }' \
        '}' \
        > "$FLEET_SHARED_NODE_TOOL_ROOT/package.json"; \
    cd "$FLEET_SHARED_NODE_TOOL_ROOT"; \
    npm install --omit=dev; \
    python3 -m pip install --target "$FLEET_SHARED_PYTHON_SITE_PACKAGES" \
        pytest \
        pytest-asyncio \
        pytest-cov \
        pytest-mock \
        pytest-timeout \
        pytest-xdist

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
