# MakaMek relay hub reverse proxy.
# Access logging is intentionally disabled: request URLs carry the relay ticket
# and API key, and nothing must ever be logged at any layer (credential redaction).
# CORS: preflight (OPTIONS) requests from allowed origins are answered
# directly by Caddy with 204 so they never reach the hub (which would 405).
# Actual responses get Access-Control-Allow-Origin echoed only when the
# request Origin matches the allowlist.
__DOMAIN__ {
    @allowed_origin header Origin __ALLOWED_ORIGINS__

    @cors_preflight {
        method OPTIONS
        header Origin __ALLOWED_ORIGINS__
    }

    handle @cors_preflight {
        header Access-Control-Allow-Origin "{header.Origin}"
        header Access-Control-Allow-Methods "GET, POST, DELETE, OPTIONS"
        header Access-Control-Allow-Headers "Content-Type, x-api-key"
        header Access-Control-Max-Age "86400"
        header Vary "Origin"
        respond 204
    }

    handle @allowed_origin {
        header Access-Control-Allow-Origin "{header.Origin}"
        header Vary "Origin"
        reverse_proxy hub:8080
    }

    handle {
        reverse_proxy hub:8080
    }

    log {
        output discard
    }
}
