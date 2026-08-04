# Spec

Binding product rules for Wanxiangshu. Clause IDs are the address system.

| Path | Role |
|------|------|
| `00.md` … `17.md`, `99.md` | Clause documents (active product contract) |
| `conformance.toml` | Per-clause machine ledger (status, owners, tests) |
| `conformance.md` | Generated table from the ledger (do not hand-edit) |
| `coverage.toml` | Transitional id → tests map derived from the ledger |

`docs/rfcs/` holds approved-but-undelivered designs (Strength, Student&Teacher, Enforcer nudge). Those are not product contract.

Status words (`CONFORMANT`, `PARTIAL`, …) belong only in the ledger, never in clause prose.
