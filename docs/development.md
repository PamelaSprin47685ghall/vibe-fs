# Development

## Setup

```bash
npm ci
dotnet tool restore
npm run build
npm test
```

Node ≥ 20. Local tools (`fable`, `fantomas`) come from `.config/dotnet-tools.json`.
`package-lock.json` is committed; use `npm ci`, not ad-hoc `npm install`, for a reproducible tree.

`bun-pty` is pinned via `overrides` so the installed version matches the direct dependency (npm peer/override behavior).

## Repository layout

```text
src/Wanxiangshu/   production F# (sole source root)
resources/         packaged runtime assets (prompts, enforcer catalog)
spec/              binding product contract
docs/              architecture, development, release, decisions, RFCs
tests/unit/        pure / contract tests against dist
tests/integration/ resources, journal, plugin, package, harness
tests/e2e/         scenarios (TOML) + cases
scripts/           build.mjs, check.mjs, focused checks
dist/              Fable output (not committed)
artifacts/         local build/package output (not committed)
```

## Daily workflow

```text
读条款 → 读代码 → 改动 → format:check / check → 最小测试 → 扩大范围
```

- Production source lives only under `src/Wanxiangshu/`.
- Unit tests import `dist/` through `tests/unit/support/domain.mjs`; do not assert Fable private names outside that facade.
- Format F# with `npm run format` before push when sources change.

## Commands

| Command | Use |
|---------|-----|
| `npm run build` | Fable → `dist/` (`scripts/build.mjs`) |
| `npm run format` | fantomas write on `src/Wanxiangshu` |
| `npm run format:check` | fantomas check only |
| `npm run lint` | `format:check` + `scripts/check.mjs` (spec + architecture) |
| `npm test` / `npm run test:unit` | unit (`tests/unit/run.mjs`) |
| `npm run test:integration` | integration (`tests/integration/run.mjs`) |
| `npm run test:package` | package install/import surface |
| `npm run test:e2e` | e2e (`tests/e2e/run.mjs`; pass `--repeat N`) |
| `npm run check` | lint → build → unit → integration |
| `npm run check:release` | check → e2e `--repeat 3` → package → `npm pack --dry-run` |

## Tests

Three layers, separate runners:

| Layer | Entry | Scope |
|-------|-------|--------|
| unit | `tests/unit/run.mjs` | Domain and application contracts against `dist/` |
| integration | `tests/integration/run.mjs` | resources, journal boot, plugin contract, package tarball, harness |
| e2e | `tests/e2e/run.mjs` | `scenarios/` (TOML) + `cases/`; host-backed flows |

Package checks can also run alone via `npm run test:package`.
E2E multi-round: `npm run test:e2e -- --repeat 3`.

## Specifications and RFCs

- `spec/` is the binding product contract. Clause IDs are stable addresses. `spec/00.md` navigates active specs; `spec/99.md` is the glossary.
- `docs/rfcs/` holds non-binding future designs (e.g. strength, student-teacher).
- `docs/decisions/` records accepted decisions (e.g. enforcer catalog, Kolmogorov rules).
- `spec/` does not record implementation status, owners, or work progress. Tests name clauses directly.

## Runtime resources

Shipped under `resources/` and included in the npm tarball:

- `resources/prompts/*-system.md` — ten role system prompts (blogger, browser, coder, devops, executor, inspector, manager, meditator, orchestrator, reviewer)
- `resources/enforcer/catalog.json` — enforcer rule catalog

Load path: `Infrastructure/Resources/` (`PackageResources`, `PromptResources`, `EnforcerCatalogResource`, `RuntimeResources`). Plugin init calls `RuntimeResources.load` / `install` once; missing or invalid resources fail fast at startup. Paths resolve from the package layout (`dist/` → `resources/`), not from `process.cwd()`.

## Common failures

1. **Stale `dist/`** — unit tests refuse a stale build. Run `npm run build` after F# changes.
2. **Missing local tools** — `dotnet tool restore` needs `.config/dotnet-tools.json`. Without it, `fable` / `fantomas` are unavailable.
3. **Lockfile drift** — use `npm ci`. A tree without `package-lock.json` or with a mismatched lock fails CI and local reproducibility.
4. **Resource load failure** — empty/invalid enforcer catalog or missing prompt files abort plugin startup. Fix files under `resources/`; do not invent code-side fallbacks.
5. **Working directory ≠ package root** — resource paths are package-relative. Run tools from the repository (or installed package) root, not an arbitrary cwd.
