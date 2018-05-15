# EventStoreConsumer

This template creates an Event Store consumer Console application designed to be run as a service within Docker. 

The application logs to Serilog and has both Console and [Seq](http://getseq.net) sinks configured. 

It provides the boilerplate code required to subscribe to Event Store streams and makes it easy to build strongly-typed
event handlers by deriving from the `Consumer` base class (see below).

## Installation

```
dotnet new -i ./EventStoreConsumer/ 
```

## Usage

```
dotnet new cko-es-consumer
```

## Project contents

This template builds on top of the [ConsoleService](https://github.com/CKOTech/checkout-bootstrap-dotnet/tree/master/ConsoleService) template. In addition to the base set of files, it also includes the following:

- `Consumer.cs` - Provides a base class for Event Store stream consumers
- `ConsumerManager.cs` - Manages running stream consumers
- `EmailEventsConsumer.cs` - A sample stream consumer that consumes events from the `emails` stream
- `EventStoreCheckpointManager.cs` - A checkpoint manager that stores consumer checkpoints in Event Store in streams named `Ditto_{ConsumerName}_Checkpoint`
- `SerilogEventStoreLogger` - An Event Store logger that uses Serilog
- `Events/EmailSent` - An example event type that will be bound to `EmailSent` events in Event Store

The underlying interfaces for the above classes have been ommited since they are self explanatory.

Similar to the [ConsoleService](https://github.com/CKOTech/checkout-bootstrap-dotnet/tree/master/ConsoleService) template template, `AppService` is the entry point to your application logic and in this template, simply starts and stops the `ConsumerManager`.

## Docker

A `Dockerfile` is included that makes use of [multi-stage builds](https://docs.docker.com/engine/userguide/eng-image/multistage-build/) to build a docker image.

For example, to build the image, execute the following within the application directory:

```
docker build -t ckotech/event-store-consumer-template . 
```

To then run the application within docker:

```
dotnet run -it --rm ckotech/event-store-consumer-template
```

## Running the application

The easiest way to get Event Store running on your machine is to use Docker. A `docker-compose` file is included that will bring up an Event Store instance and build and run your application in docker.

Environment variables are used to override the default settings in `appsettings.json`, and should be prefixed with the name of your application.

To bring up the environment with `docker-compose` (after building the image):

```
docker-compose up
```

To run *and* build the docker image:

```
docker-compose up --build
```

Once up, you can create some `EmailSent` events directly in Event Store using cUrl or your favourite HTTP test tool:

```
curl -X POST \
  http://localhost:2113/streams/emails \
  -H 'accept: application/json' \
  -H 'cache-control: no-cache' \
  -H 'content-type: application/vnd.eventstore.events+json' \
  -H 'es-expectedversion: -2' \
  -d '[
  {
    "eventId": "1fd07248-d04f-413b-a88d-9adb00ed9a1b",
    "eventType": "EmailSent",
    "data": {
      "from": "ben.foster@checkout.com",
      "to": "engineering@checkout.com",
      "sentOn": "2017-11-08T07:27:04Z",
      "message": "Hey, have you heard about Event Store?"
    }
  }
]
'
```

## Creating an Event Store Consumer

The `Consumer` base class makes it easy to consume Event Store streams in a strongly typed way. 

Suppose we store `HeroRegistered` events in Event Store for our super-hero convention registration system, in the following format:

```json
{
  "name": "Batman",
  "costume": "Dark with bat mask",
  "powers": [
    "Genius Intellect",
    "Martial Arts",
    "Detective Skills"
  ]
}
```

The first step is to create a class that maps to your event schema. It should have the same name as the event in Event Store since we use the class name to set up the mapping:

```csharp
    public class HeroRegistered
    {
        public string Name { get; set; }
        public string Costume { get; set; }
        public IEnumerable<string> Powers { get; set; }
    }
```

Once done we can define a consumer to consume events from the `heroes` stream and handle the `HeroRegistered` event:

```csharp
    public class HeroEventsConsumer : Consumer
    {
        public HeroEventsConsumer(ILogger logger) : base(logger, "heroes")
        {
            When<HeroRegistered>(OnHeroRegistered);
        }

        private void OnHeroRegistered(HeroRegistered e)
        {
            Logger.Information("Hero {hero} just registered!", e.Name);
        }
    }
```

You can handle as many events in this stream as you like, just add the appropriate handler, for example:

```csharp
    public HeroEventsConsumer(ILogger logger) : base(logger, "heroes")
    {
        When<HeroRegistered>(OnHeroRegistered);
        When<VillainRegistered>(OnVillainRegistered);
        When<BattleStarted>(OnBattleStarted);
        When<HeroWon>(OnHeroWon);
    }
```

The final step is to register your consumer with the application container. Open up `AppRegistry.cs` and register your consumer:

```csharp
    For<IConsumer>().Add<HeroEventsConsumer>();
```

Once done it will be automatically injected into the `ConsumerManager` when it starts. 

Note: The consumer sets up a catch-up subscription by default. If you want to use a live subscription or implement competing consumers you'd need to extend the appropriate components.
