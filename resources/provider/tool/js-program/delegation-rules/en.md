Every delegation member returns **data**, never rendered text. The harness renders your value for you; return values stay JSON.

```
{ ok: true, ... }          success
{ ok: false, code: "..." } refusal (stable kebab code)
```

## fork

```js
await js.fork("index the parser module");                  // charge only
await js.fork({ charge: "index the parser", name: "parser-index", calling: "engineer" });
```

- Payload: string (charge) or `{ charge, name?, calling?, keywords? }`. A missing `name` is derived from the charge.
- Result: `{ ok: true, name, agentId, outcome }` with `outcome` `created` | `nudged` | `commissioned`.
- Refusal codes: `name-required`, `charge-required`, `unknown-calling`, `calling-conflict`, `name-already-belongs`, `warm-start-unavailable`, `context-unavailable`, `not-a-delegation-role`, `placement-failed`, `dispatch-uncertain`.

## join

```js
const { records, byName } = await js.join();
```

- Result: `{ ok: true, records: [{ name, agentId, role, status, workRecord }], byName: { [name]: record } }`.
- `status` is `completed` | `undelivered` | `failed` | `abandoned`; `workRecord` is the child's terminal record delivered via `js.deliver` (empty when `undelivered`, `failed`, or `abandoned`).
- Membership-scoped: it collects the children this session created. Refusal codes: `context-unavailable`, `not-ready`, `nothing-to-join`, `join-in-progress`, `interrupted`, `abandoned`.

## fission

```js
const group = await js.fission({ charge: "explore three designs", lanes: 3 });
const { records } = await js.join();          // collects the lanes of this group
```

- Same identity, several lanes: each lane is this participant over one slice of the charge, and the host converges them.
- Result: `{ ok: true, groupId, lanes: [{ index, sessionId }] }`.
- Refusal codes: `fission-unavailable`, `lane-start-failed`, `not-ready`, `context-unavailable`, `invalid-prompt`.

## dag

```js
const outcome = await js.dag({ nodes: [
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
