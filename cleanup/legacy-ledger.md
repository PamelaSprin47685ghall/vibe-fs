# Compatibility Ledger — temporary cleanup workbench

Policy: Name the creditor. Name the exit. Or delete the debt.

`UNKNOWN` never means keep. Every survivor is either deleted after first-party
migration or bounded to a named external/durable/deployment creditor.

| ID | Surface | Current creditor | Evidence | Writer | Verdict | Exit condition | Owner |
|---|---|---|---|---|---|---|---|
| LEGACY-001 | `ManagerActivation` vocabulary | none | repository search: zero production callers | no | DELETE | immediate; implementation/docs removed | mission-manager |
| LEGACY-002 | `RunCompletion.AgentId` field | none | repository search: field absent; canonical Map/AgentName callers | no | DELETE | immediate; no type/codec/fixture occurrence | execution |
| LEGACY-003 | `JoinResultRenderer.renderCompletedBatch` | none | repository search: zero callers after canonical `renderJoinItemBatch` migration | no | DELETE | immediate; renderer/support/tests removed | delegation |
| LEGACY-004 | `JoinItem.ofRunCompletion` | Host.Join canonical projection | Host.Join `ofAgentRunCompletion` call sites | no legacy writer | BOUNDED-COMPAT | delete when Host.Join consumes canonical JoinItem input | delegation |
| LEGACY-005 | FactCodec historical decoders | historical durable journal bytes | per-migration inventory required | decode only | BOUNDED-COMPAT | retention horizon/sample inventory proves no old bytes | persistence |
| LEGACY-006 | Host V1 TodoTable sink | OpenCode Host V1 contract | current Host V1 projection contract | no canonical writer | BOUNDED-COMPAT | Host V1 TodoTable removed from supported host contract | host-boundary |
| LEGACY-007 | `false abort` runtime migration | historical retired records | durable sample inventory required | no new writer | INVESTIGATE | convert to decode-only/refusal or delete after evidence | execution |
| LEGACY-008 | `js-boundary-baseline.json` | none | terminal goal is absolute zero | no | DELETE | scanner debt reaches zero | js-surface |
| LEGACY-009 | `verification-system/tests/support/domain.mjs` | none as a test contract | repository search: zero semantic-zone imports; all callers use production owner surfaces | no | DELETE | facade and family adapters deleted after zero-consumer proof | semantic-owners |

The ledger is not a permanent architecture document. Delete it after all
bounded survivors have named creditors and exit conditions, and after all
unowned rows are removed.
