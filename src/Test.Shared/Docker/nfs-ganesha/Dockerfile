FROM debian:bookworm-slim

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends dbus nfs-ganesha nfs-ganesha-vfs rpcbind \
 && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/blobject-ganesha-entrypoint.sh
RUN chmod +x /usr/local/bin/blobject-ganesha-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/blobject-ganesha-entrypoint.sh"]
