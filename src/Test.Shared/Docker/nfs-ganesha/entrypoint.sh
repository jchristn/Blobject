#!/bin/sh
set -eu

MOUNT_PORT="${MOUNT_PORT:-20048}"

mkdir -p /export
chmod 0777 /export

cat > /etc/ganesha/ganesha.conf <<CONF
NFS_CORE_PARAM
{
    Protocols = 3, 4;
    NFS_Port = 2049;
    MNT_Port = $MOUNT_PORT;
    mount_path_pseudo = true;
    Enable_NLM = false;
    Enable_RQUOTA = false;
}

EXPORT_DEFAULTS
{
    Access_Type = RW;
    Squash = No_Root_Squash;
    SecType = sys;
}

EXPORT
{
    Export_Id = 1;
    Path = /export;
    Pseudo = /export;
    Access_Type = RW;
    Disable_ACL = true;
    Protocols = 3, 4;
    Transports = TCP;
    SecType = sys;
    Squash = No_Root_Squash;

    CLIENT
    {
        Clients = 0.0.0.0, ::0;
        Access_Type = RW;
    }

    FSAL
    {
        Name = VFS;
    }
}
CONF

mkdir -p /run/dbus /var/run/ganesha /var/lib/nfs/ganesha

# remove state left by a previous run so the container survives docker restart
rm -f /run/dbus/pid /var/run/dbus/pid /run/rpcbind.lock /run/rpcbind/rpcbind.lock /var/run/ganesha/ganesha.pid
dbus-daemon --system --fork
rpcbind -w
exec ganesha.nfsd -F -L /dev/stderr -f /etc/ganesha/ganesha.conf
