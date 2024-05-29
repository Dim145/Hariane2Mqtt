#!/bin/sh

# Exit if /config is not mounted
if [ ! -d /config ]; then
	echo "Error: /config not mounted"
    exit 0
fi

# Default location
CONFIGSOURCE="/config/gazpar_2_mqtt/config.yaml"

mkdir -p /config/gazpar_2_mqtt

# transform each config.yaml entries to env variables
if [ -f $CONFIGSOURCE ]; then
    echo "Loading config from $CONFIGSOURCE"
    while IFS=': ' read -r key value
    do
        if [ ! -z "$key" ] && [ ! -z "$value" ]; then
            key=$(echo $key | tr '[:lower:]' '[:upper:]' | tr '-' '_' | tr '.' '_')
            value=$(echo $value | sed 's/^ *//g' | sed 's/ *$//g')
            
            # add to env for cron jobs
            export $key=$value
        fi
    done < $CONFIGSOURCE
fi

exec "$@"