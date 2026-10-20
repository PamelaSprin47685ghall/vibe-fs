Every delegation member returns **data**, never rendered text. The harness renders your value for you; return values stay JSON.

```
{ ok: true, ... }          success
{ ok: false, code: "..." } refusal (stable kebab code)
```

## fork

```js
await this.fork("index the parser module");                  // charge only
await this.fork({ charge: "index the parser", name: "parser-index", calling: "engineer" });
```

- Payload: string (charge) or `{ charge, name?, calling?, keywords? }`. A missing `name` is derived from the charge.
- Result: `{ ok: true, name, agentId, outcome }` with `outcome` `created` | `nudged` | `commissioned`.
- Refusal codes: `name-required`, `charge-required`, `unknown-calling`, `calling-conflict`, `name-already-belongs`, `warm-start-unavailable`, `context-unavailable`, `not-a-delegation-role`, `placement-failed`, `dispatch-uncertain`.

## join

```js
const { records, byName } = await this.join();
```

- Result: `{ ok: true, records: [{ name, agentId, role, status, view }], byName: { [name]: record } }`.
- `status` is `completed` | `failed` | `abandoned`; a child only enters the records after it ends with `this.deliver(view)` — a child that has not delivered is not settled: it stays active, the join does not close, and you may keep waiting or re-delegate. `view` is the child's delivered TOML view from `this.deliver` (empty when `failed` or `abandoned`).
- Membership-scoped: it collects the children this session created. Refusal codes: `context-unavailable`, `not-ready`, `nothing-to-join`, `join-in-progress`, `interrupted`, `abandoned`.

## fission

```js
const group = await this.fission({ charge: "explore three designs", lanes: 3 });
const { records } = await this.join();          // collects the lanes of this group
```

- Same identity, several lanes: each lane is this participant over one slice of the charge, and the host converges them.
- Result: `{ ok: true, groupId, lanes: [{ index, sessionId }] }`.
- Refusal codes: `fission-unavailable`, `lane-start-failed`, `not-ready`, `context-unavailable`, `invalid-prompt`.

## dag

```js
const outcome = await this.dag({ nodes: [
  { id: "spec",  charge: "write the interface spec" },
  { id: "impl",  charge: "implement it", deps: ["spec"] },
  { id: "probe", charge: "probe two designs", deps: ["spec"], lanes: 2 },
] });
```

- Nodes run in dependency waves: every ready node forks, then one `join` collects the wave; a dependent node's prompt carries its dependencies' records.
- Result: `{ ok: true, results: { [id]: record }, skipped: [id...] }`.
- **A node that fails parks the program** — the same recovery channel as `where`. The parked payload is `{ dag, node, code, deps, done, pending }`; the injected value decides:
  - `"retry"` — run the node again;
  - `"skip"` — mark it skipped (dependents see `null` for it);
  - any other value — use it as that node's result and continue;
  - `{ abort: reason }` — fail the program with that reason.

## horizon

```js
const { agents, ptys } = await this.horizon();
```

- Who remains at your horizon, as data: `{ ok: true, agents: [{ label, status, target, workState, record, turnsUsed, turnsLeft, timeUsedMs, timeLeftMs }], ptys: [{ terminal }] }`.
- `status` is `returned` | `still-away` | `did-not-return`; `workState` is `latest` | `none` | `unavailable`, and `record` carries the latest work text when it is `latest`.
- The four calibration numbers say what this participant has spent and what it will likely need: turns used and left on its delegator's estimate, wall-clock used, and the time still to come at the pace it has shown. All four are `0` whenever either source is missing.
- Refusal: `{ ok: false, code: "horizon-unavailable" }`.

## advisor

```js
const advice = await this.advisor("Is the caching layer's eviction policy sound?", { prefixUntil: 12, thenMap: {} });
```

- Consult this session's **dedicated advisor** — a reviewer holding no tools that answers in prose — before committing to an approach or declaring work done. The same session always reaches the same advisor, and later calls append only the view delta plus the new intent, so it keeps the thread.
- `intent` is a separate question prompt. `view` uses the shared declaration `{ prefixUntil, thenMap }`, with both fields required. Generate the full view from the current live intervals first, then take the delta against host-owned delivery facts. The first consult receives the complete view; prefixUntil is not a delivery boundary. thenMap must cover every visible anchor outside the protected prefix: null keeps, a summary replaces the whole interval, and missing keys default to null. View and intent remain separate prompts.
- Result: `{ ok: true, advice }` (prose as data) or `{ ok: false, code: "advisor-unavailable", reason }`; a malformed view declaration refuses with `VIEW_DECLARATION_INVALID`.
- Treat the answer as guidance: a plan, a correction, or a stop signal. The consult itself writes nothing into your transcript.
