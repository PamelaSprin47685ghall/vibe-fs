# behavior-diagnosis — HOW（实现模型与约束）

> 非 normative。WHAT 命题的落点见 `PROOF.md`；本文件解释 `src/` 里每个概念
> 的精确位置、约束与失败模式，末尾是「历史与弃权」。

## 1. 领域纯内核

### 1.1 `src/Wanxiangshu/Domain/EnforcerCatalog.fs`

```fsharp
type EnforcerRule =
    { Name: string          // 目录 basename = TipIdentity
      EnforcerText: string  // resources/enforcer/<name>/enforcer.md 全文
      MainText: string      // resources/enforcer/<name>/main.md 全文
      RuleId: string        // durable id; clean break = Name
      FieldName: string     // provider enum 值; = Name
      LexicalOrder: int }   // 目录顺序 1..N（只描述装载/enum 顺序）
```

- `EnforcerCatalog.validate`（BD-003）：schemaVersion=1、非空、三身份唯一且相等、
  序连续、正文非空。失败返回 `Error string`，装载层转抛 → fail fast。
- `EnforcerCatalog.tryFindByField`（BD-007）：trim 后精确 `FieldName`/`Name` 命中；
  无 fuzzy、无近似、无默认。
- `EnforcerCatalog.fieldNames`：按 LexicalOrder 输出 provider enum 清单。

### 1.2 `src/Wanxiangshu/Domain/EnforcerCodec.fs`（BD-006/007/008）

```fsharp
type CanonicalBlogCall = { Text: string option; Evidence: string option; Tip: EnforcerTip }
```

- `decodeCall rules rawArgs`：只认 `entry`（兼容旧 `text`）、`tip`、`evidence`；
  其余 property 忽略（ENFORCER-024）。缺/空/非 string `tip` → `MissingTipError`；
  未知 tip → `UnknownTip <value>`。
- `hasValidText`：entry trim 后非空才算有效文本。

> 注意（诚实性）：历史 what/enforcer 条款 ENFORCER-004/020 声称「无 `evidence`
> 字段」，但当前 codec **仍保留 optional `evidence`**（合并时精确去重、上限
> 128 KiB）。本包按当前世界记录：evidence 不改变 occurrence 身份（BD-009），
> 「evidence 删除」是文档与代码之间的漂移，见 §7 弃权。

### 1.3 `src/Wanxiangshu/Enforcer/Cycle/Model.fs`（BD-009）

Provider cycle 的 cardinality gate 位于 Host continuation 边界：raw assistant step 必须恰好一个
`chronicle` part。0 次与 2+ 次不进入 merge/commit；terminal 后直接复用 BD-017 的 protocol repair。
其中 2+ 次通常仍会因 Host tool loop 进入下一次 transform；**0 次不会**，所以 zero-tool terminal 的
repair 入口由 `SessionIdle → ReconcilePass → HostTurnObserver` 驱动，不能等一个不存在的后续 transform。
`EnforcerCycle` 只处理已通过该 gate 的单调用 canonical value，不再承担多调用业务归并语义。

### 1.4 `src/Wanxiangshu/Domain/RulebookObservation.fs`（BD-015）

- `ObservationUnit`：可选 TipName + 可选 FrameDigest/Body 的配对单元。
- `WorkLogObservation`：TipName + CycleId + 可选 FrameDigest（tip-anchored）。
- `pairTipsAndFrames`：前向 zip；剩余 tips 或 frames unpaired 追加。
- `ofTipsAndFrames`：zip tip 身份 × frame digest；剩余 tips 保留（digest=None），
  剩余 frames 丢弃（不发明 tip）。

## 2. 资源装载与 Blogger system 合成（BD-002/004/005）

`src/Wanxiangshu/Infrastructure/Resources/EnforcerCatalogResource.fs`：

- `loadFor lang`：枚举 `resources/enforcer/*/` 子目录（basename = TipName），
  kebab-case 校验，按语言读叶子（en：`enforcer.md`+`main.md`；zh-CN：
  `enforcer.zh-CN.md`+`main.zh-CN.md`），缺文件/空文本抛异常，最后过
  `EnforcerCatalog.validate`；任何失败 → 启动异常。
- `composeBloggerSystemPromptFor lang base rules`：base + `# Enforcer Rulebook` +
  按 LexicalOrder 的 `## <Name>` + enforcer.md 全文，`"\n\n"` 拼接。derived only，
  不写回仓库。`main.md` **从不**进入 Blogger system（audience 分离，见
  `guidance-delivery`）。

## 3. Cycle 提交与恢复（BD-010/011/012/013/017）

### 3.1 校验：`src/Wanxiangshu/Session/EnforcerCycleDecode.fs`

- 内容硬界：`MaxBlogTextBytes = 512 * 1024`、`MaxEvidenceBytes = 128 * 1024`。不存在
  多调用 merge cap：2+ raw `chronicle` 在 canonical cycle 构造前已转 protocol repair。
- cardinality（BD-009）：先按 raw assistant parts 计 `chronicle` 调用数；terminal 时必须 =1。
  0/2+ → protocol repair，不进入 `validateCycle`/commit。
- 身份/边界校验（BD-010/011）：通过 cardinality 后，空 messageId / 越界 →
  `Diagnostic.fatal "enforcer-cycle-failed"`（fail closed）。`EnforcerHost` 同名的
  `MaxBlogTextBytes` 等常量必须与 Decode 保持一致（单一来源是 Decode）。

### 3.2 提交：`src/Wanxiangshu/Session/EnforcerCycleCommit.fs`

```fsharp
type CycleCommitOutcome = KnownCommitted | KnownNotCommitted of string | CommitUnknown of string
```

- `commitCycle`：先查 receipt（已存在 → `KnownCommitted`，幂等）；无 staged context
  → `KnownNotCommitted`；PERSIST-010 precheck（staged ingest/cutoff/epoch vs 投影
  不一致）→ `KnownNotCommitted`（可恢复弃置，绝不先写事实再被 fold 拒绝）；
  blobs 先写（text、evidence），再 append 单条 `BlogObservationCommitted`；
  `WriteUnknown` → `CommitUnknown`（fail-closed reconcile，不盲重试模型）。

### 3.3 协调：`src/Wanxiangshu/Session/EnforcerHost.fs` + `EnforcerContinuation.fs`

- `handleContinuation`：薄分发（emptyCallsBranch / commitBranch / firstRequestBranch）。
- `EnforcerContinuation`：三分支 + `CycleDisposition`；成功提交后 Park 或注入；0/2+ chronicle 与
  completed-but-invalid 单调用共用 nudge/repair/AABB/Fallback 决策表（BD-017）。同一 terminal 重放
  只投影，不重复 nudge；下一 invalid terminal 才进入 AABB。
- `HostTurnObserver`：`Role.Blogger` 的 zero-tool idle terminal 不再进入 ordinary
  `MissingClosingReport`。它调用 `InteractionRepairWorkflow.repairBloggerProtocol`：第一次 invalid
  terminal 通过 fresh idle permit 发送 `blogger-missing-tool` nudge；同 terminal 重放幂等；新的
  invalid terminal 消费 fresh idle permit、由 `FallbackLedger` 推进一步并发送 `blogger-aabb` repair；
  AABB durable claim 已存在后再失败则终止该 Blogger cycle，不再自动提示。
- cold/reconcile recovery 只从 `SessionMessage.ToolParts` 读取 `ToolName=chronicle` + completed state；
  `MessagePart.ToolResult` 已丢 tool name，禁止用于判断修复是否成功。`blogger-aabb` claim 是 idle AABB
  的 durable 阶段证据；纯 transcript `rejudgeFromEvidence` 仍不得凭空发明 AABB。
- `EnforcerFrameRecovery.fs`：`tryLiveCycleContext`（commit 只用 live InFlight）、
  `tryReloadRequestContext`（durable open materialization 恢复）、
  `lastCoveredSequence` / `coveredPrefixDigest`（出生门，BD-013）。

### 3.4 投影：`src/Wanxiangshu/Feedback/Enforcer/Projection.fs`（BD-014）

- `RecentTipLimit = 8`；`applyFromEntry`（每 cycle 一个 tip，按 ProviderRun 幂等）；
  `applySquash count`（co-truncate 最老 `min(count, tips)`）；`recentTips`
  oldest → newest；`tryFindByProviderRun`。
- `src/Wanxiangshu/Feedback/Enforcer/Observation.fs`（BD-015/016）：
  `observationsOf` / `observationsOfSession` / `observationsAfterSquash` 把
  Enforcement 与 Blog 两个投影 zip 成配对 Observation 视图——命名 fold，非第二
  store；物理事实仍是 `BlogObservationCommitted` / `BlogObservationsSquashed`。

## 4. 失败模式速查（红了说明什么）

| 症状 | 断裂的命题 | 排查入口 |
|---|---|---|
| 新增规则后 `catalog.test.mjs` 失败 | BD-003 | `EnforcerCatalog.validate` 或目录/叶子 |
| 未知 tip 被接受 | BD-007 | `tryFindByField` 是否被改成 fuzzy |
| 2+ `chronicle` 仍然提交/推进 coverage | BD-009 | raw cardinality gate 是否在 commit 前被绕过 |
| 重复 ToolCallId 竟提交了 | BD-010 | `EnforcerCycleDecode` 身份校验 |
| frame 与 coverage 不同步 | BD-012 | `EnforcerCycleCommit.commitCycle` 原子性 |
| squash 后 tips 独立存活 | BD-016 | `EnforcementProjection.applySquash` / Fold 接线 |
| 零推进窗口被启动 | BD-013 | `mainContextFromChunk` 出生门 |

## 5. 验证命令

```text
node --test requirements/behavior-diagnosis/tests/<file>   # 单文件（每文件必须绿）
node requirements/verification-system/tests/run.mjs                                    # 全单元（cutover 时由 lead 执行）
```

## 6. 依赖

- `semantic-trace`：诊断建立在 XTrace 覆盖推进的事实上（BD-013 出生门读
  XTraceProjection）。
- 消费方：`guidance-delivery`（把本包产生的 occurrence 变成 Main 可恢复交付）。

## 7. 历史与弃权

| 源 | 裁决 | 记录 |
|---|---|---|
| 旧 `SSOT/15` score-vector / throttle / NudgeAnchored / Main overlay | GARBAGE（clean break，ENFORCER-072/073） | 历史 why/enforcer；历史 change（enforcer）§10 |
| `catalog.json` 与 `enforcement-a01` 旧 id | GARBAGE（目录即身份取代） | 历史 change（rulebook）§0/§23 |
| 历史 what/enforcer 声称「无 evidence 字段」 | HOW 漂移：当前 codec 仍保留 optional evidence（merge 去重 + 128 KiB 界）；occurrence 身份不因 evidence 改变 | 本文件 §1.2；cutover 时需与文档统一 |
| `scripts/checks/enforcer-rulebook-gate.mjs` | 已退休空壳（2026-08-12）；tip-SSOT proof 由 `tests/unit/enforcer/**` catalog 测试承担，不再有 prose 形状机器门 | 历史 HANDOFF §24 |
| enforcer.md 写作宪法 A4–A30（mandatory headings / token budget / sibling 校准） | HOW（authoring 规范，非 runtime 合同）；不再有机械门 | 历史 change（rulebook）Appendix A |
| Blogger 生命周期物理所有权（HasFlight / HasParked / PendingOffer / DrainWindow） | 归 Blogger convergence 交叉（`context-compression` 侧）；本包只消费 cycle 提交事实 | `requirements/behavior-diagnosis/tests/blogger-cycle-atomic-fact.test.mjs` |
