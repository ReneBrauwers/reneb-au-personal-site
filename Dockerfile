FROM nginxinc/nginx-unprivileged:1.31.3-alpine3.24-slim@sha256:ff4671e70f4f903721c5eacce1373d3e5d21b3d5f6fb03982154aabd084ed32e

COPY --chown=101:101 nginx/nginx.conf /etc/nginx/nginx.conf
COPY --chown=101:101 site/ /usr/share/nginx/html/

USER 101:101
EXPOSE 8080

ENTRYPOINT ["/usr/sbin/nginx"]
CMD ["-g", "daemon off;"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
