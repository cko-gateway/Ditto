# Ditto Kinesis

Ditto Kinesis extends the base Ditto library to support replicating events to a Kinesis stream. The configuration is the same as the default Ditto application with the exception that you also need to set the `Ditto_Kinesis__StreamName` environment variable.

AWS settings are configured using the Microsoft configuration framework as per https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-netcore.html.

Event Store events are sent to Kinesis as JSON. Given the following event written to Event Store:

```
curl --location --request POST 'http://localhost:2113/streams/customer-8cda47e7-8c9c-4c1f-8dd9-6534f6179819' \
--header 'Content-Type: application/vnd.eventstore.events+json' \
--header 'ES-ExpectedVersion: -1' \
--header 'Accept: application/json' \
--header 'Authorization: Basic YWRtaW46Y2hhbmdlaXQ=' \
--data-raw '[
    {
        "eventId": "48b47bfd-50f6-4df2-8d62-c4e00341e701",
        "eventType": "customer_registered",
        "data": {
            "first_name": "John",
            "last_name": "Smith",
            "phone_number": "0111111111111"
        },
        "metadata": {
            "source": "ditto"
        }
    }
]'
```

The following JSON is written to Kinesis:

```json
{
   "stream_id":"customer-8cda47e7-8c9c-4c1f-8dd9-6534f6179819",
   "event_id":"48b47bfd-50f6-4df2-8d62-c4e00341e701",
   "event_number":0,
   "event_type":"customer_registered",
   "event_timestamp":"2020-07-03T07:32:58.916158Z",
   "replicated_on":"2020-07-03T07:41:17.847499Z",
   "data":{
      "first_name":"John",
      "last_name":"Smith",
      "phone_number":"0111111111111"
   },
   "metadata":{
      "source":"ditto"
   }
}
```

Note that the formatting of the `data` and `metadata` JSON is preserved so if you use Pascal-Case accessing the first name property above would be accessible at `data.FirstName`.
