#!/bin/sh
# Builds the plugin and installs it into a Jellyfin container over SSH, then restarts it:
#   JELLYFIN_SSH=user@host JELLYFIN_CONTAINER=jellyfin ./install.sh
# DOCKER defaults to `docker`; set it where the remote shell's PATH lacks it.
set -e
cd "$(dirname "$0")"
: "${JELLYFIN_SSH:?set JELLYFIN_SSH to user@host}" "${JELLYFIN_CONTAINER:?set JELLYFIN_CONTAINER}"
DOCKER="${DOCKER:-docker}"
dotnet build src -c Release -p:Version="$(sed -n 's/.*"version": "\(.*\)".*/\1/p' src/meta.json)"
VERSION=$(sed -n 's/.*"version": "\(.*\)".*/\1/p' src/meta.json)
DIR="/config/plugins/MDBList Ratings_$VERSION"
scp -q src/bin/Release/net10.0/Jellyfin.Plugin.MdbListRatings.dll src/meta.json "$JELLYFIN_SSH:/tmp/"
ssh "$JELLYFIN_SSH" "$DOCKER exec $JELLYFIN_CONTAINER sh -c 'rm -rf /config/plugins/MDBList\\ Ratings_*' \
  && $DOCKER exec $JELLYFIN_CONTAINER mkdir -p '$DIR' \
  && $DOCKER cp /tmp/Jellyfin.Plugin.MdbListRatings.dll '$JELLYFIN_CONTAINER:$DIR/' \
  && $DOCKER cp /tmp/meta.json '$JELLYFIN_CONTAINER:$DIR/' \
  && $DOCKER restart $JELLYFIN_CONTAINER"
