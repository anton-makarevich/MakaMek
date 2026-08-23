# MakaMek relay hub reverse proxy.
# Access logging is intentionally disabled: request URLs carry the relay ticket
# and API key, and nothing must ever be logged at any layer (credential redaction).
__DOMAIN__ {
    reverse_proxy hub:8080

    log {
        output discard
    }
}
