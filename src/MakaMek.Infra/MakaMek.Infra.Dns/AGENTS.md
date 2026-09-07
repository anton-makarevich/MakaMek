# MakaMek.Infra.Dns

Pulumi IaC stack that manages Cloudflare DNS records for the `makamek.nl` zone.

## Important: Record ordering in `Pulumi.prod.yaml`

The records list in `Pulumi.prod.yaml` is **positional** — each entry's array index is
used as its Pulumi resource name (`record-0`, `record-1`, …). This means:

- **Always append** new records at the **end** of the list.
- **Never insert** a record in the middle — doing so shifts every subsequent index,
  causing Pulumi to destroy and recreate resources that haven't actually changed
  (Cloudflare API rejects duplicates with error code `81058`).
- When removing a record, be aware that trailing entries may shift; verify that
  Pulumi state matches the intended resource names before running `pulumi up`.
