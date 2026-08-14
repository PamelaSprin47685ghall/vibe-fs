# WHY —— 为什么 interaction-authority 必须独立存在

## 一句话

物理 `role=user` 廉价且可伪造；若不把 authority 收成 typed 来源，continuation 与 repair 会自我抬升
为 HumanRoot，Fallback/Review 预算被反复重置（`docs/why/prompt.md` §1）。

## 不可替代性：为什么别的包解释不了

`dispatch-protocol` 回答「已授权 interaction 怎么穿过不可靠 Host 而不重复」，它**预设** authority 已存在。
若把「谁有资格开新 Logical Run」塞进 dispatch，dispatch 每次重写发送机制时都会顺带改变谁能当 Root——
两个问题可以独立变化（boundary card INDEPENDENT CHANGE：新增一种合法 continuation provenance 而
物理 dispatch 协议完全不变）。`participant-identity` 回答「换执行者不等于换人」，它消费已成立的
authority 事实，不裁决「这条消息凭什么成为新 root」。

本包独占的不可替代事实：

```text
一条物理 user 消息 ≠ 一个 authority turn。
只有 typed provenance 能创建（Root）或继续（Continuation）logical interaction。
UnknownOrigin fail closed。
```

## 历史上 RED 长什么样（失败模式考古）

1. **transport receipt 冒充消息身份**：旧测试曾断言 `accepted-*` 收据能携带 authority。
   `PROMPT-005` 后禁止：`accepted-*` 只是 Host 调用返回的收据，不是物理 `msg_*`。
   （`tests/unit/prompt/authority.test.mjs` 头部注释记录了这两条被删除的旧断言及其反向重建。）
2. **prompt 丢失 PromptKey 后凭 ExplicitAgent 抬权**：ActiveLogicalRun 存在时，仅凭 `ExplicitAgent`
   把 UnknownOrigin 提升为 HumanRoot 会重置当前 Logical Run 与 Fallback cursor。`corrective.md`
   裁决：mid-run 用户消息可以唤醒 join（低权限 pulse），但**不** AcceptHumanRoot、不 reset LogicalRun、
   不新建 Manager Life——PROMPT-004 的 fail-closed 规则保持不动。
3. **assistance 被当 fallback 失败**：`[NEEDHELP]` abort 若计入 ProviderFailure / LoopKill，
   FallbackCursor 与 retry budget 被误推进。`increase-strength.md` §6/§14 裁决：assistance abort 的
   owner 是 assistance，不得推进 fallback（HOST-027 的 interaction-authority 半边）。
4. **idle 续推无资格**：`cache.md` §14-15：`SessionIdle` 只证明 t0 时刻 idle，不证明 t6 发送时刻仍
   idle；idle 派生的 continuation（ManagerIdleEncouragement、idle 触发的 interaction-repair）必须持有
   fresh 资格。资格机制归 `causal-wait`（QuiescencePermit），但「idle 续推仍是 continuation、
   不得重置 authority、同一 occasion 只 claim 一次」归本包。

## 独立变化测试（INDEPENDENT CHANGE）

新增一种合法 continuation provenance（如新的 Guard 类），而物理 dispatch 协议、claim 四阶段、
PromptKey 组成全部不动 → authority 包单独变化，dispatch 包零变化。反向亦然（boundary card §10）。

## 边界（DOES NOT OWN）

| 看似邻近的事实 | 真正 owner |
|---|---|
| transport claim/submission/physical acceptance 协议 | `dispatch-protocol` |
| `Model=None` 发送海关（Root 不得选 model） | `dispatch-protocol` |
| Persona freeze、ExecutionBinding ≠ 换人 | `participant-identity` |
| Companion 关联 | `session-ontology` |
| provider projection / attempt recovery | `provider-projection` / `provider-attempt-recovery` |
| `AttemptExecutionProfile` 的当前 record 字段集 | HOW（本包 HOW.md 历史与弃权节） |
