Every delegation member returns **data**, never rendered text. The harness renders your value for you; return values stay JSON.

```
{ ok: true, ... }          success
{ ok: false, code: "..." } refusal (stable kebab code)
```

Every member that hands work to someone else — `fork`, `resume`, `commission`, `devops`, `dag` — carries a **report mapping** as its last argument: a `Record<string, null|string>` that maps an anchor number to a summary string or `null` (keep). The host reads it as `{ prefixUntil: 0, thenMap: reportMapping }`, so a `{ prefixUntil, thenMap }` wrapper is not accepted. The mapping is the context the host projects into the child; a missing or invalid mapping refuses with `{ ok: false, code: "VIEW_DECLARATION_INVALID" }`.

## fork

```js
await this.fork("index the parser module", { 1: null });                      // charge only
await this.fork({ charge: "index the parser", name: "parser-index", calling: "engineer" }, { 1: null, 2: "parser inventory" });
```

- Payload: string (charge) or `{ charge, name?, calling?, keywords? }`. A missing `name` is derived from the charge.
- `reportMapping` is required: `Record<string, null|string>` — anchor number → summary or `null`. The host reads it as `{ prefixUntil: 0, thenMap: reportMapping }`. A missing or invalid mapping refuses with `{ ok: false, code: "VIEW_DECLARATION_INVALID" }`.
- Result: `{ ok: true, name, agentId, outcome }` with `outcome` `created` | `nudged` | `commissioned`.
- Refusal codes: `name-required`, `charge-required`, `unknown-calling`, `calling-conflict`, `name-already-belongs`, `warm-start-unavailable`, `context-unavailable`, `not-a-delegation-role`, `placement-failed`, `dispatch-uncertain`.

## resume

```js
const resumed = await this.resume("continue the audit", { 1: null, 4: "audit findings so far" });
```

- Hand the next charge to a delegated worker that already exists. `resume` takes **no `calling`**: it continues the worker already in place instead of calling out to a role, so a payload carrying `calling` is refused.
- `reportMapping` is required: `Record<string, null|string>` — anchor number → summary or `null`. The host reads it as `{ prefixUntil: 0, thenMap: reportMapping }`. A missing or invalid mapping refuses with `{ ok: false, code: "VIEW_DECLARATION_INVALID" }`.
- Result: `{ ok: true, name, agentId, outcome }`.

## commission

```js
await this.commission({ charge: "write the interface spec", name: "spec" }, { 1: null });
```

- The orchestrator's own way to hand a charge to a worker: an orchestrator commissions the work instead of doing it. Payload is a string (charge) or `{ charge, name?, calling?, keywords? }`.
- `reportMapping` is required: `Record<string, null|string>` — anchor number → summary or `null`. The host reads it as `{ prefixUntil: 0, thenMap: reportMapping }`. A missing or invalid mapping refuses with `{ ok: false, code: "VIEW_DECLARATION_INVALID" }`.
- Result: `{ ok: true, name, agentId, outcome }`.

## devops

```js
const answer = await this.devops("find the flaky test", { 1: null });
```

- A DevOps consultation that runs synchronously inside this call: the host consults DevOps and hands the answer back right away. There is no child to join and nothing is left running.
- `reportMapping` is required: `Record<string, null|string>` — anchor number → summary or `null`. The host reads it as `{ prefixUntil: 0, thenMap: reportMapping }`. A missing or invalid mapping refuses with `{ ok: false, code: "VIEW_DECLARATION_INVALID" }`.
- Result: `{ ok: true, record }` — the investigation's record as data.

## join

```js
const { records, byName } = await this.join();
```

- Result: `{ ok: true, records: [{ name, agentId, role, status, view }], byName: { [name]: record } }`.
- `status` is `completed` | `failed` | `abandoned`; a child only enters the records after it ends with `this.deliver(reportMapping)` — a child that has not delivered is not settled: it stays active, the join does not close, and you may keep waiting or re-delegate. `view` is the child's delivered TOML view from `this.deliver` (empty when `failed` or `abandoned`).
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
] }, { 1: null, 2: "parser inventory" });
```

- Nodes run in dependency waves: every ready node forks, then one `join` collects the wave; a dependent node's prompt carries its dependencies' records.
- One `reportMapping` goes to every node: the same mapping is handed to all node workers, and a node cannot replace its own copy. It is required — `Record<string, null|string>`, anchor number → summary or `null`, read by the host as `{ prefixUntil: 0, thenMap: reportMapping }`. A missing or invalid mapping refuses with `{ ok: false, code: "VIEW_DECLARATION_INVALID" }`.
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
const advice = await this.advisor("Is the caching layer's eviction policy sound?", { 1: null });
```

- Consult this session's **dedicated advisor** — a reviewer holding no tools that answers in prose — before committing to an approach or declaring work done. The same session always reaches the same advisor, and later calls append only the new material plus the new intent, so it keeps the thread.
- `intent` is a separate question prompt. `reportMapping` is required and is a `Record<string, null|string>` — anchor number → summary or `null`; the host reads it as `{ prefixUntil: 0, thenMap: reportMapping }`, the shared view declaration with `prefixUntil` fixed at `0`. Generate the mapping from the current live intervals first, then take the delta against host-owned delivery facts. The first consult receives the complete material; the mapping is not a delivery boundary. The mapping must cover every visible anchor: `null` keeps, a summary replaces the whole interval, and missing keys default to null. Mapping and intent remain separate prompts.
- Result: `{ ok: true, advice }` (prose as data) or `{ ok: false, code: "advisor-unavailable", reason }`; a missing or invalid report mapping refuses with `VIEW_DECLARATION_INVALID`.
- Treat the answer as guidance: a plan, a correction, or a stop signal. The consult itself writes nothing into your transcript.
