# MakaMek relay hub reverse proxy.
# Access logging is intentionally disabled: request URLs carry the relay ticket
# and API key, and nothing must ever be logged at any layer (credential redaction).
# CORS (rendered only when allowedOrigins is configured): preflight (OPTIONS)
# requests from allowed origins are answered directly by Caddy with 204 so they
# never reach the hub (which would 405). Actual responses get
# Access-Control-Allow-Origin echoed only when the request Origin matches the
# allowlist.
__DOMAIN__ {
# __CORS_BEGIN__
    # The allowlist is rendered as an anchored regex; the Caddyfile `header`
    # matcher cannot express an OR-list of origins in a single token.
    @allowed_origin header_regexp Origin `__ALLOWED_ORIGINS_REGEX__`

    @cors_preflight {
        method OPTIONS
        header_regexp Origin `__ALLOWED_ORIGINS_REGEX__`
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
# __CORS_END__

    handle {
        reverse_proxy hub:8080
    }

    log {
        output discard
    }
}
