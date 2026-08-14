# host-boundary

> 一句话 WHY：业务必须建立在外部 Host 可稳定证明的物理能力上，而不是流式噪声、私有实现、偶然 hook
> 参数。

## WHAT 概览

本包定义业务依赖外部 Host 时的**最小可验证能力合同**与**观察可靠性**：

```text
事件分层     碎片（message.updated / part.delta / …）在 codec 边界丢弃；
             只有粗粒度信号（idle / retry / aborted / deleted）进入业务层（HOST-001/002，ARCH-002）
typed 边界   业务只见 typed HostSignal，不见 raw payload；信号是 wake 不是事实载体（HOST-003）
快照观测     snapshot 投影保持单一物理事实语义一致（tool part 状态、session-shaped 投影，HOST-004）
compaction   prevention（关闭自动/overflow/prune/autocontinue）+ containment（reanchor），HOST-006
因果读       Transform→ProviderRunIdentity 唯一未完成 assistant；命中 0/≥2 不写 seal（HOST-010）
tool 身份    ToolContext 双半边（message id + call id）；before/after 只有 call id（HOST-011）
多实例      共享身份注册表 vs 每实例 Journal；共享表不跨 await（HOST-012）
sanitize     空 Content 预防，避免上游 400（HOST-016）
定位 canary  sessionID+callID 经完整 SDK snapshot 唯一定位；不能唯一 fail closed（HOST-025）
reasoning    只从 reasoning delta 识别 [NEEDHELP]；rolling suffix；每 run 一次（HOST-027）
物理边界     不修改 OpenCode 本体；只用现有 Hook/SDK（ARCH-003）；tool 文本结果有界（ARCH-012）
```

## HOW 概览

```text
Host/HostDigest.fs                    唯一 sha256（durable identity）
Infrastructure/OpenCode/Codec/         HostEventCodec（信号边界）、NeedHelpEventCodec、ProviderWireDecode
Infrastructure/OpenCode/Host/          Sessions（ISessionHostPort）、SessionSnapshotPort、HostSignalBootstrap、
                                       HostSessionContext、HostMessageProjection、NeedHelpSensor、
                                       SessionQuiescenceGate（宿主）、SharedState、ManagedAgentConfig、
                                       HostCompactionGate/Observer、Events（HostEventPort）
Infrastructure/OpenCode/Signals/       HostSignal / HostSignalAdapter / HostSignalSubscribe
Tools/ToolContext.fs                   Tool 执行上下文（session + workspace + cancellation）
Application/Reconciliation/            Reconciler（single-flight/dirty/有界因果重读）、XWire（transform 合成）
Domain/HostCompactionPolicy.fs         HOST-006 纯策略（prevention keys + containment 决策）
```

## proof 概览

- MOVE（7）：`events-port`、`host-message-projection`、`host-session-context`、`needhelp-sensor`、
  `session-snapshot-locality`、`host001-fragment-events`、`host012-tool-part`。
- NEW：`host-capability-observation`（HOST-006 gate + HostSignal wake + HostDigest）。
- REUSE：`codec/signals.test.mjs`（HOST-002/003）、`plugin/host-hooks.test.mjs`（ARCH-002/003）、
  `context/tool-result-bound.test.mjs`（ARCH-012）、`host/shared-state`（HOST-012 部分）、
  `session-execution-binding`（PROMPT-008 物理身份绑定部分）、`reconciliation/`（xwire 部分）、
  `scripts/checks/architecture.mjs`（host-boundary gate：Kernel/Domain 禁 Fable.Core.JsInterop）。

## 阅读顺序

1. `WHY.md` → 2. `WHAT.md` → 3. `HOW.md`（含历史与弃权）→ 4. `PROOF.md` → 5. `tests/`。

## DEPENDS ON

无产品语义依赖（INDEX.md：`host-boundary → 无`）。本包 PROVIDES 其它 packages 可依赖的物理
ports 与 observation reliability。
