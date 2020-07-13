#!/bin/bash
# configure-node.sh

# Enables job control
set -m

# Enables error propagation
set -e

echo "starting event store"

/entrypoint.sh eventstored &

check_db() {
  curl --silent http://127.0.0.1:2113/status > /dev/null
  echo $?
}

create_subscriptions() {
  create_status=$(curl -s -o /dev/null -w "%{http_code}" -X PUT \
     -H "Content-Type: application/json" \
     -u admin:changeit \
     -d @subscription.json \
     http://127.0.0.1:2113/subscriptions/\$ce-customer/ditto-customer)


  create_status=$(curl -s -o /dev/null -w "%{http_code}" -X PUT \
     -H "Content-Type: application/json" \
     -u admin:changeit \
     -d @subscription.json \
     http://127.0.0.1:2113/subscriptions/\$ce-customer/ditto-kinesis-customer)

  if [[ $create_status -eq 401 ]]; then
    echo 1
  else 
    echo 0
  fi
}

check_subscriptions() {
   check_status=$(curl -s -o /dev/null -w "%{http_code}" \
     -u admin:changeit \
     http://127.0.0.1:2113/subscriptions/\$ce-customer/ditto-customer)

   check_status=$(curl -s -o /dev/null -w "%{http_code}" \
     -u admin:changeit \
     http://127.0.0.1:2113/subscriptions/\$ce-customer/ditto-kinesis-customer)

  if [[ $check_status -eq 200 ]]; then
    echo 0
  else
    echo 1
  fi
}

# Wait until it's ready
until [[ $(check_db) == 0 ]]; do
  >&2 echo "Waiting for eventstore Server to be available ..."
  sleep 1
done

until [[ $(create_subscriptions) == 0 ]]; do
  >&2 echo "Failed to create subscription ..."
  sleep 1
done

if [ "$SEED_DATA" = true ] ; then
  # create seed data via http - https://eventstore.org/docs/http-api/creating-writing-a-stream/index.html
  echo "Create events ........."
  ./create_events.sh 10 0
  echo "Finished seeding eventstore"
fi

until [[ $(check_subscriptions) = 0 ]]; do
  >&2 echo "Unable to find subscription ..."
  sleep 1
done

fg 1
