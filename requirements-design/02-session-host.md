# Session / Host substrate

## `session-ontology`

**WHY**  
“有一个 session”不等于“出现一个新 participant”。execution topology、ownership 与 personhood 必须分离，否则 attached work、internal leaf、replica、companion 会制造假的角色与错误能力继承。

**OWNS**
- session 的正交分类轴：execution class × ownership/attachment。
- Root 与 Attached 的逻辑归属区别。
- Work 与 InternalLeaf 的执行能力区别。
- Attached 恰有一个 logical owner；物理 Host parent 不必等于 logical owner。
- runtime topology 不自动决定 Role/Persona/Authority。

**DOES NOT OWN**
- 当前具体 `AttachmentKind` 列表。
- managed session 的 create/reuse/closure/replacement 机制。
- delegation charge/return。
- Role/Persona/ExecutionBinding 身份规则。
- `SatelliteKind`、已删除 Student/Teacher 等历史兼容形状。

**DEPENDS ON**  
无。

**PROVIDES**
- managed lifecycle、participant identity、delegation 共用的 session existence/ownership ontology。

**FAILURE MEANING**  
RED = execution class、logical ownership 与 participant identity 只能靠彼此猜测，不能独立表达。

**INDEPENDENT CHANGE**  
新增一种 Attached Work 类型而不改变 Persona 或 lifecycle protocol。

**CURRENT EVIDENCE**  
`docs/shape/host.md` HOST-008；type `Kernel/SessionOwnership.fs`（`SessionExecutionClass`/`SessionOwnership`）、`Session/AgentRoleIdentity.fs`、`Domain/CompanionIdentity.fs`；fact `Journal/{SessionAssociation,LinkageProjection}.fs`；Dedicated Sync* 与 Companion/Bookkeeper/StrengthReplica。

---

## `managed-session-lifecycle`

**WHY**  
只要系统创建 managed session，就必须有唯一 owner 负责创建、复用、停止、回收与 replacement；否则每个 feature 都会复制 parent map、cancel、retire、restore 规则。

**OWNS**
- managed session create/attach/register。
- owner closure 与级联 cancel。
- reusable vs one-shot。
- completion、retire、tombstone 与已回收实例不可重新激活。
- proven permanent loss 后 replacement 的资格；lookup failure/ownership conflict fail closed。
- restart 后重新定位已有 managed session 的 lifecycle 判据。

**DOES NOT OWN**
- 什么 session kind 存在。
- delegation 的业务含义。
- participant identity。
- generic crash reconciliation；这里只定义 session-specific 合法恢复结果。
- Host 的具体 session API。

**DEPENDS ON**
- `session-ontology`
- `crash-reconciliation`

**PROVIDES**
- managed session 不重复、不孤儿、不从已回收状态重新激活的 guarantee。

**FAILURE MEANING**  
RED = 同一 logical owner 可得到两个活跃 replacement，或 restart/cancel 后 ownership 无法收敛。

**INDEPENDENT CHANGE**  
把当前 runtime registry 换成 durable locator + Host lookup，而不改变 session ontology/delegation WHAT。

**CURRENT EVIDENCE**  
HOST-008/015；wiring `Session/{AttachedSessionRuntime,HandleController,ForkRuntime,SatelliteRuntime,ReuseScope}.fs`；fact `Journal/LinkageProjection.fs`；handle tombstone、managed-session restore tests。

---

## `host-boundary`

**WHY**  
业务必须建立在外部 Host 可稳定证明的物理能力上，而不是流式噪声、私有实现、偶然 hook 参数。

**OWNS**
- 最小 Host capability contract：稳定 session/message snapshot、粗粒度 lifecycle wake、provider transform、tool invocation、attempt interruption、session create/read、必要 identity/config observation。
- stream fragment 只可作窄传感器输入，不能积分成业务真相。
- Host capability 缺口必须由 canary/contract proof 证明；不能默默依赖 undocumented API。
- provider run / tool call 等物理 identity 的可信取得边界。
- observation 不足或多解时 fail closed。
- 默认不修改 Host 本体；若未来产品选择 Host fork，应另立需求。

**DOES NOT OWN**
- OpenCode hook 名/参数 shape。
- session ontology/lifecycle。
- provider language、interaction authority、projection。
- Pair guidance、Todo membrane、compaction policy 等 feature 语义。
- upstream workaround/quirk。
- Host 假设需要什么 proof 强度；这是 verification-system 的横向治理，不是 Host semantic dependency。

**DEPENDS ON**
- 无产品语义依赖。

**PROVIDES**
- 其它 packages 可依赖的物理 ports 与 observation reliability。

**FAILURE MEANING**  
RED = 产品语义需要猜 Host private/streaming state 或依赖未经验证的物理能力。

**INDEPENDENT CHANGE**  
迁到另一 Host，只要 adapter 提供同等 capability，participant/mission/durability WHAT 不变。

**CURRENT EVIDENCE**  
`docs/{why,what,shape,how,proof}/host.md`；ARCH-002/003；host `Host/HostDigest.fs`、`Infrastructure/OpenCode/**`、`Tools/ToolContext.fs`；wiring `Application/Reconciliation/{Reconciler,XWire}.fs`；snapshot、transform、session API 与 canaries。ProviderLanguage、HOST-013、MagicTodo overlay 等混入 Host 文档的产品事实应迁出。
