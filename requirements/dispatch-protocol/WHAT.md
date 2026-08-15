# WHAT —— 唯一 normative 合同（dispatch-protocol）

> 当前世界必须同时成立的事实。每条命题的测试落点见 [`PROOF.md`](PROOF.md)（锚点 `R1`..`R11`）。

```text
术语：
  claim           = 一次 logical dispatch 的 durable 记录（PromptKey 定位）。
  receipt         = Host 调用返回的 transport 收据（accepted-* 形态）。
  physical 落地   = 真实 chat.message / 明确 msg_* 出现在 Host 上（唯一可证明 accepted 的证据）。
  logical effect  = 一次 logical send 在 Host/业务世界产生的后果。
```

## DISPATCH-PROTOCOL-001 — PromptDispatcher 是唯一写入口

所有插件产生的 user-shaped message（Guard、repair、ReviewConfirmation、busy nudge、provider
failure continuation、Orchestrator 冲突提示、SyncDelegate 首发与 idle nudge 等）必须经过同一个
`PromptDispatcher`；禁止第二 writer 直接 `prompt_async`（PROMPT-005 / 历史 shape/prompt 条款）。

- 含义：发出去的每一条内部消息都有 claim 记录，恢复才能凭 PromptKey 找到它。
- 证据：→ PROOF.md R5、R6。

## DISPATCH-PROTOCOL-002 — 四态 claim 生命周期

一次 dispatch 的持久阶段恰好是四类事实（PROMPT-005）：

```text
Claimed → Submitted → PhysicalAccepted
Claimed → Abandoned
Claimed → Submitted → Abandoned
```

`Submitted` 保持 claim pending（只记 receipt），`PhysicalAccepted` 才解决它；`Abandoned` 是终局，
不改 Active Logical Run，同 PromptKey 不再重发。

- 含义：claim 先于发送持久化（durability 是 sequencing 前提）；`acceptanceCallback` 只在
  PhysicalAccepted 后触发。
- 证据：→ PROOF.md R1。

## DISPATCH-PROTOCOL-003 — transport receipt ≠ 物理消息身份

`accepted-*` 是 Host 对调用的回执，**不是**物理 MessageId、**不是** authority 证据；不能从
`Submitted` 推断 authority 已生效。admission 形态可判别（`TransportReceipt.isAdmissionShaped`）。

- 含义：四阶段链保持完整——receipt 只升级到 Submitted；物理落地必须由后续 `chat.message` 建立。
- 边界：crossing 缺席（receipt 永远到不了 root）的另一半归 `interaction-authority`。
- 证据：→ PROOF.md R1、R2。

## DISPATCH-PROTOCOL-004 — physical acceptance 只由真实物理证据建立

`PhysicalAccepted` 只能由真实物理 message evidence 建立：明确的 `msg_*` 落地（live）或恢复时在
Host 尾部找到携带同一 PromptKey 的 `role=user` 消息（recovery）。`accepted-*` 永远不够。

- 含义：Authoroty Root 只有在真实物理消息证明后才生效（`AcceptPhysicalAgentOwnerRoot` 先写
  PhysicalAccepted 再 RegisterAuthority，顺序不可倒）。
- 证据：→ PROOF.md R4。

## DISPATCH-PROTOCOL-005 — PromptKey 是确定性幂等身份

PromptKey 是派生的，从不随机生成：

```text
PromptKey = digest(SessionId, LogicalRunId, AuthorityRootUserMessageId,
                   Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

同一 logical dispatch 在任何进程派生同一 key；任一组件变化都移动 key（PROMPT-011）。

- 含义：恢复能凭 key 匹配 Host 消息回 pending claim；随机 GUID 无法服务（重启后派生不同 key）。
- 证据：→ PROOF.md R2。

## DISPATCH-PROTOCOL-006 — 同 payload 的两个独立 logical act 仍可区分

`ClaimSequence` 以 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 为 scope 单调递增，
在 claim **注册**时消费（成功与否都消费）；abandon 后同 payload 再发得到新序号 → 新 key
（PROMPT-011）。

- 含义：「同一 Guard 连发两次」是两个 key，不是看起来像重复的一个 key。
- 证据：→ PROOF.md R2、R3。

## DISPATCH-PROTOCOL-007 — uncertain physical outcome 不自动重发

显式证据核对时未找到物理落地 → 保持 Pending（`StillPending`），**绝不自动重发，也不因进程重启次数自动 abandon**。进程重启不是替旧 tool 写 terminal 的授权；旧 claim 保持为可审计的中断/未知事实，未来只能由显式 `/continue` 或其它新用户意图决定是否继续。

- 含义：Host 可能已接受消息并开始 provider run；重发会在已落地的消息之外制造第二次逻辑效果；自动 abandon 又会把未知结果伪装成已收尾。
- 边界：物理证据窗口仍可有界；restart-count recovery budget 已退役。
- 证据：→ PROOF.md R3、R4。

## DISPATCH-PROTOCOL-008 — at-most-one logical effect，不虚构 exactly-once

合同是 `at-most-one logical effect + fail-closed unknown outcome`。不假装 exactly-once physical
delivery，不用时间窗口代替 PromptKey，不把 `accepted-*` 当物理落地，不为清理挂起而重发
（PROMPT-011 禁止清单）。

- 含义：unknown 宁可挂起也不重复；预算耗尽时放弃（Abandoned），而非伪装成功。
- 证据：→ PROOF.md R3、R4。

## DISPATCH-PROTOCOL-009 — fire-and-forget 只改变调用方等待

`AwaitMode.Detached`（fire-and-forget）只表示调用方不等待 PhysicalAccepted；claim、authority、
持久化、幂等与错误记录**全部照跑**。禁止独立的 `postPromptFireAndForget` 旁路（PROMPT-007）。

- 含义：Detached 仍返回 PromptKey、仍写 claim/submit；调用方成功不要求物理落地。
- 证据：→ PROOF.md R5。

## DISPATCH-PROTOCOL-010 — Root 不得选择/覆盖 model

发送恒 `Model = None`；`AuthorityExecutionProfile` 没有 model 字段，「Authority Root 覆盖 model」
结构性不可表达（PROMPT-002 的 dispatch 半边）。

- 含义：model 由 Host-final managed binding 解析；root 抬不了 model。
- 证据：→ PROOF.md R2。

## DISPATCH-PROTOCOL-011 — 插件 user-shaped message 一律经 PROMPT-005

所有 runtime/plugin/Host 构造的 synthetic user-role message 必须经 PromptDispatcher 并携带
PromptKey / typed origin metadata；禁止 keyless internal sender（PROMPT-012 残留保留 + corrective
§3.4 closed-world producer invariant 的发送侧）。

- 含义：keyless user message 因此可作为「外部真实用户消息」的判别依据（join wake 候选），
  无需文本启发式。
- 边界：wake 本身归 `delegation`；「keyless ⇒ 非插件生产」的 authority 后果归
  `interaction-authority` 消费。
- 证据：→ PROOF.md R6、R7。

## 反向覆盖核对（COVERAGE.md 归属）

本包 WHAT 覆盖 COVERAGE.md 中单 owner 行：PROMPT-005/007/011、PROMPT-002（Model=None 分片）、
PROMPT-012 残留（「插件 user-shaped message 仍经 PROMPT-005」）、HOST-002 交叉
（keyless 用户消息 signal JoinAttempt 的发送侧判别）。Root/Continuation 判定、来源解析顺序 →
`interaction-authority`（不在此复制）；Requested/Accepted 通用分型 → `effect-accounting`。
