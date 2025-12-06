#!/bin/bash
set -e

echo "Starting Job System Worker (.NET)"
echo "Job Name: $JOB_NAME"
echo "Manager URL: $MANAGER_URL"

# If S3 code path is provided, sync code from S3
if [ -n "$S3_CODE_PATH" ]; then
    echo "Syncing code from S3: $S3_CODE_PATH"
    aws s3 sync "$S3_CODE_PATH" /app/job-code
fi

# Start the worker host
exec dotnet /app/JobSystem.Worker.DotNet.dll
