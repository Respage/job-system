output "vpc_id" {
  description = "VPC ID"
  value       = aws_vpc.main.id
}

output "public_subnet_ids" {
  description = "Public subnet IDs"
  value       = aws_subnet.public[*].id
}

output "private_subnet_ids" {
  description = "Private subnet IDs"
  value       = aws_subnet.private[*].id
}

output "manager_security_group_id" {
  description = "Security group ID for the manager"
  value       = aws_security_group.manager.id
}

output "worker_security_group_id" {
  description = "Security group ID for workers"
  value       = aws_security_group.worker.id
}

output "worker_instance_profile_name" {
  description = "IAM instance profile name for workers"
  value       = aws_iam_instance_profile.worker.name
}

output "manager_instance_profile_name" {
  description = "IAM instance profile name for the manager"
  value       = aws_iam_instance_profile.manager.name
}

output "s3_bucket_name" {
  description = "S3 bucket name for job code"
  value       = aws_s3_bucket.code.bucket
}

output "ecr_repository_node_url" {
  description = "ECR repository URL for Node.js worker"
  value       = aws_ecr_repository.worker_node.repository_url
}

output "ecr_repository_dotnet_url" {
  description = "ECR repository URL for .NET worker"
  value       = aws_ecr_repository.worker_dotnet.repository_url
}

output "worker_ami_id" {
  description = "AMI ID used for workers"
  value       = local.worker_ami
}

output "aws_region" {
  description = "AWS region"
  value       = var.aws_region
}

output "manager_config" {
  description = "Configuration values for the manager appsettings.json"
  value = {
    AWS = {
      Region                = var.aws_region
      S3Bucket              = aws_s3_bucket.code.bucket
      VpcId                 = aws_vpc.main.id
      SubnetIds             = aws_subnet.private[*].id
      SecurityGroupId       = aws_security_group.worker.id
      WorkerInstanceProfile = aws_iam_instance_profile.worker.name
    }
  }
}
