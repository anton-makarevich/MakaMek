#cloud-config
package_update: true

write_files:
  - path: /opt/makamek-hub/docker-compose.yml
    permissions: '0644'
    content: |
__DOCKER_COMPOSE__
  - path: /opt/makamek-hub/Caddyfile
    permissions: '0644'
    content: |
__CADDYFILE__
  - path: /opt/makamek-hub/.env
    permissions: '0600'
    content: |
      Hub__ApiKey=__HUB_API_KEY__

runcmd:
  # Docker Engine + compose plugin from the official repo (docker-compose-v2 is
  # not reliably available in the Ubuntu 22.04 archive).
  - install -m 0755 -d /etc/apt/keyrings
  - curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  - chmod a+r /etc/apt/keyrings/docker.asc
  - echo "deb [arch=arm64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu jammy stable" > /etc/apt/sources.list.d/docker.list
  - apt-get update
  - DEBIAN_FRONTEND=noninteractive apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  - systemctl enable --now docker
  - mkdir -p /opt/makamek-hub
  - cd /opt/makamek-hub && docker compose pull && docker compose up -d
