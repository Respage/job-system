const axios = require('axios');

class JobManagerClient {
    constructor(managerUrl, apiKey, jobName) {
        this.managerUrl = managerUrl.replace(/\/$/, '');
        this.apiKey = apiKey;
        this.jobName = jobName;
        this.workerId = null;
        this.config = null;

        this.client = axios.create({
            baseURL: this.managerUrl,
            headers: {
                'Content-Type': 'application/json',
                'X-Api-Key': this.apiKey
            },
            timeout: 30000
        });
    }

    async register(instanceId) {
        try {
            const response = await this.client.post('/api/workers/register', {
                jobName: this.jobName,
                instanceId: instanceId || this._getInstanceId()
            });

            this.workerId = response.data.workerId;
            this.config = response.data.config;

            console.log(`Registered with manager. Worker ID: ${this.workerId}`);
            return response.data;
        } catch (error) {
            console.error('Failed to register with manager:', error.message);
            throw error;
        }
    }

    async reportStatus(status, itemId = null, progressPercent = null, message = null) {
        if (!this.workerId) {
            throw new Error('Worker not registered');
        }

        try {
            await this.client.post(`/api/workers/${this.workerId}/status`, {
                status,
                itemId,
                progressPercent,
                message
            });
        } catch (error) {
            console.error('Failed to report status:', error.message);
        }
    }

    async sendHeartbeat() {
        if (!this.workerId) {
            return;
        }

        try {
            await this.client.post(`/api/workers/${this.workerId}/heartbeat`);
        } catch (error) {
            console.error('Failed to send heartbeat:', error.message);
        }
    }

    async reportComplete(itemId, success, message = null) {
        if (!this.workerId) {
            throw new Error('Worker not registered');
        }

        try {
            await this.client.post(`/api/workers/${this.workerId}/complete`, {
                itemId,
                success,
                message
            });
        } catch (error) {
            console.error('Failed to report completion:', error.message);
        }
    }

    startHeartbeat(intervalMs = 30000) {
        this.heartbeatInterval = setInterval(() => {
            this.sendHeartbeat();
        }, intervalMs);
    }

    stopHeartbeat() {
        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
            this.heartbeatInterval = null;
        }
    }

    _getInstanceId() {
        // Try to get EC2 instance ID from metadata
        return process.env.EC2_INSTANCE_ID ||
               process.env.HOSTNAME ||
               `worker-${Date.now()}`;
    }
}

module.exports = { JobManagerClient };
