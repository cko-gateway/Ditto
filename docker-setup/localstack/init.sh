#!/bin/bash
aws configure set aws_access_key_id "XXX"
aws configure set aws_secret_access_key	"XXX"
aws configure set aws_session_token "XXX"
aws configure set region "eu-west-1"
aws configure set output "json"
aws --endpoint-url=http://localstack:4566 kinesis create-stream --stream-name ditto --shard-count 1
aws --endpoint-url=http://localstack:4566 kinesis wait stream-exists --stream-name ditto

awslocal sqs create-queue --queue-name ditto
