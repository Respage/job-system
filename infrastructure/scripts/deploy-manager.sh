#!/bin/bash
set -e

# Job System Manager Deployment Script
# Usage: ./deploy-manager.sh [environment]

ENVIRONMENT=${1:-dev}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Deploying Job System Manager for environment: $ENVIRONMENT"

# Build the .NET application
echo "Building .NET application..."
cd "$PROJECT_ROOT"
dotnet publish src/JobSystem.Manager/JobSystem.Manager.csproj -c Release -o ./publish

# Get Terraform outputs
echo "Getting infrastructure configuration..."
cd "$PROJECT_ROOT/infrastructure/terraform"
TERRAFORM_OUTPUT=$(terraform output -json manager_config)

echo "Manager configuration:"
echo "$TERRAFORM_OUTPUT" | jq .

echo ""
echo "Deployment package created at: $PROJECT_ROOT/publish"
echo ""
echo "Next steps:"
echo "1. Copy the publish folder to your manager server"
echo "2. Update appsettings.json with the AWS configuration above"
echo "3. Set up the database connection string"
echo "4. Run: dotnet JobSystem.Manager.dll"
