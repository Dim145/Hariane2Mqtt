ARG BUILD_FROM=dim145/hariane2mqtt:0.2.3

FROM ${BUILD_FROM} AS base

RUN apk add jq

COPY entrypoint.sh /addon_entrypoint.sh

RUN chmod +x /addon_entrypoint.sh

VOLUME /data

ENTRYPOINT ["/addon_entrypoint.sh"]
CMD ["/entrypoint.sh"]

############
# 5 Labels #
############

LABEL \
  io.hass.version="0.2.4" \
  io.hass.type="addon" \
  io.hass.arch="amd64|arm64"