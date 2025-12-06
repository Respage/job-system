const { JobManagerClient } = require('./jobManagerClient');
const path = require('path');
const fs = require('fs');

// OpenTelemetry setup
const { NodeSDK } = require('@opentelemetry/sdk-node');
const { getNodeAutoInstrumentations } = require('@opentelemetry/auto-instrumentations-node');
const { OTLPTraceExporter } = require('@opentelemetry/exporter-trace-otlp-http');

async function main() {
    const managerUrl = process.env.MANAGER_URL;
    const apiKey = process.env.API_KEY;
    const jobName = process.env.JOB_NAME;
    const otlpEndpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

    if (!managerUrl || !apiKey || !jobName) {
        console.error('Missing required environment variables: MANAGER_URL, API_KEY, JOB_NAME');
        process.exit(1);
    }

    // Initialize OpenTelemetry if endpoint is configured
    if (otlpEndpoint) {
        const sdk = new NodeSDK({
            traceExporter: new OTLPTraceExporter({
                url: `${otlpEndpoint}/v1/traces`
            }),
            instrumentations: [getNodeAutoInstrumentations()],
            serviceName: `job-worker-${jobName}`
        });
        sdk.start();
        console.log('OpenTelemetry initialized');
    }

    // Initialize job manager client
    const client = new JobManagerClient(managerUrl, apiKey, jobName);

    // Handle graceful shutdown
    let isShuttingDown = false;
    const shutdown = async (signal) => {
        if (isShuttingDown) return;
        isShuttingDown = true;

        console.log(`Received ${signal}, shutting down gracefully...`);
        client.stopHeartbeat();

        // Give time for current work to complete
        await new Promise(resolve => setTimeout(resolve, 5000));
        process.exit(0);
    };

    process.on('SIGTERM', () => shutdown('SIGTERM'));
    process.on('SIGINT', () => shutdown('SIGINT'));

    // Register with manager
    try {
        await client.register();
        client.startHeartbeat();
    } catch (error) {
        console.error('Failed to register with manager, exiting:', error.message);
        process.exit(1);
    }

    // Load and run the job code
    const jobCodePath = '/app/job-code';
    const mainFile = path.join(jobCodePath, 'index.js');

    if (!fs.existsSync(mainFile)) {
        console.error(`Job code not found at ${mainFile}`);
        process.exit(1);
    }

    console.log(`Loading job code from ${mainFile}`);

    try {
        // The job code should export a function or class that handles the work
        const jobModule = require(mainFile);

        // If it exports a run function, call it with the client
        if (typeof jobModule.run === 'function') {
            await jobModule.run(client);
        } else if (typeof jobModule === 'function') {
            await jobModule(client);
        } else {
            console.log('Job module loaded, worker will keep running');
            // Keep the process alive
            await new Promise(() => {});
        }
    } catch (error) {
        console.error('Job execution failed:', error);
        await client.reportStatus('Failed', null, null, error.message);
        process.exit(1);
    }
}

main().catch(error => {
    console.error('Bootstrap failed:', error);
    process.exit(1);
});
