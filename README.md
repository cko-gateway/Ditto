# Ditto

Ditto is a cluster replication tool for [Event Store](http://eventstore.org). It works by subscribing to specific streams from a _source_ cluster and replicating them to a _destination_ cluster.

It was designed to be run as a standalone application using Docker. 

Most of the code is part of our boilerplate Event Consumer template that we use at Checkout.com and automatically take care of Event Store connection management and logging via Serilog / Seq.

### Configuration

The application can be configured via JSON (`appsettings.json`) or using Environment Variables:

| JSON Setting | Environment Variable | Default Value | Description |
| ------------ | -------------------- | ----------- | ----------- |
| SourceEventStoreConnectionString | Ditto_Settings:SourceEventStoreConnectionString |   | The source event store connection string |
| DestinationEventStoreConnectionString | Ditto_Settings:DestinationEventStoreConnectionString |   | The destination event store connection string |
| CheckpointSavingInterval | Ditto_Settings:CheckpointSavingInterval | 5000 | The interval in milliseconds before the current checkpoint is saved |
| StreamIdentifiers | Ditto_Settings:StreamIdentifiers |  | Semi-colon (`;`) separated identifiers of streams that should be replicated* |
| CheckpointManagerRetryCount | Ditto_Settings:CheckpointManagerRetryCount | 5 | The number of times the Checkpoint Manager should attempt to save the Checkpoint in the event of a failure
| CheckpointManagerRetryInterval | Ditto_Settings:CheckpointManagerRetryInterval | 1000 | The interval in milliseconds between Checkpoint Manager retries |
| ReplicationThrottleInterval | Ditto_Settings:ReplicationThrottleInterval | 0 | The interval in milliseconds to wait between events. This can be useful if you want to reduce the load on your source server |


**Note - When replicating category streams you will need to escape the `$` in the stream identifier under docker for example, to replicate the category stream `$ce-emails` set your stream identifier to `$$ce-emails`.

### Ditto Checkpoints

Ditto will start a new catchup subscription for each stream you subscribe to and automatically take care of maintaining the last checkpoint of each stream. This means you can safely stop and start Ditto and it will pick up where it left off. 

Ditto generates a checkpoint stream for each stream you subscribe to, named according to the source stream. For example, when subscribing to the `$ce-emails` stream, a new checkpoint stream will be created on the destination server called `Ditto_ReplicatingConsumer_ce_emails_Checkpoint`. Each time the Ditto Checkpoint Manager saves the current checkpoint, a `Checkpoint` event is written to the above stream, for example:

```
{
  "LastEventProcessed": 0
}
```

The Ditto Checkpoint Manager defers the writing of checkpoints according to the `CheckpointSavingInterval` setting. If you're replicating a large number of events it does not make sense to write the checkpoint after each event is replicated. In most cases we dial this up to around 10,000 milliseconds. 

#### Idempotency

Note that because we're using the source stream event version and event ID, the writes are idempotent. This means that should Ditto be shut down before the last checkpoint is written, you will not get duplicate events when it restarts.

### Replication Considerations

Ditto was originally designed to replicate the `$all` system stream. However, I found that this resulted in the replication of a lot of internal streams/events which we did not want. Since it's [not currently possible](https://github.com/EventStore/EventStore/issues/718) to ignore system streams I opted to explicitly specify the streams we want to replicate.

Instead we subscribe to category streams e.g. `$ce-emails` and then populate the original streams on the destination cluster, for example:

![Ditto in action](docs/img/ditto.png)

### Running the example

To run the example, clone the repository and run `docker-compose up --build` from the root of the repository. This will start:

1. Source Event Store at [http://localhost:2113](http://localhost:2113)
2. Destination Event Store at [http://localhost:4113](http://localhost:4113)
3. SEQ at [http://localhost:5341](http://localhost:5341)
4. Ditto replicating the `$ce-emails` category stream from the source to destination servers

To test the replication you can use the Event Store HTTP API to create some email events on the source:

```
curl -X POST \
  http://localhost:2113/streams/emails-john.smith@example.com \
  -H 'Accept: application/json' \
  -H 'Cache-Control: no-cache' \
  -H 'Content-Type: application/vnd.eventstore.events+json' \
  -H 'ES-ExpectedVersion: -2' \
  -H 'Postman-Token: a75df4e7-7aa5-d5c4-e056-580d71c28ac6' \
  -d '[
  {
    "eventId": "ba54c41a-15fe-4c0d-9687-6b017df77b37",
    "eventType": "EmailSent",
    "data": {
      "from": "info@eventstore.org",
      "sentOn": "2017-11-08T07:27:04Z",
      "message": "Hey, have you heard about Event Store?"
    }
  }
]
'
```

You should then be able to browse to the stream at [http://localhost:2113/web/index.html#/streams/emails-john.smith@example.com](http://localhost:2113/web/index.html#/streams/emails-john.smith@example.com) and see the event in the `$ce-emails` category stream at [http://localhost:2113/web/index.html#/streams/$ce-emails](http://localhost:2113/web/index.html#/streams/$ce-emails).

Ditto will then replicate this event to the destination server/cluster. You should see output similar to the following in stdout and in SEQ:

```
2018-05-15 10:11:16 INF Replicating "EmailSent" #0 from "emails-john.smith@example.com" "completed" in 35.9 ms
```

You should then be able to browse to the same stream on the destination server at [http://localhost:4113/web/index.html#/streams/emails-john.smith@example.com](http://localhost:4113/web/index.html#/streams/emails-john.smith@example.com)

