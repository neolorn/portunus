# The throwaway server of the owner's check by hand of a push (P-044): OpenSSH's server from the
# SDK image's own package sources, and the agent, run through the test harness, in push-only
# mode. build/check-push-by-hand.ps1 builds it from a folder under tmp/ holding these files, the
# harness published for linux-x64 from the clone (agent/), and the owner's public key
# (authorized_keys), and removes it at the end.
ARG SDK
FROM mcr.microsoft.com/dotnet/sdk:${SDK}

# The host keys the package makes are removed: start.sh makes the container's own at its start.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends openssh-server \
    && rm -rf /var/lib/apt/lists/* /etc/ssh/ssh_host_*

# The push group the agent's configuration names, and the owner's login in it, which signs in by
# key alone.
RUN groupadd --gid 4244 portunus-push \
    && useradd --uid 4300 --create-home --shell /bin/sh --groups portunus-push owner \
    && usermod --password '*' owner \
    && mkdir -p /run/sshd /home/owner/.ssh /opt/portunus

COPY sshd.conf /etc/ssh/sshd_config.d/check-push.conf
COPY sample.json /etc/portunus/sample.json
COPY swaps /opt/portunus/swaps
COPY start.sh /opt/portunus/start.sh
COPY portunus-agent /usr/local/bin/portunus-agent
COPY agent/ /opt/portunus/agent/
COPY authorized_keys /home/owner/.ssh/authorized_keys

RUN chmod 0755 /etc/portunus /opt/portunus /usr/local/bin/portunus-agent \
    && chmod 0644 /etc/portunus/sample.json /opt/portunus/swaps /etc/ssh/sshd_config.d/check-push.conf \
    && chmod -R a+rX,go-w /opt/portunus/agent \
    && chown -R owner:owner /home/owner/.ssh \
    && chmod 0700 /home/owner/.ssh \
    && chmod 0600 /home/owner/.ssh/authorized_keys

EXPOSE 22
ENTRYPOINT ["/bin/sh", "/opt/portunus/start.sh"]
