# Spec

Transitional layout for 0.5.3 repository normalization.

- **Authoritative clause text** still lives under `SSOT/` until the full SSOT → `spec/clauses/` rename.
- **Clause → test ownership** still lives in `STATUS/conformance.toml` (machine ledger) until replaced by `spec/coverage.toml`.
- This directory is the target home for both; do not add a second product contract here.

## Files

| Path | Role |
|------|------|
| `README.md` | This note |
| `coverage.toml` | Optional partial mapping; full truth remains `STATUS/conformance.toml` until rename |
