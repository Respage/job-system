using Amazon.EC2;
using Amazon.EC2.Model;
using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.Workers;

public class Ec2WorkerManager : IWorkerManager
{
    private readonly IAmazonEC2 _ec2Client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Ec2WorkerManager> _logger;

    public Ec2WorkerManager(
        IAmazonEC2 ec2Client,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<Ec2WorkerManager> logger)
    {
        _ec2Client = ec2Client;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WorkerInstance> LaunchWorkerAsync(JobDefinition job, bool useSpot, CancellationToken cancellationToken = default)
    {
        var instanceType = GetInstanceType(job.InstanceSize);
        var amiId = GetWorkerAmiId(job.Language);
        var userData = GenerateUserData(job);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        string ec2InstanceId;

        if (useSpot)
        {
            ec2InstanceId = await LaunchSpotInstanceAsync(job, instanceType, amiId, userData, cancellationToken);
        }
        else
        {
            ec2InstanceId = await LaunchOnDemandInstanceAsync(job, instanceType, amiId, userData, cancellationToken);
        }

        var worker = new WorkerInstance
        {
            Id = Guid.NewGuid(),
            JobDefinitionId = job.Id,
            Ec2InstanceId = ec2InstanceId,
            InstanceType = instanceType,
            IsSpotInstance = useSpot,
            Status = WorkerStatus.Pending,
            LaunchedAt = DateTime.UtcNow
        };

        dbContext.WorkerInstances.Add(worker);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Launched {InstanceType} worker {WorkerId} (EC2: {Ec2InstanceId}, Spot: {IsSpot}) for job {JobName}",
            instanceType, worker.Id, ec2InstanceId, useSpot, job.Name);

        return worker;
    }

    private async Task<string> LaunchOnDemandInstanceAsync(
        JobDefinition job,
        string instanceType,
        string amiId,
        string userData,
        CancellationToken cancellationToken)
    {
        var request = new RunInstancesRequest
        {
            ImageId = amiId,
            InstanceType = instanceType,
            MinCount = 1,
            MaxCount = 1,
            UserData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userData)),
            IamInstanceProfile = new IamInstanceProfileSpecification
            {
                Name = _configuration["AWS:WorkerInstanceProfile"]
            },
            SubnetId = GetSubnetId(),
            SecurityGroupIds = new List<string> { _configuration["AWS:SecurityGroupId"] ?? "" },
            TagSpecifications = new List<TagSpecification>
            {
                new TagSpecification
                {
                    ResourceType = ResourceType.Instance,
                    Tags = new List<Tag>
                    {
                        new Tag("Name", $"job-worker-{job.Name}"),
                        new Tag("JobSystem", "true"),
                        new Tag("JobName", job.Name),
                        new Tag("JobId", job.Id.ToString())
                    }
                }
            }
        };

        var response = await _ec2Client.RunInstancesAsync(request, cancellationToken);
        return response.Reservation.Instances[0].InstanceId;
    }

    private async Task<string> LaunchSpotInstanceAsync(
        JobDefinition job,
        string instanceType,
        string amiId,
        string userData,
        CancellationToken cancellationToken)
    {
        var request = new RunInstancesRequest
        {
            ImageId = amiId,
            InstanceType = instanceType,
            MinCount = 1,
            MaxCount = 1,
            UserData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userData)),
            IamInstanceProfile = new IamInstanceProfileSpecification
            {
                Name = _configuration["AWS:WorkerInstanceProfile"]
            },
            SubnetId = GetSubnetId(),
            SecurityGroupIds = new List<string> { _configuration["AWS:SecurityGroupId"] ?? "" },
            InstanceMarketOptions = new InstanceMarketOptionsRequest
            {
                MarketType = MarketType.Spot,
                SpotOptions = new SpotMarketOptions
                {
                    SpotInstanceType = SpotInstanceType.OneTime,
                    InstanceInterruptionBehavior = InstanceInterruptionBehavior.Terminate
                }
            },
            TagSpecifications = new List<TagSpecification>
            {
                new TagSpecification
                {
                    ResourceType = ResourceType.Instance,
                    Tags = new List<Tag>
                    {
                        new Tag("Name", $"job-worker-{job.Name}-spot"),
                        new Tag("JobSystem", "true"),
                        new Tag("JobName", job.Name),
                        new Tag("JobId", job.Id.ToString()),
                        new Tag("SpotInstance", "true")
                    }
                }
            }
        };

        var response = await _ec2Client.RunInstancesAsync(request, cancellationToken);
        return response.Reservation.Instances[0].InstanceId;
    }

    public async Task TerminateWorkerAsync(WorkerInstance worker, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new TerminateInstancesRequest
            {
                InstanceIds = new List<string> { worker.Ec2InstanceId }
            };

            await _ec2Client.TerminateInstancesAsync(request, cancellationToken);

            _logger.LogInformation(
                "Terminated EC2 instance {Ec2InstanceId} for worker {WorkerId}",
                worker.Ec2InstanceId, worker.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate EC2 instance {Ec2InstanceId}", worker.Ec2InstanceId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkerInstance>> GetActiveWorkersAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        return await dbContext.WorkerInstances
            .Where(w => w.JobDefinitionId == jobDefinitionId
                && (w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateWorkerStatusFromEc2Async(WorkerInstance worker, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DescribeInstancesRequest
            {
                InstanceIds = new List<string> { worker.Ec2InstanceId }
            };

            var response = await _ec2Client.DescribeInstancesAsync(request, cancellationToken);

            if (response.Reservations.Count > 0 && response.Reservations[0].Instances.Count > 0)
            {
                var instance = response.Reservations[0].Instances[0];
                worker.PrivateIpAddress = instance.PrivateIpAddress;

                // Map EC2 state to worker status
                worker.Status = instance.State.Name.Value switch
                {
                    "pending" => WorkerStatus.Pending,
                    "running" => WorkerStatus.Running,
                    "shutting-down" => WorkerStatus.Terminating,
                    "terminated" => WorkerStatus.Terminated,
                    "stopping" => WorkerStatus.Terminating,
                    "stopped" => WorkerStatus.Terminated,
                    _ => worker.Status
                };

                if (worker.Status == WorkerStatus.Terminated)
                {
                    worker.TerminatedAt = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get EC2 status for instance {Ec2InstanceId}", worker.Ec2InstanceId);
        }
    }

    private string GetInstanceType(InstanceSize size)
    {
        return size switch
        {
            InstanceSize.Small => _configuration["InstanceTypes:Small"] ?? "t3.small",
            InstanceSize.Medium => _configuration["InstanceTypes:Medium"] ?? "t3.medium",
            InstanceSize.Large => _configuration["InstanceTypes:Large"] ?? "t3.large",
            _ => "t3.small"
        };
    }

    private string GetWorkerAmiId(JobLanguage language)
    {
        // This would typically come from configuration or be looked up dynamically
        // based on the ECR image and region
        return _configuration[$"AWS:WorkerAmi:{language}"]
            ?? _configuration["AWS:WorkerAmi:Default"]
            ?? throw new InvalidOperationException("Worker AMI not configured");
    }

    private string GetSubnetId()
    {
        var subnets = _configuration.GetSection("AWS:SubnetIds").Get<string[]>();
        if (subnets == null || subnets.Length == 0)
        {
            throw new InvalidOperationException("No subnets configured");
        }

        // Simple round-robin across subnets
        return subnets[Random.Shared.Next(subnets.Length)];
    }

    private string GenerateUserData(JobDefinition job)
    {
        var managerUrl = _configuration["ManagerUrl"]
            ?? throw new InvalidOperationException("ManagerUrl not configured");

        var s3Bucket = _configuration["AWS:S3Bucket"]
            ?? throw new InvalidOperationException("S3Bucket not configured");

        return $@"#!/bin/bash
set -e

# Job System Worker Bootstrap Script
export JOB_NAME=""{job.Name}""
export MANAGER_URL=""{managerUrl}""
export API_KEY=""{job.ApiKey}""
export S3_CODE_PATH=""s3://{s3Bucket}/{job.S3CodePath}""
export LANGUAGE=""{job.Language}""
export QUEUE_TYPE=""{job.QueueType}""
export QUEUE_CONNECTION=""{job.QueueConnectionString}""
export QUEUE_NAME=""{job.QueueName}""

# Install AWS CLI if not present
if ! command -v aws &> /dev/null; then
    curl ""https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip"" -o ""awscliv2.zip""
    unzip awscliv2.zip
    sudo ./aws/install
fi

# Create app directory
mkdir -p /app/job-code
cd /app

# Pull code from S3
aws s3 sync $S3_CODE_PATH /app/job-code

# Start worker based on language
if [ ""$LANGUAGE"" == ""Node"" ]; then
    cd /app/job-code
    npm install --production
    MANAGER_URL=$MANAGER_URL API_KEY=$API_KEY JOB_NAME=$JOB_NAME node /app/bootstrap.js
elif [ ""$LANGUAGE"" == ""CSharp"" ]; then
    cd /app/job-code
    dotnet restore
    MANAGER_URL=$MANAGER_URL API_KEY=$API_KEY JOB_NAME=$JOB_NAME dotnet run --configuration Release
fi
";
    }
}
