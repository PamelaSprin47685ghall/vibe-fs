# omp adapter skeleton (HNI-03)

Thin adapter from omp 18.12.4's extension API to the host-neutral ports. Not shipped; it proves the ports are
implementable on a second host and keeps the TS mirror honest.

- `ports.ts` — TS mirror of `src/Wanxiangshu/Host/Ports/*.fsi` (checked by `mirror-check.mjs`).
- `adapter.ts` — `createOmpHostPorts` (omp → `HostPorts`), `registerProductHooks` (`ProductHooks` → `pi.on`/`registerTool`), `activateOnOmp`.
- `sdk-children.ts` — child sessions through the omp SDK (`createAgentSession`, `SessionManager`).
- `fixtures/` — offline events and a fake `ExtensionAPI`.

Run: `./check.sh` (needs the omp-task vibe install for types, TypeScript at `$TSC`, `bun`). No omp process, no model calls.
