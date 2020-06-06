
create_events(){
    if [ -z "$1" ]; then 
        LIMIT=1
    else 
        LIMIT=$1
    fi
    
    if [ -z "$2" ]; then 
        DELAY=0
    else 
        DELAY=$2
    fi

    COUNTER=0
    while [  $COUNTER -lt $LIMIT ]; do
        UUID=$(cat /proc/sys/kernel/random/uuid)
        customerUUID=$(cat /proc/sys/kernel/random/uuid)

        curl -s -o /dev/null -w "%{http_code}" --location --request POST "http://127.0.0.1:2113/streams/customer-$customerUUID" \
        -u admin:changeit \
        -H 'Content-Type: application/vnd.eventstore.events+json' \
        --data-raw '[
            {
                "eventId": "'"$UUID"'",
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
            
        COUNTER=$((COUNTER+1))
        echo " - created event number $COUNTER"
        sleep $DELAY
    done
}

create_events $1 $2
