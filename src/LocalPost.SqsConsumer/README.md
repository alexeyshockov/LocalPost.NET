# LocalPost Amazon SQS Consumer

## IAM Permissions

To operate on a queue, below [permissions](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-api-permissions-reference.html) are required:
- `sqs:GetQueueUrl`
- `sqs:GetQueueAttributes`
- `sqs:ReceiveMessage`
- `sqs:ChangeMessageVisibility`
