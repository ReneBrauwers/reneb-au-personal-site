FROM nginxinc/nginx-unprivileged:1.31.7-alpine3.24-slim@sha256:a8cb0f33d51fa107c96fd2bd3a71aaf564ec131e9d2bdf8700944f0ed8b170d7

COPY --chown=101:101 nginx/nginx.conf /etc/nginx/nginx.conf
COPY --chown=101:101 site/ /usr/share/nginx/html/

USER 101:101
EXPOSE 8080

ENTRYPOINT ["/usr/sbin/nginx"]
CMD ["-g", "daemon off;"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
