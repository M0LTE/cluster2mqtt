#!/bin/bash
set -e

REMOTE_PATH="/opt/cluster2mqtt"
SERVICE_NAME="cluster2mqtt"

# Create user if doesn't exist
if ! id -u cluster2mqtt >/dev/null 2>&1; then
    sudo useradd -r -s /sbin/nologin cluster2mqtt
    echo 'Created cluster2mqtt user'
fi

# Create directory if doesn't exist
sudo mkdir -p $REMOTE_PATH

# Stop service if running
sudo systemctl stop $SERVICE_NAME 2>/dev/null || true

# Copy new files
sudo cp -r /tmp/cluster2mqtt-deploy/* $REMOTE_PATH/
sudo chmod +x $REMOTE_PATH/cluster2mqtt

# Preserve existing appsettings.Production.json if it exists
if [ -f "$REMOTE_PATH/appsettings.Production.json" ]; then
    echo 'Keeping existing appsettings.Production.json'
fi

# Set ownership
sudo chown -R cluster2mqtt:cluster2mqtt $REMOTE_PATH

# Install systemd service
sudo cp /tmp/cluster2mqtt.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME

# Start service
sudo systemctl start $SERVICE_NAME

# Cleanup
rm -rf /tmp/cluster2mqtt-deploy
rm -f /tmp/cluster2mqtt.service
rm -f /tmp/install-remote.sh

# Show status
sleep 2
sudo systemctl status $SERVICE_NAME --no-pager
