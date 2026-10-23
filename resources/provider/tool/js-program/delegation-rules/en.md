Delegation members return data. The host formats results as TOML.

```js
{ ok: true, ... }           // success
{ ok: false, code: "..." }  // refusal
```

**Do not confuse slots.**

| Slot | Where | Required? | Purpose |
|------|--------|-----------|---------|
| `prefixUntil` + `carry` | **Tool top-level** every `todowrite-for-*` call | both required | Layout of **this session's next request** |
| `extra` | Last argument of `fork` / `resume` / `commission` / `devops` / `dag` / `advisor` / `deliver` | optional, default `[]` | Background sent to the **child** (or into delivery), not a history rewrite of the parent |

The last argument of `fork`, `resume`, `commission`, `devops`, `dag`, and `advisor` is an optional `extra` array, defaulting to `[]`. An integer copies a visible anchor's full history interval; a string supplies literal text. Items are sent in array order. Include only the background the recipient needs.

**The parent's current turn is not sent to the child.** State the task once in `charge` or `intent` as ordinary prose; do not repeat it in `extra`. The message is prose, not structured fields — say what the work suggests, plain and clear. There is no member-level prefix parameter. The recipient receives material as readable TOML tables. An invalid array or an invisible anchor returns `VIEW_DECLARATION_INVALID` before any work is created. `join`, `horizon`, and `fission` take no `extra`.

## fork

```js
await this.fork("index the parser module");
await this.fork({ charge: "index the parser", name: "parser-index", calling: "engineer" }, [1, "List public entries only"]);
```

- Payload: a charge string, or `{ charge, name?, calling? }` where `name` and `calling` are routing metadata and `charge` carries the message as prose. A missing `name` is derived from the charge. No content-shaped fields.
- Result: `{ ok: true, name, agentId, outcome }`, with `outcome` `created` | `nudged` | `commissioned`.
- Refusal codes: `name-required`, `charge-required`, `unknown-calling`, `calling-conflict`, `name-already-belongs`, `warm-start-unavailable`, `context-unavailable`, `not-a-delegation-role`, `placement-failed`, `dispatch-uncertain`.

## resume

```js
const resumed = await this.resume({ name: "audit-road", charge: "continue the audit" }, [4, "Parser entries have been checked"]);
// or omit extra:
await this.resume({ name: "audit-road", charge: "continue the audit" });
```

- Payload `{ charge, name }` gives the next task to an existing worker. `name` is required. `calling` is not accepted because this call continues existing work.
- Result: `{ ok: true, name, agentId, outcome }`.

## commission

```js
await this.commission({ charge: "audit the parser independently", name: "parser-audit" }, [1]);
```

- Entrust an independent purpose to a Manager road. Payload is a charge string or `{ charge, name?, calling? }`, where `name` and `calling` are routing metadata and `charge` carries the message as prose. A missing `name` comes from the charge. The role determines an omitted `calling`; an explicit value that disagrees is refused with `unknown-calling`.
- Result: `{ ok: true, name, agentId, outcome }`.

## devops

```js
const answer = await this.devops("find the flaky test", [1]);
```

- Consult this task's bound DevOps and wait for the investigation result. There is no separate child to `join`. State the question in `intent`.
- Result: `{ ok, kind, status, returned: [{ text }] }`. An unbound or closed seat refuses with `assessment-pending-for-devops`.

## join

```js
const { records } = await this.join();
```

- Result: `{ ok: true, records: [{ name, agentId, role, status, returned: [{ text }] }] }`.
- Collects children created by this session. `status` is `completed` | `failed` | `abandoned` | `voided`. Completion requires both valid delivery and a proven terminal. A child that can still deliver remains active and keeps `join` waiting. A child whose delivery has become impossible is settled and reported once as `voided`. Read each record's `returned` items in order; they transport the child's prose report verbatim — there is no report schema and no mandated sections. Other terminal states return an empty array.
- Refusal codes: `context-unavailable`, `not-ready`, `nothing-to-join`, `join-in-progress`, `interrupted`, `abandoned`.

## fission

```js
const group = await this.fission({ charge: "explore three designs", lanes: 3 });
const { records } = await this.join();
```

- Several lanes share one identity. Each lane takes part of the same charge; the host collects them.
- Result: `{ ok: true, groupId, lanes: [{ index, sessionId }] }`.
- Refusal codes: `fission-unavailable`, `lane-start-failed`, `not-ready`, `context-unavailable`, `invalid-prompt`.

## dag

```js
const outcome = await this.dag({ nodes: [
  { id: "spec", charge: "write the interface spec" },
  { id: "impl", charge: "implement it", deps: ["spec"] },
  { id: "probe", charge: "probe two designs", deps: ["spec"], lanes: 2 },
] }, [1, "The attached record lists current parser entries"]);
```

- Nodes run in dependency waves. Ready nodes `fork`, then one `join` collects the wave. Dependents receive their prerequisite nodes' records.
- All nodes receive the same `extra`; a node cannot override it.
- Result: `{ ok: true, results: { [id]: record }, skipped: [id...] }`.
- A failed node calls `park` with `{ dag, node, code, deps, done, pending }`. Inject `"retry"` to repeat it, `"skip"` to skip it (dependents see `null`), `{ abort: reason }` to fail the program, or another value to replace that node's result.

## horizon

```js
const { agents, ptys } = await this.horizon();
```

- Result: `{ ok: true, agents: [{ label, status, target, workState, record, turnsUsed, turnsLeft, timeUsedMs, timeLeftMs }], ptys: [{ terminal }] }`.
- `status` is `returned` | `still-away` | `did-not-return`. `workState` is `latest` | `none` | `unavailable`; `record` contains the latest work text when it is `latest`.
- The four measures describe turns and time used, and the estimated turns and time remaining. All four are `0` if either source is missing.
- When unavailable, returns `{ ok: false, code: "horizon-unavailable" }`.

## advisor

```js
const advice = await this.advisor("Is the cache eviction policy sound?", [1, "The current policy is LRU"]);
```

- Consult this session's dedicated advisor. The advisor has no tools and answers in prose. State the question in `intent`; use `extra` for the history needed to judge it.
- The same session keeps the same advisor. The host appends material not delivered before and the new question; you need not track a delivery cursor.
- Result: `{ ok: true, advice }` or `{ ok: false, code: "advisor-unavailable", reason }`.
- Use the answer to inform the next decision. The consultation does not itself rewrite your transcript.
