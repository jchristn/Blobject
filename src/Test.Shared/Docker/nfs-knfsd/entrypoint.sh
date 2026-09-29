#!/bin/sh
set -eu

MOUNT_PORT="${MOUNT_PORT:-20048}"

mkdir -p /export
chmod 0777 /export

mkdir -p /proc/fs/nfsd /var/lib/nfs/rpc_pipefs /run/rpcbind
modprobe nfsd 2>/dev/null || true
mountpoint -q /proc/fs/nfsd || mount -t nfsd nfsd /proc/fs/nfsd
mountpoint -q /var/lib/nfs/rpc_pipefs || mount -t rpc_pipefs sunrpc /var/lib/nfs/rpc_pipefs

printf '/export *(rw,sync,no_subtree_check,no_root_squash,insecure,fsid=0)\n' > /etc/exports

rm -f /run/rpcbind.lock /run/rpcbind/rpcbind.lock
rpcbind -w
exportfs -ra
rpc.nfsd -N 4 -V 3 --tcp 8

cleanup() {
    rpc.nfsd 0 || true
    exportfs -au || true
    if [ -n "${mountd_pid:-}" ]; then
        kill "$mountd_pid" 2>/dev/null || true
        wait "$mountd_pid" 2>/dev/null || true
    fi
    umount /proc/fs/nfsd || true
    umount /var/lib/nfs/rpc_pipefs || true
}

trap cleanup TERM INT

rpc.mountd -F --port "$MOUNT_PORT" --no-udp -V 3 &
mountd_pid=$!
wait "$mountd_pid"
