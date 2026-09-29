FROM debian:bookworm-slim

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends samba \
 && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/blobject-samba-entrypoint.sh
RUN chmod +x /usr/local/bin/blobject-samba-entrypoint.sh

EXPOSE 445
ENTRYPOINT ["/usr/local/bin/blobject-samba-entrypoint.sh"]
