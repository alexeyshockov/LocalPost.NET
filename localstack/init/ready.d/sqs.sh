#!/usr/bin/env bash

set -euo pipefail

# Enable debug
#set -x

awslocal sqs create-queue --queue-name lp-test
QUEUE_URL=$(awslocal sqs get-queue-url --queue-name lp-test --query 'QueueUrl' --output text)

awslocal sqs send-message \
    --queue-url "$QUEUE_URL" \
    --message-body '{"TemperatureC": 25, "TemperatureF": 77, "Summary": "not hot, not cold, perfect"}'
