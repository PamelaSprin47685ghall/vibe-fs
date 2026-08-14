# HOW：intra-participant-parallelism 实现模型（非 normative）

## 1. Domain spine

建议 production 只持有少量不可非法组合的类型：canonical prompt parser；`FissionGroupId`；lane index/count；`FissionWorkBundle` keyed union；`CompletionAffinity = PreFissionBroadcast | Lane k`；active-group projection。禁止用一组 `isFissioned/isLastLane/hasHandoff` bool 模拟状态机。

## 2. V1 physical replacement

Fission tool 在 old caller tool context 中：

1. 解析 prompts；解析失败立即返回，无副作用。
2. 从 Authority/Profile 取 current managed agent/role；从 `sessionParents` 取 old physical parent；从 canonical LWR port 取 old caller record。
3. 预留一个 process-local admission slot，防同 owner 两次并发 fission。
4. 对每 lane 创建 fresh Host session，创建参数里的 `parentID` 使用 **old caller 的 parentID**，而不是 old caller id。root caller 则 parentID absent。
5. lane session 继承 old caller selected managed agent、directory、provider language；首 prompt = canonical LWR envelope + exact lane input + lane index/count guidance。不要用 Host session fork。
6. 所有 lane 都完成 create + subscribe/bind + prompt admission 后，commit group；失败则 abort/delete 已建 lanes、release slot、old caller 继续。
7. commit 后给 old caller 写 Fission-owned interrupt mark，再调用 physical abort。Ordinary abort workflow 先消费此 mark：不 terminal、不 cascade children/PTY、不 provider recovery。

## 3. Logical-owner alias

lane physical session 不是新 participant。运行期应维护 `laneSessionId -> oldLogicalOwnerSessionId` alias。需要 logical owner state 的工具 runtime（尤其 shared child registry / handle set）按 alias 找 owner runtime；provider horizon 不暴露 alias、lane session id 或 group id。

Host `parentID` 与 logical owner alias 是两件不同事实：前者只保持 sibling physical topology，后者保持 same-participant semantics。

## 4. Existing external work

Admission snapshot owner 当前 outstanding child runs 与 PTYs，登记为 `PreFissionBroadcast` completion sources。source terminal 后生成一个 canonical completion fact，并为 group 的每个 lane 维护 delivery bit/key。lane 下一安全 provider boundary materialize inbox；已关闭 lane 的 undelivered broadcast 留在 group forwarding closure，不丢弃。

Admission 后新 run/PTY 在创建点记录 current lane affinity。join/drain 在 lane context 中只消费 `PreFissionBroadcast` 中尚未投递给本 lane的 completion，加上 affinity == current lane 的 completions。

## 5. Work ring / convergence

lane own LWR 只登记一次为 `index -> canonical ref/digest`。ring successor 是运输策略，closed successor 由 pure forwarding closure 继续向后找 active lane；无 active lane 时 group finalizer 持有 bundle。最终按 lane index 稳定排序物化 aggregate context；最后可继续的 lane 消费完整 handoff 后，其 ordinary terminal 作为 logical owner terminal candidate。

## 6. Durability

Fission-specific durable facts建议最小化为 admission、lane materialized/closed、completion source/delivery、bundle contribution 与 converged/failed。事实存统一 durable substrate；physical subscriptions、abort callbacks、locks 是可重建资源，不持久化。恢复先 fold group，再 reconcile physical sessions；无法证明 alias/membership 时 fail closed。

## 7. 当前 vocabulary 映射

历史 Proposal 中的 `Meditator` 已不在当前 Role vocabulary；其现行 reasoning office 是 `Inquiry`。因此 V1 entitlement 使用 `Role.Inquiry`。历史 `Executor` 也不是当前 Role case；hidden execution helper不获得 fission office consequence。

## 历史与弃权

- 不采用 OpenCode `session.fork`：它会把 lane 塞进错误的 Host parent/child topology，而且把 transcript clone 机制与业务 identity 耦合。
- 不采用同一 physical SessionId 多 provider stream。
- 不采用 per-lane child registry。
- 不采用 lane raw transcript summary；LWR owner 保持唯一。
- 不把 Prompt Refresh 整体塞进本包；认知/affordance 文案仍由相应 package owner 承担。
