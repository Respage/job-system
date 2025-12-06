FROM node:20-alpine

# Install AWS CLI
RUN apk add --no-cache \
    python3 \
    py3-pip \
    curl \
    bash \
    && pip3 install --break-system-packages awscli

# Create app directory
WORKDIR /app

# Copy bootstrap files
COPY src/bootstrap.js /app/bootstrap.js
COPY src/jobManagerClient.js /app/jobManagerClient.js
COPY package.json /app/package.json

# Install bootstrap dependencies
RUN npm install --production

# Create directory for job code
RUN mkdir -p /app/job-code

# Set environment variables
ENV NODE_ENV=production
ENV MANAGER_URL=""
ENV API_KEY=""
ENV JOB_NAME=""

# Copy entrypoint script
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
