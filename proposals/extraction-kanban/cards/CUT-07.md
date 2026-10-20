# CUT-07：参与者身份与执行绑定分离（含 D-01 护栏）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-07) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（2026-10-20 eng-cut07-goose：库 identity 0.11.0 发布，主仓切换合入 master，旧 owner 退出；见 §11）
- **所属泳道 / 波次**：权威 / W1 独立卡
- **切换前置**：`CUT-00`（已完成，见 [CUT-00](CUT-00.md)）
- **强制护栏（D-01）**：**严格保留现行未知来源拒绝 / 明确 `UnknownPromptOrigin` 语义，绝对禁止按 `wanxiangli/src/authority` 草稿将缺失或未知来源静默回退为 `human`。**
- **零阻塞结论**：角色词汇（`Role`）、具名人格与私有身份（`PersonaCatalog` / `ParticipantIdentity`）、入站来源分类与身份种子（`PromptOrigin` / `IdentitySeed`）以及会话拓扑关联（`SessionOwnership` / `SessionAssociation`）在主仓中已位于独立的纯 Contract `.fsproj`，零 I/O、零宿主 SDK 依赖，可零阻塞独立抽出。

## 2. 草稿概念名/路径与主仓真实模块/`.fsproj` 映射

委托说明及 `wanxiangli/src/identity`、`src/authority` 草稿中使用了 `src/Wanxiangshu/Authority/`、`ActorIdentity`、`PersonName` 等概念名。在主仓当前基线（`b322c180b`）中，这些概念分布在以下精确路径与编译单元中：

| 草稿概念 / 委托说明符号 | 主仓真实源码路径（含 `.fsi`） | 所属编译单元（`.fsproj`） | 对应真实类型与职责 |
|---|---|---|---|
| 角色基础词汇（公开角色与历史角色隔离） | `src/Wanxiangshu/Foundation/Roles.fs(.fsi)`<br>`src/Wanxiangshu/Foundation/RolesSurface.fs(.fsi)` | `Wanxiangshu.Owner.participant-identity.foundation-roles.fsproj` | `Role`（`Manager | Orchestrator | Engineer | DevOps | Blogger | Plan` 等）及 `tryParseRole` / `tryParseHistoricalRole` |
| `ActorIdentity` / `PersonName` / 受管目录 | `src/Wanxiangshu/Participant/Persona/Catalog.fs(.fsi)`<br>`src/Wanxiangshu/Participant/Persona/ManagedCatalog.fs(.fsi)`<br>`src/Wanxiangshu/Participant/Persona/Identity.fs(.fsi)`<br>`src/Wanxiangshu/Participant/Persona/Surface.fs(.fsi)` | `Wanxiangshu.Owner.participant-identity.participant-persona.fsproj` | `Persona`（稳定职务人格）、`PersonaProvenance`、`ParticipantIdentity`（私有强类型，仅经 `IdentitySeed` 解析）、`ParticipantIdentityEvidence`、`ManagedAgentCatalog` |
| `IdentitySeed` 与 **D-01** 来源鉴别（`Origin`） | `src/Wanxiangshu/Interaction/Authority/IdentitySeed.fs(.fsi)`<br>`src/Wanxiangshu/Interaction/Authority/Origin.fs(.fsi)`<br>`src/Wanxiangshu/Interaction/Authority/Facts.fs(.fsi)`<br>`src/Wanxiangshu/Interaction/Authority/PromptSessionFact.fs(.fsi)` | `Wanxiangshu.Owner.interaction-authority.interaction-authority-identityseed.fsproj` | `IdentitySeed`、`PromptOrigin`（含 `UnknownPromptOrigin` 拒绝分支，不回退 `HumanPrompt`）、`AuthorityRootAcceptancePayload` |
| 会话拓扑角色与容器归属 | `src/Wanxiangshu/Execution/Session/Ownership.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/SyncDelegateRole.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/Association.fs(.fsi)`<br>`src/Wanxiangshu/Execution/Session/AssociationSurface.fs(.fsi)` | `Wanxiangshu.Owner.session-ontology.execution-session-ownership.fsproj`<br>`Wanxiangshu.Owner.session-ontology.execution-session-syncdelegaterole.fsproj`<br>`Wanxiangshu.Owner.session-ontology.execution-session-association.fsproj` | `SessionOwnership`、`SyncDelegateRole`（`Bookkeeper | Predictor`）、`SessionAssociation` |
| `ExecutionBinding`（物理执行与模型目标绑定） | `src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs(.fsi)`<br>`src/Wanxiangshu/OpenCode/Host/Sessions.fs(.fsi)`<br>`src/Wanxiangshu/OpenCode/Host/SessionsSurface.fs(.fsi)` | `Wanxiangshu.Owner.session-ontology.opencode-host-sessionexecutionbinding.fsproj`<br>`Wanxiangshu.Owner.session-ontology.opencode-host-sessionbindingsurface.fsproj` | `ExecutionBinding` / `SessionExecutionBinding`（将每次物理执行的 `ModelTarget` 与容量租约同逻辑 `ParticipantIdentity` 解耦） |

## 3. 目标

- **用户/维护者当前损失**：`wanxiangli/src/identity` 草稿仅定义了极简不透明 ID 与裸角色绑定，缺少主仓要求的版本化 provenance（`Role × PersonaProvenance` resolve-once）、内部身份隔离（`Bookkeeper` / `Predictor` 不入公开 `Role`）、活跃/历史角色解码隔离以及 **D-01** 未知来源显式拒绝；若直接按草稿简化替换，会导致切换模型重绑身份、历史只读角色在恢复时扩权为 `Engineer`，或无标记输入被当成人类指令放行。
- **这次完成后可少维护什么**：在 `wanxiangli/identity` 确立自治的逻辑参与者身份（`ParticipantIdentity` / `ParticipantIdentityEvidence`）与物理执行绑定（`ExecutionBinding`）分离契约，F# 与 JS 边界共用同一套不可伪造的身份验证与解析入口，为后续 `CUT-08`（能力门禁）、`CUT-09`（权威根）、`CUT-10`（模型路由）提供稳定基石。
- **明确不做**：
  - 不建立包含全仓所有业务 ID（如 `EventId`、`WorkId`、`CaseId`、`JsTransactionId`）的“万能 Foundation ID 库”；
  - 不把 `ParticipantIdentity` 的五元组证据（`OwnerLogicalRunId`、`LogicalRunId`、`Role`、`SelectedAgent`、`Persona` + `PersonaProvenance`）压缩成裸 `role: string`；
  - 不在本卡内迁移 `Interaction/Authority/Ledger.fs` 的完整逻辑根生命周期状态机（归 `CUT-09`）或 MJS 容量调度器（归 `CUT-10`）。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/participant-identity/WHAT.md`：`[001]` `ParticipantIdentity` 是 logical participant run 的唯一私有身份 owner、`[002]` `ParticipantIdentity ≠ ExecutionBinding`、`[003]` `Persona` 由 `Role × versioned provenance` resolve-once 并随 `AuthorityRootAccepted` 原子安装、`[004]` 换执行者 ≠ 换人、`[005]` system prompt identity 只消费 `ParticipantIdentity`、`[006]` 稳定 Role identity 与可变机器 binding 严格分界、`[007]` 内部身份（`Bookkeeper`、`Predictor`）受同一原子模型约束且不入公开 `Role`、`[008]` 派生 root 只能安装显式 owner-derived identity evidence、`[009]` exact prior-run closure 后才可在同一 `SessionId` 安装 fresh identity、`[010]` 活跃身份解析与历史身份隔离解码。
  - `requirements/capability-enforcement/WHAT.md`：`[001]`、`[002]`、`[006]`、`[013]`、`[014]` 权限仅由逻辑身份与请求类别决定，不被模型切换或未知来源绕过。
  - `requirements/session-ontology/WHAT.md`：`[001]`—`[014]` 会话容器（`SessionId`）、逻辑运行（`LogicalRunId`）、拓扑归属（`SessionOwnership`）与物理模型绑定分离。
- **新库草稿差异及裁决（D-01 & 身份体系）**：
  1. **D-01 护栏**：`wanxiangli/src/authority` 草稿曾允许缺来源标记时默认回退为 `human`；本卡与 `CUT-09` 严格执行 D-01 裁决——任何缺失、畸形或未获权威证据支撑的来源一律归入显式拒绝（`UnknownPromptOrigin` / `OriginRejected`），绝不回退为人类输入。
  2. **身份证据完整性**：拒绝 `wanxiangli/src/identity` 草稿的“裸 role 代替身份证据”简化，完整保留 `Role × Persona × SelectedAgent × PersonaProvenance` 的 resolve-once 语义以及 `tryParseRole`（仅活跃角色）与 `tryParseHistoricalRole`（仅历史解码）的严格分界。

## 5. 自治个体（A / B / C）

- **A（`ParticipantIdentity` & `IdentitySeed` Owner，抽入新库）**：
  - 负责：维护活跃角色（`Manager | Orchestrator | Engineer | DevOps | Blogger | Plan`）与内部角色（`Bookkeeper | Predictor`）的封闭类型、`Role × PersonaProvenance` 到稳定 `Persona` 的一次性确定性解析、派生身份的 owner-derived evidence 校验，以及历史角色名与活跃角色名的隔离解码。
  - 拥有状态：纯不可变值与确定性目录，无可变运行时缓存。
  - 永远成立的不变量：外部模块无法直接构造或篡改 `ParticipantIdentity` 字段；`Coder`/`Inspector`/`Browser`/`Inquiry`/`Distiller` 等历史角色在活跃解析路径 `tryParseRole` 中永远返回 `None`；`DevOps` 固定解析为唯一名号 `"devops"`（Persona `Operator`），绝不被解析为具备 Fission 权能的 `Engineer`。
- **B（`ExecutionBinding` 值与校验模型，抽入新库契约层）**：
  - 负责：表达单次物理执行的模型目标与租约绑定（`SessionId + PhysicalUserMessageId + Role + ParticipantIdentity + ModelTarget + CapacityFence`），提供换绑（retry / fallback / readonly-delegate）时保持 `ParticipantIdentity` 不变的纯变换与一致性校验。
  - 与 A 不共享可变值：B 只能引用 A 的只读身份证据，绝不能反向修改 A 的 `Role`、`Persona` 或 `SelectedAgent`。
- **C（主仓 `AuthorityLedger` 与 `SessionExecutionBinding` 组合层）**：
  - 负责：在准入边界结合 `PromptOrigin`（执行 **D-01** 拒绝未知来源）将 `ParticipantIdentityEvidence` 写入原子 `AuthorityRootAccepted` 事件；在 `chat.params` 与重试边界调用 MJS scheduler 更新物理 `ExecutionBinding`；在同一 `SessionId` 复用前核验 `AuthorityLogicalRunClosed`（`participant-identity-009`）。

## 6. 公开消息与最小完整契约

- **输入字段 / 字节 / 范围 / 身份**：
  - 活跃角色解析：`Roles.tryParseRole : string -> Role option`（仅接受 `"manager"`、`"orchestrator"`、`"engineer"`、`"devops"`、`"blogger"`、`"plan"`）；
  - 历史解码解析：`Roles.tryParseHistoricalRole : string -> Role option`（仅供历史事件解码使用）；
  - 身份种子解析：`IdentitySeed.resolveRoot : LogicalRunId -> Role -> SelectedAgent -> PersonaProvenance -> Result<ParticipantIdentityEvidence, IdentityRejection>`；
  - 派生身份解析：`IdentitySeed.resolveDerived : parentEvidence: ParticipantIdentityEvidence -> childRunId: LogicalRunId -> childRole: Role -> Result<ParticipantIdentityEvidence, IdentityRejection>`；
  - 来源判定（D-01）：`PromptOrigin.classify : rawOriginObservation -> Result<VerifiedPromptOrigin, UnknownPromptOrigin>`。
- **成功结果及其精确保证**：
  - 返回不可变的 `ParticipantIdentity` 与可序列化的 `ParticipantIdentityEvidence`，保证 `Role`、`Persona`、`SelectedAgent` 与 `Provenance` 满足唯一映射；
  - 物理重绑 `ExecutionBinding.rebindTarget` 仅产出携带新 `ModelTarget` / `CapacityFence` 的 `ExecutionBinding`，其关联的 `ParticipantIdentityEvidence` 逐字段保持原值。
- **拒绝结果及零副作用保证**：
  - 活跃路径传入历史角色名（如 `"coder"`、`"inspector"`）或内部槽位名（`"predictor"`、`"bookkeeper"`）、派生身份的 `OwnerLogicalRunId` 不匹配、同一 run 内重复安装不同 payload、来源缺失或未鉴权（D-01）：全部返回具名 `Error`，不产生任何半安装身份或默认 `human` / `engineer` 身份。
- **Unknown 的来源与恢复方式**：
  - 入站请求缺少来源证明、来源字段类型非法（非原始非空字符串）或声称内部角色却无对应存活委派句柄时，一律分类为 `UnknownPromptOrigin` 并 fail-closed 拒绝准入，不允许猜测恢复。
- **公开 `.d.ts` 与 Fable 交付形状**：
  - JS 边界（`RolesSurface`、`PersonaSurface`、`SessionsSurface`）只接收原始字符串与 plain record，在入口执行显式 JS 类型谓词校验（拒绝 숫자、布尔、boxed 值或空串），内部强类型 `ParticipantIdentity` 不裸露可改写字段。

## 7. 主仓调用方清单与对应测试套件

### 7.1 主仓调用方清单

1. **权威准入与来源校验（D-01 边界）**：
   - `src/Wanxiangshu/Interaction/Authority/{IdentitySeed,Origin,Facts,PromptSessionFact,Model,Fold,Ledger,ProjectionQueries,Surface}.fs`
   - `src/Wanxiangshu/OpenCode/Host/ChatAdmission/{Intent,Transaction}.fs`
2. **会话拓扑、委派与生命周期**：
   - `src/Wanxiangshu/Execution/Session/{Ownership,SyncDelegateRole,Association,AssociationSurface}.fs`
   - `src/Wanxiangshu/Execution/Session/Attachment/{AttachmentLeaseCore,Runtime}.fs`
   - `src/Wanxiangshu/Execution/Session/ChatExecution/{Facts,Fold,Runtime}.fs`
   - `src/Wanxiangshu/Execution/Delegation/{Fact,Ledger,Fork/Runtime,SyncDelegate/Model,SyncDelegate/Runtime}.fs`
   - `src/Wanxiangshu/Execution/Fission/{Facts,Fold,OpenCode/Host}.fs`
3. **物理模型绑定、重试与投影**：
   - `src/Wanxiangshu/OpenCode/Host/{SessionExecutionBinding,Sessions,SessionsSurface,ManagedAgentConfig,ProviderSystemTransform}.fs`
   - `src/Wanxiangshu/Participant/Provider/Attempt/{RequestKind,Failure,Fallback/Facts,Fallback/Fold,Fallback/Ledger}.fs`
   - `src/Wanxiangshu/Participant/Provider/Projection/{Plan,Wire}.fs`
   - `src/Wanxiangshu/Resources/PromptResources.fs`
   - `src/Wanxiangshu/Foundation/OfficeCapability.fs`

### 7.2 对应测试套件

- `requirements/participant-identity/tests/001.test.mjs` 至 `005.test.mjs`、`007.test.mjs` 至 `010.test.mjs`（完整覆盖 `WHAT[PARTICIPANT-IDENTITY-001..010]`）
- `requirements/capability-enforcement/tests/001.test.mjs`、`002.test.mjs`、`006.test.mjs`、`013.test.mjs`、`014.test.mjs`
- `requirements/session-ontology/tests/001.test.mjs` 至 `012.test.mjs`、`014.test.mjs`

## 8. 必须能红的最小反例与拆除测试设计

1. **反例 1（D-01 未知来源回退 human）**：构造缺失来源标记、携带非字符串来源字段或伪造内部 continuation 标记的入站请求，调用来源鉴别与准入接口，必须返回 `Error (UnknownPromptOrigin ...)` 并拒绝准入；若任何分支将其静默当成 `HumanInteractive` 或自动生成 `AuthorityRoot`，测试立即变红。
2. **反例 2（换模型/重试篡改身份或 System Prompt）**：对已绑定 `ParticipantIdentity(Role.Engineer, Persona.Engineer, ...)` 的 logical run 执行物理重试、fallback 到备用 provider/model 或发起 Predictor 只读同伴委托，检查重绑后的 `ParticipantIdentityEvidence` 与 System Prompt 身份段落，必须与初始安装逐字节一致（对应 `participant-identity-002/004/005/006`）。
3. **反例 3（历史只读角色在活跃路径扩权或内部身份外泄）**：向活跃身份解析器 `tryParseRole` 或 fork 准入入口传入历史角色 `"inspector"`、`"coder"` 或内部槽位 `"predictor"`、`"bookkeeper"`，必须全部拒绝；向 `DevOps` 身份请求 Fission 能力必须拒绝（对应 `participant-identity-007/010`）。
4. **反例 4（未关闭前任 run 即在同 SessionId 安装新身份，或错配父证据）**：在同一 `SessionId` 尚未提交 `AuthorityLogicalRunClosed` 时尝试安装新 `ParticipantIdentity`，或传入 `OwnerLogicalRunId` 与父 run 不一致的派生证据，必须 fail-closed（对应 `participant-identity-008/009`）。
5. **独立拆除测试**：在 `wanxiangli` 中独立编译 `identity` 子包（不含 OpenCode 宿主、MJS 调度器或 SQLite/EventStore），运行上述 4 个反例与正控测试全绿。

## 9. 精确写集合与暂留/相邻卡边界

- **库仓（`wanxiangli`）写集合**：
  - `src/identity/*` 及对应工程/导出声明（实现 `Role`、`Persona`、`ParticipantIdentity`、`ParticipantIdentityEvidence`、`IdentitySeed`、**D-01** `PromptOrigin` 鉴别类型与 `ExecutionBinding` 纯绑定合同）。
- **主仓（`wanxiangshu`）切换期写集合**：
  - `src/Wanxiangshu/Foundation/Roles.fs(.fsi)`
  - `src/Wanxiangshu/Foundation/RolesSurface.fs(.fsi)`
  - `src/Wanxiangshu/Participant/Persona/{Catalog,ManagedCatalog,Identity,Surface}.fs(.fsi)`
  - `src/Wanxiangshu/Interaction/Authority/{IdentitySeed,Origin}.fs(.fsi)`
  - `src/Wanxiangshu/Execution/Session/{Ownership,SyncDelegateRole,Association,AssociationSurface}.fs(.fsi)`
  - `src/Wanxiangshu/Wanxiangshu.Owner.participant-identity.foundation-roles.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.participant-identity.participant-persona.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.interaction-authority.interaction-authority-identityseed.fsproj`
- **暂留主仓与相邻卡边界**：
  - `src/Wanxiangshu/Foundation/Identity.fs` 中的通用领域 ID（`EventId`、`SessionId` 等）按需保留，不搞全仓 ID 大搬家；
  - `src/Wanxiangshu/Interaction/Authority/{Model,Fold,Ledger,ProjectionQueries}.fs` 的逻辑根状态机与关闭结算归 `CUT-09`；
  - `src/Wanxiangshu/Foundation/OfficeCapability.fs` 与工具双侧门禁归 `CUT-08`；
  - `src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs` 中涉及 MJS 容量租约查询与 Journal 投影的组合逻辑留待 `CUT-10` / `CUT-12` 协同接线。

## 10. 实施记录（库仓独立能力完成，源码层面）

- **认领**：Engineer 认领本卡库仓部分（`wanxiangshu-lifecycle-cutover` 工作树内 `wanxiangli` 库仓）。
- **已落地**（`wanxiangli/src/identity`，Fable 独立编译单元，无 ProjectReference）：
  - `Roles.fs`：活跃六角色 `tryParseRole` 与历史十一角色 `tryParseHistoricalRole` 的严格分界（反例 3）；
  - `PersonaCatalog.fs`：V1 目录（`Role → Persona`、`bookkeeperPersona`、`predictorPersona`）；
  - `ParticipantIdentity.fs`：`ParticipantIdentity` 私有构造、`resolveAtRoot`/`inheritFromOwner`/`rehydrate` 的 resolve-once 与逐字段错配拒绝（`LegacyParticipantName`/`RoleMismatch`/`PersonaMismatch`/`UnsupportedPersonaCatalogVersion`/`OriginMismatch`/`OwnerRequired`），内部槽位（bookkeeper/predictor）显式映射不入公开 Role；
  - `PromptOrigin.fs`：D-01 `classify`——human-root 必须携带 participant 证明、agent-owner-root 必须携带 owner/child 证明、continuation 闭合词汇、host-internal；其余一律 `UnknownPromptOrigin`，**不存在任何 human 回退分支**（反例 1）；
  - `ExecutionBinding.fs`：`bind`（全字段 + 活跃角色 + 证据角色一致 + 证据整体重验）与 `rebindTarget`（只改 `modelTarget`/`capacityFence`，evidence 逐字段原值；重建时整体重验，篡改即拒）（反例 2）；
  - `Identity.fs`：JS 门面（`wanxiangli/identity` 导出，plain data 进出）+ `index.d.ts` 契约声明；
  - `tests/001-005.test.mjs`：WHAT[participant-identity-00N] 锚点，覆盖卡内反例 1–3 及 owner 派生（反例 4 的库仓侧：`OwnerRequired`/persona 错配；同 SessionId 关闭核验归 CUT-09 主仓侧）。
- **主仓切换状态**：被 vendor tarball `0.5.2`（无 `./identity` 导出）物理阻塞，归 CUT-33 发 `0.6.0` 后的集成队列；主仓 `Roles.tryParseRole` 宽解析被 Journal 历史解码调用方依赖（编辑面外），活跃隔离已由 `ParticipantIdentity` 层承担且 `requirements/participant-identity/tests/010.test.mjs` 绿，故本卡不动主仓源码。
- **验收边界**：源码与测试已写、未运行；DevOps 需在库仓执行 `npm test`（含 `src/identity/tests`）与独立构建验证后方可认领通过。

## 11. 主仓切换与旧 owner 退出（2026-10-20，eng-cut07-goose）

### 11.1 库侧（wanxiangli）

- `b196749` 实现、`54dcc34` 发布 **0.11.0**。master 现在的 gitlink `c9a943d`（0.16.0）包含它。
- `Roles.fs`：补 `Advisor`（活跃、内部），增加 `isInternal`（Advisor、Distiller）。活跃顺序为 Orchestrator, Manager, Engineer, DevOps, Plan, Advisor；历史角色为 Coder, Inspector, Browser, Inquiry, Distiller。
- `PersonaCatalog.fs`：Advisor → Auditor，增加 `Persona.tryParse`（含 `DirectorV1` 别名）。
- `ParticipantIdentity.fs`：按宿主算法逐分支重写。
  - 名称分类顺序：Blank → Legacy → Malformed → bookkeeper/活跃角色 → Unknown。
  - `validateShape` 依次检查目录版本、RoleMismatch、BlankPersona。
  - 根来源 rehydrate 检查 PersonaMismatch；继承来源检查 OwnerPersonaMismatch / OwnerCatalogVersionMismatch；有 owner 但来源为根时报 OriginMismatch。
  - 继承的 legacy 名解码为历史角色。
  - `restoreRecorded` 保持宿主 `fromInput` 语义。
  - JS 边界 `decodeInput` 报 InvalidRoleLabel / InvalidOriginLabel。
  - `errorView` 带 `expected` / `actual`。
- `Identity.fs` 门面与 `index.d.ts` 同步更新；`ExecutionBinding` 改走 `decodeInput >> restoreRecorded`。
- 测试：identity 32/32；库全套 504/504（`--test-force-exit`，原因见 evidence §1）。

### 11.2 主仓接线（F）

两个提交：`refactor(CUT-07): consume wanxiangli/identity as the participant identity owner` 和 `fix(CUT-07): call the typed RoleCatalog interface instead of dynamic access`。

- `Foundation/Roles.fs`：只保留 `Role` DU 与 `roleLabel` 编解码。
  - 私有类型接口 `RoleCatalog` 从 `wanxiangli/identity` 导入。Foundation 是纯核心目录，禁止 JsInterop，所以只用 Import/Emit。
  - `all` 来自库 `activeRoleLabels`；`tryParseRole` 来自库 `tryParseHistoricalRole`，保留宿主一贯的宽解析，Journal 历史解码依赖它，活跃准入由 ParticipantIdentity 把关；`isInternal` 来自库 `isInternalRole`。
- `Participant/Persona/Catalog.fs(.fsi)`：新增 internal `IdentityLibrary`。`Persona.ofCanonical` / `tryParse`、`PersonaCatalog.personaV1` / `persona` / `bookkeeperPersona` 全部由库回答。
- `Participant/Persona/Identity.fs`：`ParticipantIdentity` 成为库的类型化投影。
  - `inputView` / `evidenceView` 交给库，`evidenceOfView` / `kindOfLabel` / `errorOfView` 映射回 F#；`fromInput` 等于库 `restoreRecorded`。
  - 宿主只保留 legacy V1 适配：HumanRoot 走 resolveAtRoot，AgentOwnerRoot 判为 Unprovable。
  - `.fsi` 未改，所有调用方零改动。
- `Participant/Persona/ManagedCatalog.fs`：`legacyAgentNames` / `isLegacyAgentName` 来自库。

### 11.3 旧符号去向（G）

| 旧实现 | 去向 |
|---|---|
| `Roles.all` 活跃列表、`isInternal` 判定表 | 删除，改由库 `activeRoleLabels` / `isInternalRole` 回答 |
| `Roles.tryParseRole` 的大小写/历史解析 | 删除，改由库 `tryParseHistoricalRole` 回答，宿主只做 label→DU |
| `Persona` 名称解析、`PersonaCatalog` V1 Role→Persona 表、bookkeeper 人格 | 删除，改由库 `tryParsePersona` / `personaOfRole` / `bookkeeperPersona` 回答 |
| `ParticipantIdentity` resolve/inherit/rehydrate/fromInput 校验算法及名称分类 | 删除，改由库 `resolveAtRoot` / `inheritFromOwner` / `rehydrate` / `restoreRecorded` 回答；宿主只保留 F# 类型投影与错误映射 |
| `ManagedCatalog` legacy 名单 | 删除，改由库 `legacyNames` / `isLegacyName` 回答 |
| 保留在宿主 | `Role`/`Persona`/错误 DU 编解码；Host 呼名表 `tryParsePersonaCallingName`（宿主 UI 词汇）；legacy V1 记录适配（历史数据读取，未重写历史） |

只有一个可写 owner，没有同步器。旧测试无需删除：participant-identity 等 WHAT 套件原样运行，现在测试的是库回答。

### 11.4 验收（§9）

证据见 [evidence/CUT-07](../evidence/CUT-07/README.md)。

- 14 个相关套件 base `959f05643` 与 candidate 计数相同，既有失败用例名 diff 为空。
- check.mjs 无新增发现，report-only 审计 255 → 253。
- 差分 509,617 次双算只有 1 处不同：`null` 角色名从抛错变为空权限，fail-closed。
- 净改进：身份规则只剩一个 owner；宿主少约 200 行判定逻辑；行为保持；权限未扩大；历史读取未变。

### 11.5 移交相邻卡（未做，范围明确）

- 宿主 `Interaction/Authority/Origin.fs` 对接库 `classifyOrigin` 归 **CUT-09**：库的 continuation 词汇与宿主现行词汇不一致，需在 CUT-09 统一。D-01 护栏两侧都没有 human 回退。
- `IdentitySeed` 组合归 **CUT-09**。
- 宿主 `SessionExecutionBinding` 对接库 `bindExecution` / `rebindTarget` 归 **CUT-10 / CUT-12**。

### 11.6 复核（2026-10-21，eng-cut07b-goose 接手）

- 接手时两提交已位于 `origin/master` `1c90ecf69` 之上（merge-base 即 origin/master），工作树无源码改动。
- 在含 `fix(CUT-07)` 的 HEAD 上重新完整构建（`node scripts/build.mjs` exit 0），重跑 participant-identity、capability-enforcement、session-ontology、interaction-authority、office-capability、managed-chat-execution：计数与 §2 表一致，fail/cancelled 为 0。
- 差分重跑：compared 509617; differing 1（仍为 §11.4 的 `null` fail-closed 差异）。
- `node scripts/check.mjs` 重跑：exit 1（既有），对 base 去路径去行号后无新增发现；shard-edge-audit 253。
- delegation / managed-session-lifecycle / work / dispatch-protocol 未在含 fix 的 HEAD 上重跑（fix 只把 `Roles.all` 的动态属性访问换成类型化接口调用，产物等价）；它们最近一次 candidate 运行的结果见 evidence §2。
