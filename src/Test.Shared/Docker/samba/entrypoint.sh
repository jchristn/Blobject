#!/bin/sh
set -eu

SMB_USER="${SMB_USER:-blobject}"
SMB_PASSWORD="${SMB_PASSWORD:-Blobject-Test-123!}"
SMB_SHARE="${SMB_SHARE:-share}"

id "$SMB_USER" >/dev/null 2>&1 || useradd --no-create-home --shell /usr/sbin/nologin "$SMB_USER"
mkdir -p /srv/share
chown "$SMB_USER" /srv/share
chmod 0777 /srv/share
printf '%s\n%s\n' "$SMB_PASSWORD" "$SMB_PASSWORD" | smbpasswd -a -s "$SMB_USER" >/dev/null

cat > /etc/samba/smb.conf <<CONF
[global]
    workgroup = WORKGROUP
    server role = standalone server
    map to guest = never
    server min protocol = SMB2_02
    smb ports = 445
    disable netbios = yes
    load printers = no
    printing = bsd
    printcap name = /dev/null
    disable spoolss = yes
    log level = 1

[$SMB_SHARE]
    path = /srv/share
    read only = no
    browseable = yes
    valid users = $SMB_USER
    create mask = 0664
    directory mask = 0775
CONF

exec smbd --foreground --no-process-group --debug-stdout
