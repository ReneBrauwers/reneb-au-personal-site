FROM nginxinc/nginx-unprivileged:1.29.1-alpine3.22-slim@sha256:ba47582e1ad3ad1df0c12a21bf464770c5b995e5f06db4aabead4ab1ae2858c4

COPY --chown=101:101 nginx/nginx.conf /etc/nginx/nginx.conf
COPY --chown=101:101 site/ /usr/share/nginx/html/

USER 101:101
EXPOSE 8080

ENTRYPOINT ["/usr/sbin/nginx"]
CMD ["-g", "daemon off;"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
