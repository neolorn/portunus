#!/bin/sh
# Makes the container's host key, starts OpenSSH's server, and runs the agent as root through
# the test harness, the swaps file in place of /proc/swaps the one path set (P-027, P-044); its
# log is the container's.
set -e
ssh-keygen -q -t ed25519 -N '' -f /etc/ssh/ssh_host_ed25519_key
/usr/sbin/sshd
exec dotnet /opt/portunus/agent/Portunus.Agent.Harness.dll /etc/portunus /opt/portunus/swaps run sample
