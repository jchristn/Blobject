FROM debian:bookworm-slim

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends kmod nfs-common nfs-kernel-server rpcbind \
 && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/blobject-knfsd-entrypoint.sh
RUN chmod +x /usr/local/bin/blobject-knfsd-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/blobject-knfsd-entrypoint.sh"]
