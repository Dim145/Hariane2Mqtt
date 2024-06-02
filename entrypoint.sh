#!/bin/ash

if [ -z "$CRON" ]; then
    echo "CRON is not set. Exiting."
    exit 1
fi

echo "$CRON"

echo "$CRON ASH_ENV=/.env /app/Hariane2Mqtt > /dev/stdout 2> /dev/stdout" > /etc/crontabs/root

if [ -z "$HASS_URL" ]; then
    echo "HASS environment variable is not set. The service will not be registered."
else 
    echo "HASS_URL is set to $HASS_URL"
    
    # Function to register the service with Home Assistant
    register_service() {
        echo "Registering service sync_hariane..."
    
        # Here we use the API to register the service
        curl -X POST "$HASS_URL/api/services/sync_hariane" \
            -H "Authorization: Bearer $SUPERVISOR_TOKEN" \
            -H "Content-Type: application/json" \
            -d '{"service": "sync_hariane", "description": "A custom service for syncing Hariane."}'
    }
    
    # Function to listen for WebSocket events and handle service calls
    listen_for_service_calls() {
        while true; do
            # Connect to Home Assistant WebSocket
            exec 3<>/dev/tcp/$(echo $WS_URL | sed 's|ws://;s|/api/websocket')
    
            # Send authentication message
            echo "{\"type\": \"auth\", \"access_token\": \"$SUPERVISOR_TOKEN\"}" >&3
    
            # Read authentication response
            read -r response <&3
            echo "Auth response: $response"
    
            # Subscribe to service call events
            echo "{\"id\": 1, \"type\": \"subscribe_events\", \"event_type\": \"call_service\"}" >&3
    
            # Listen for events
            while read -r event <&3; do
                echo "Received event: $event"
    
                # Check if the event is a call to our service
                if echo "$event" | grep -q "\"domain\": \"sync_hariane\""; then
                    echo "sync_hariane service called"
    
                    # Execute the desired command
                    echo "test"
                fi
            done
    
            # Sleep before reconnecting in case of disconnection
            sleep 5
        done
    }
    
    # Main execution starts here
    register_service
    listen_for_service_calls
fi

crond -f -L /dev/stdout