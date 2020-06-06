FROM eventstore/eventstore:release-5.0.8
RUN apt-get update && apt-get install -y dos2unix

COPY configure-node.sh \
 create_events.sh \
 subscription.json \
 /opt/eventstore/

WORKDIR /opt/eventstore

RUN dos2unix configure-node.sh create_events.sh
RUN chmod +x ./configure-node.sh ./create_events.sh

ENTRYPOINT ["./configure-node.sh"]