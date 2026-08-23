services:
  hub:
    image: __HUB_IMAGE__
    container_name: makamek-hub
    restart: unless-stopped
    env_file: .env
    environment:
      # Caddy proxies from the compose bridge network; trust RFC1918 docker ranges
      # so ForwardedHeaders pick up real client IPs for rate limiting.
      - Hub__TrustedProxies=172.16.0.0/12
    expose:
      - "8080"

  caddy:
    image: caddy:2-alpine
    container_name: makamek-caddy
    restart: unless-stopped
    depends_on:
      - hub
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config

volumes:
  caddy_data:
  caddy_config:
