# CUT-25 sandbox verification (eng-cut25b-goose, 2026-10-21)

## (a) Escape routes in evidence/CUT-25 — regression vs f37a7ba48

Base: `f37a7ba48` dist at `/workspace/vfs-cut/wt/CUT-25g-scratch/dist-base` (pre-membrane `JsSandbox.js`, no `DONT_CONTEXTIFY`).
Candidate: current master after `dc3096154` (membrane via wanxiangli/sandbox 0.23.2).

Harness: `requirements/repository-programming/tests/006-cut25-escapes.test.mjs` (8 tests).

| Route (probe / test) | on f37a7ba48 dist-base | on current |
|---|---|---|
| global `api` constructor | FAIL (ok=true, reaches process) | PASS |
| global `__deadlineRef` | FAIL | PASS |
| `globalThis.constructor` | FAIL | PASS |
| returned array (`this.ls`) | FAIL | PASS |
| write outside via escape | FAIL (file written) | PASS (no file) |
| post-await sync infinite loop | FAIL (child non-zero / hang) | PASS (`PROGRAM_TIMEOUT`, ~204ms) |
| patched `WeakMap.prototype.has` | n/a on f37a7ba48 (no membrane WeakMap); RED on 0.23.1 — see `membrane-0.23.1-red-intrinsics.json` | PASS |
| patched `WeakMap.prototype.set` | same | PASS |

Raw probe JSON still in `base-red-escapes.json` / `fixed-green-escapes.json` / `wired-green-escapes.json`.

## (b) Await-loop / hard timeout

Verified: program `await this.cat(...); while (true) {}` under `deadlineMs=200` ends with `{ok:false, code:PROGRAM_TIMEOUT}` in ~204ms (child process; afterEvaluate driver). Library `SPEC[sandbox-002]` covers the same. Honest residual: node:vm is not a security boundary; isolation is a membrane + timeout driver, not a separate OS process.

## Landed this turn

- wanxiangli `bd67c08` + release `7b06b93` (0.23.2)
- host `eb172e465` board claim; `f6749e0a2` adopt 0.23.2 + WeakMap regressions; `dc3096154` delete dead `wrapProgram`
