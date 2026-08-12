# null-ambiguity — Main

## What To Do Now
不要再让下游从 `null`、`None`、空串、缺字段旁边的 flag 或 status 猜“为什么没有值”。在**仍然知道原因的生产边界**把会改变 caller 行为的缺席原因命名成闭合结果，例如 `Found | NotFound | Forbidden`。如果缺席确实只有一种业务含义，保留 `Option`；不要为了显得类型化而制造多余 case。

## Why This Matters
一旦几个不同事实都被压成“没有值”，信息就丢了。后续代码只能靠旁证复原：看 HTTP status、查另一个 boolean、解析 error string、根据时间猜是否尚未加载。于是原本生产者已经知道的一件事，被系统改造成每个 caller 都要重新推理的一道题。

真正的问题不是 `null` 这个字符，而是**不同原因要求不同动作，却共用一个表示**。`NotFound` 可能正常结束，`Forbidden` 必须拒绝，`NotLoaded` 可能等待，`Failed` 需要上报。若这些动作不同，它们就不是同一个 `None`。

## Repair Strategy
先列出所有“没有值”的原因，再列出每种原因下 caller 应做什么。只有行为不同的原因才需要独立 case。让 adapter 负责把 wire 上的 `404`、`null`、缺字段等翻译成 domain outcome；进入 domain 后不要继续携带“值 + status + flag”的拼图。

## Decision Branches
- 如果所有 caller 对缺席都采取同一动作，`Option` 足够，不要扩张类型。
- 如果不同缺席原因导致不同 retry / authorization / fallback / terminal 行为，返回闭合 result。
- 如果 wire 必须用 `null` 表示协议事实，在 ingress 立刻翻译；不要让 transport encoding 变成 domain semantics。
- 如果现状是 `value option + wasForbidden + errorCode`，优先把整个组合折成真正的 outcome，而不是继续加 flag。

## Common Wrong Fixes
- 在 nullable value 旁加 `wasUnauthorized`、`isLoaded`、`hadError`；这只是把一个模糊状态扩成更多可能互相矛盾的状态。
- 把 reason 塞进字符串让 caller `includes("forbidden")`；这会进一步落入 `stringly-typed-error`。
- 把所有 absence 都改成 exception；这样只是从“猜缺席原因”换成“猜异常类别”。
- 为只有一个含义的可选值造五个 case；类型数量不是信息质量。

## Verification
逐个 caller 看控制流：它应该只通过匹配**生产者返回的命名 outcome**决定动作，而不再结合 null-check、status、旁边的 flag 或 prose 去复原原因。做一个回归：改变 transport 的人类文本或编码细节，不应改变 domain 分支。

这里的 invariant 是：**在原因仍然已知的地方保留原因；需要不同动作的事实，不得在边界上先压扁再让下游猜。**

## Done When
所有会改变 caller 行为的 absence reason 都在边界上保持可区分；单纯 optional 的地方仍然保持简单；下游不再承担“为什么没有值”的考古工作。