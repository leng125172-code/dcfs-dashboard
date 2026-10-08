#!/bin/sh
set -eu

envsubst \
  '${DASHBOARD_TITLE} ${DASHBOARD_API_BASE_URL} ${DASHBOARD_AUTHENTIK_URL} ${DASHBOARD_GITLAB_URL}' \
  < /usr/share/nginx/runtime-config.template.js \
  > /tmp/runtime-config.js

exec nginx -g 'daemon off;'
