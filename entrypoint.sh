#!/bin/ash

if [ -z "$CRON" ]; then
    echo "CRON is not set. Exiting."
    exit 1
fi

echo "$CRON"

echo "$CRON ASH_ENV=/.env /app/Hariane2Mqtt > /dev/stdout 2> /dev/stdout" > /etc/crontabs/root

crond -f -L /dev/stdout