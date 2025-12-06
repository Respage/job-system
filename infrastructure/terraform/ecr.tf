# ECR Repository for Node.js Worker
resource "aws_ecr_repository" "worker_node" {
  name                 = "${local.name_prefix}-worker-node"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name = "${local.name_prefix}-worker-node"
  }
}

# ECR Repository for .NET Worker
resource "aws_ecr_repository" "worker_dotnet" {
  name                 = "${local.name_prefix}-worker-dotnet"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name = "${local.name_prefix}-worker-dotnet"
  }
}

# Lifecycle policy for ECR repositories
resource "aws_ecr_lifecycle_policy" "worker_node" {
  repository = aws_ecr_repository.worker_node.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 10 images"
        selection = {
          tagStatus     = "any"
          countType     = "imageCountMoreThan"
          countNumber   = 10
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

resource "aws_ecr_lifecycle_policy" "worker_dotnet" {
  repository = aws_ecr_repository.worker_dotnet.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 10 images"
        selection = {
          tagStatus     = "any"
          countType     = "imageCountMoreThan"
          countNumber   = 10
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}
