#!/bin/sh
set -eu

envsubst \
  '${WHALEDECK_TITLE} ${WHALEDECK_API_BASE_URL} ${WHALEDECK_AUTHENTIK_URL} ${WHALEDECK_GITLAB_URL}' \
  < /usr/share/nginx/runtime-config.template.js \
  > /tmp/runtime-config.js

exec nginx -g 'daemon off;'
