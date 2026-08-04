# Development

## Setup

```bash
npm ci
dotnet tool restore
npm run build
npm test
```

## Workflow

```text
读条款 → 读状态 → 读代码 → 改动 → lint → 最小测试 → 扩大范围
```

- Spec: `spec/*.md` (clause IDs). Implementation status is not recorded in `spec/`.
- Production source: `src/Wanxiangshu/` only.
- Tests import `dist/`; never assert Fable private names outside `tests/unit/domain.mjs`.

## Commands

| Command | Use |
|---------|-----|
| `npm run build` | Fable → `dist/` |
| `npm test` | unit |
| `npm run test:harness` | harness |
| `npm run test:e2e` | canary (set `CANARY_REPEAT` for multi-round) |
| `npm run check` | static + build + unit + harness |
| `npm run lint` | format F# / XML before commit |

## Layout gate

Root files are allowlisted (`scripts/repository-layout-gate.mjs`). Do not drop sources at repo root.

## Further reading

- [`AGENTS.md`](../AGENTS.md)
- [`docs/decisions/kolmogorov.md`](decisions/kolmogorov.md)
- [`docs/architecture.md`](architecture.md)
