FROM alpine:3.22 AS certificates

FROM debian:bookworm-slim
COPY --from=certificates /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY rurimecha-core /usr/local/bin/rurimecha-core
COPY media-tools /usr/local/bin/media-tools
COPY third_party /usr/share/doc/rurimecha/third_party
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["/usr/local/bin/rurimecha-core"]
CMD ["-data-dir", "/data"]
