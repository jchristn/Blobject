FROM --platform=linux/amd64 nimbix/unfs3@sha256:d97464d023cba317e9b970b108206b088469540c1fb2ad4c6fa1b2b0216f56d1

RUN mkdir -p /export && chmod 0777 /export \
 && printf '/export (rw,insecure,no_root_squash)\n' > /etc/exports

ENTRYPOINT ["/bin/sh", "-c"]
CMD ["exec unfsd -d -t -p -n 2049 -m ${MOUNT_PORT:-20048} -e /etc/exports"]
