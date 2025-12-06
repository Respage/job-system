FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# Install AWS CLI
RUN apt-get update && apt-get install -y \
    curl \
    unzip \
    && curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip" \
    && unzip awscliv2.zip \
    && ./aws/install \
    && rm -rf awscliv2.zip aws \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy SDK project
COPY src/JobSystem.Worker.Sdk/JobSystem.Worker.Sdk.csproj JobSystem.Worker.Sdk/
COPY src/JobSystem.Worker.DotNet/JobSystem.Worker.DotNet.csproj JobSystem.Worker.DotNet/

RUN dotnet restore JobSystem.Worker.DotNet/JobSystem.Worker.DotNet.csproj

# Copy source
COPY src/JobSystem.Worker.Sdk/ JobSystem.Worker.Sdk/
COPY src/JobSystem.Worker.DotNet/ JobSystem.Worker.DotNet/

# Build
WORKDIR /src/JobSystem.Worker.DotNet
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directory for job code
RUN mkdir -p /app/job-code

# Copy entrypoint script
COPY src/JobSystem.Worker.DotNet/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
