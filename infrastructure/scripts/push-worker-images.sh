#!/bin/bash
set -e

# Push Worker Images to ECR
# Usage: ./push-worker-images.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Get ECR repository URLs from Terraform
cd "$PROJECT_ROOT/infrastructure/terraform"
NODE_REPO=$(terraform output -raw ecr_repository_node_url)
DOTNET_REPO=$(terraform output -raw ecr_repository_dotnet_url)
AWS_REGION=$(terraform output -raw aws_region)

echo "ECR Repositories:"
echo "  Node.js: $NODE_REPO"
echo "  .NET: $DOTNET_REPO"
echo ""

# Login to ECR
echo "Logging in to ECR..."
aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $(echo $NODE_REPO | cut -d'/' -f1)

# Build and push Node.js worker
echo "Building Node.js worker image..."
cd "$PROJECT_ROOT/src/JobSystem.Worker.Node"
docker build -t job-system-worker-node:latest .
docker tag job-system-worker-node:latest $NODE_REPO:latest
docker push $NODE_REPO:latest
echo "Node.js worker image pushed successfully"

# Build and push .NET worker
echo "Building .NET worker image..."
cd "$PROJECT_ROOT"
docker build -f src/JobSystem.Worker.DotNet/Dockerfile -t job-system-worker-dotnet:latest .
docker tag job-system-worker-dotnet:latest $DOTNET_REPO:latest
docker push $DOTNET_REPO:latest
echo ".NET worker image pushed successfully"

echo ""
echo "All worker images have been pushed to ECR"
