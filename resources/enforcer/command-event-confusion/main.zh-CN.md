# command-event-confusion — Main

把 request 与 occurrence 拆成不同类型、不同 owner、不同 handler。

Command 进入 current policy：校验 identity/authorization/current state，可能返回 typed rejection；只有成功决定后，才 append 描述**真正发生了什么**的 event。Event apply/replay 只负责 deterministic reconstruction，不再调用今天的 permission/business decision。

健康形状：

```text
PlaceOrder command
      ↓ current policy
Rejected | [OrderPlaced event]
                    ↓
              durable history
                    ↓ replay
              reconstructed state
```

如果 command 本身需要 durable queue/inbox，明确它仍是 pending intention；处理完成后记录 accepted/rejected/outcome。不要因为“已经写磁盘”就把 request 叫 event。

常见假修复：

- 一个 shared message 加 `validated=true` flag；
- replay failure 后 catch 并 skip 旧 event；
- event apply 里重新查 today authorization；
- policy 变化后 migration 直接删除“现在看来不合法”的历史 event；
- command payload 与 event payload 完全复用，导致 event 没有 actual generated identity/time/result；
- projection 失败就认为 event 本身需要再次审批。

验证必须做 policy-change replay：先用 policy V1 产生 event stream，再切到 V2。重放历史得到的 past state 应由 events 决定，不因今天 policy 改变而改写。V2 只影响**新的 commands**能否产生新的 events。

再测试 invalid command：它必须在 event emission 前失败，history 里不能出现一个“后来才被判 invalid”的 occurrence。

如果历史确实需要 reinterpretation（例如正式 semantic migration），应通过新 version/migration/correction protocol 明确发生，而不是让普通 replay 偷偷重新判案。

完成时 type/name/API 就能区分两类东西：一个是现在仍可拒绝的意图，一个是未来必须承认的事实；两边不会因“字段长得一样”共享 epistemic status。

> Command asks the world to change. Event testifies that it changed. 把请求写成证词，或把证词重新当请求，都会让历史失去可信度。