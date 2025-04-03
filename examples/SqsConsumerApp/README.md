# SQS Consumer Sample App

## Setup

### Local infrastructure

`docker compose up -d` to spin up the localstack & Aspire containers.

### SQS queue

```shell
aws --endpoint-url=http://localhost:4566 --region=us-east-1 --no-sign-request \
    sqs create-queue --queue-name "weather-forecasts"
```

To get the queue URL:

```shell
aws --endpoint-url=http://localhost:4566 --region=us-east-1 --no-sign-request \
  sqs get-queue-url --queue-name "weather-forecasts" --query "QueueUrl"
```

## Run

To see that the consumer is working, you can send a message to the queue using the AWS CLI:

```shell
aws --endpoint-url=http://localhost:4566 --region=us-east-1 --no-sign-request \
    sqs send-message \
    --queue-url "http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/weather-forecasts" \
    --message-body '{"TemperatureC": 25, "TemperatureF": 77, "Summary": "not hot, not cold, perfect"}'
```
