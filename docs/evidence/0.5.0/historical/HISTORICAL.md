# Historical release evidence (invalid as of 2026-08-02)

These artifacts were captured during the `0.5.0-rc.1` packaging attempt (commit
`e3f3f7aa`) and are **historical only**. They do NOT describe the current
source tree and must NOT be cited as evidence that the current code or package
is releasable:

| file | what it recorded | why it is invalid now |
|------|------------------|------------------------|
| `npm-test.txt` | `npm test` output | Ran `test:compile`, `test:next`, `test:manager-tools` and `tests-next` — all deleted in the migration (current entry: `tests-mjs` / `test:unit`). |
| `install-out.txt` | `npm install <tarball>` | Failed with ENOENT: the tarball path under `docs/evidence/0.5.0/` did not exist. |
| `import-out.txt` | `node -e "require('wanxiangshu')"` | Failed with MODULE_NOT_FOUND (`./node_modules/wanxiangshu/package.json`). |
| `npm-pack.txt` | `npm pack` listing | Shows `build/tests-next/**` and other artifacts from a stale `build/` — that directory tree no longer exists. |
| `TARBALL.sha256` | sha256 of `wanxiangshu-0.5.0-rc.1.tgz` | The tarball it references was never produced at that path. |

The current release pipeline (as of `78f644cb`) is verified by:

- `gate:static` — seven gates (ssot-lint, architecture-gate, strip-doc-bold,
  toml-format, budget-gate, surface-inventory, generated-artifact sync).
- `npm run build` — cleans the whole `build/` directory first (no stale
  artifacts can enter the package), then Fable + postbuild.
- `npm pack --dry-run` — tarball contains `build/next/**` + `build/testkit/**`
  (postbuild-copied, by design) and **no** `tests-next` or other deleted tree.

Regenerate evidence against the CURRENT pipeline when a release is actually
attempted; do not copy these files forward.
