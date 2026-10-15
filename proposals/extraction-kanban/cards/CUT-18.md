# CUT-18：规范事实编码与校验（含 D-04 护栏）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-18) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`DOING`（2026-10-15 由 eng-cut18 认领；源码施工完成，运行验证待 DevOps，见 §10 施工记录）
- **所属泳道 / 波次**：存储 / W1 独立卡
- **切换前置**：`CUT-00`（已完成，见 [CUT-00](CUT-00.md)）
- **强制护栏（D-04）**：**`wanxiangli/eventstore`（`SqliteJournal` 单流 SQLite CAS）与主仓多 writer NDJSON 规范事实协议及合法分叉（`DomainConflict`）分属两种不同承诺。** 本卡只抽取和冻结多 writer 事实协议的规范化编解码、内容寻址哈希与身份/图结构校验，**严禁用单流 SQLite CAS 抹杀多 writer 分叉，也严禁把 `Work`/`Trace`/`Ownership` 的专用顺序 record 强塞进 NDJSON `EventEnvelope`**。
- **零阻塞结论**：`CanonicalJson`、`EventEnvelope` 模型、`CanonicalEventCodec` 六大纯操作、`EventVocabulary` 契约及身份/DAG 纯校验器在主仓中已按 `durable-events-022/023` 拆成零副作用的纯 Contract 编译单元（`.fsproj`），无任何磁盘 I/O、进程锁或 Git 依赖，可零阻塞独立抽出至 `wanxiangli`（对应 `src/events` 草稿）。

## 2. 草稿概念名/路径与主仓真实模块/`.fsproj` 映射

委托说明及早期草稿中曾提及 `CanonicalJson`、`Envelope`、`EventHash`、`Validation` 等文件名。在主仓当前基线（`b322c180b`）中，这些职责分布在以下精确路径与编译单元中：

| 草稿概念 / 委托说明符号 | 主仓真实源码路径（含 `.fsi`） | 所属编译单元（`.fsproj`） | 去向判定 |
|---|---|---|---|
| `CanonicalJson`（键序递归升序、单行无 BOM JSON） | `src/Wanxiangshu/Foundation/CanonicalJson.fs(.fsi)` | `Wanxiangshu.Owner.dispatch-protocol.foundation-canonical-json.fsproj` | **抽出核心（A）**：Unicode 代码点（Ordinal）键序与紧凑序列化 |
| `Envelope` & `EventHash`（无版本信封模型与 `PayloadRef` 内容标识） | `src/Wanxiangshu/Persistence/EventStore/Model.fs(.fsi)`<br>`src/Wanxiangshu/Persistence/EventStore/AppendResult.fs(.fsi)` | `Wanxiangshu.Owner.durable-events.persistence-eventstore-model.fsproj`<br>`Wanxiangshu.Owner.durable-events.eventstore-append-result-contract.fsproj` | **抽出核心（A）**：7 字段无版本 `EventEnvelope`、`PayloadRef`、`EventStreamId`、`StorageInvalid`、`EventParents.canonicalize` 与 `PayloadRefs.canonicalize` |
| `CanonicalEventCodec`（规范编解码与同 ID 字节一致性校验） | `src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs(.fsi)` | `Wanxiangshu.Owner.durable-events.persistence-eventstore-canonicalcodec.fsproj` | **抽出核心（A）**：6 个公开纯操作（`encode`、`checkIdentity`、`mergeByIdentity`、`tryDecode`、`tryDecodeUtf8Text`、`tryDecodeUtf8`） |
| 词汇表契约与主仓权威词汇表装配 | `src/Wanxiangshu/Persistence/EventStore/EventVocabulary.fs(.fsi)`<br>`src/Wanxiangshu/Persistence/EventStore/AuthoritativeVocabulary.fs(.fsi)` | `Wanxiangshu.Owner.durable-events.eventstore-event-vocabulary-contract.fsproj`<br>`Wanxiangshu.Owner.durable-events.eventstore-authoritative-vocabulary.fsproj` | **A 接收显式 `isEventTypeKnown: string -> bool` 谓词**；主仓各业务域聚合表 `AuthoritativeVocabulary.fs` 留在主仓组合层（C） |
| `Validation`（DAG 父引用、环检测、同 ID 碰撞与 `ProjectionCutTail` 契约） | `src/Wanxiangshu/Persistence/EventStore/IntegrationKernel.fs(.fsi)`<br>`src/Wanxiangshu/Persistence/EventStore/IntegratorEngine.fs(.fsi)` | `Wanxiangshu.Owner.durable-convergence.eventstore-integrator-contract.fsproj`<br>`Wanxiangshu.Owner.durable-convergence.eventstore-integrator-engine.fsproj` | **纯校验函数归本卡（A）**；多 writer 段文件读写、k-way merge、Git 同步与 `LocalEventStore` 归 `CUT-19` |

## 3. 目标

- **用户/维护者当前损失**：`wanxiangli/src/events` 仍为草稿，若不先冻结多 writer NDJSON 事实协议的精确字节规范、SHA-256 内嵌载荷闭包、同 ID 碰撞拒绝与显式词汇表门禁，后续 `CUT-19`（多 writer 存储与收敛）施工时极易发生编码漂移、把合法并发分叉误判为损坏，或把未知/退役事件静默跳过。
- **这次完成后可少维护什么**：在 `wanxiangli/events` 提供唯一的无版本 `EventEnvelope` 规范编解码器与纯校验器；主仓 `CanonicalEventCodec`、`Model` 与 Integrator 校验层直接复用同一实现，彻底消除同义 codec。
- **明确不做**：
  - 不强行把 `CUT-01`（`Work`）、`CUT-03`（`Trace`）、`CUT-04`（`Ownership`）的 SQLite 专用记录改版为 NDJSON `EventEnvelope`（遵守 **D-04**）；
  - 不在本卡内实现物理文件追加、64 MiB 段滚动、文件锁、k-way 合并或 Git Hook 远端同步（全部归 `CUT-19`）；
  - 不在通用库内硬编码主仓业务事件名（如 `Strength`、`Casebook`、`Sphinx` 事件名），词汇表合法性通过显式传入的 vocabulary 集合或谓词判定。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/durable-events/WHAT.md`：`[002]` `EventEnvelope` 无版本与 additive vocabulary、`[003]` canonical JSON 是 identity 协议（同 ID 异字节触发 Identity Collision fail-closed，同 ID 同字节幂等去重）、`[004]` 完整 NDJSON 行（单行 JSON + LF，无 BOM）、`[007]` `StorageInvalid` 全局 fail-closed、`[008]` 并发 fork（`DomainConflict`）不升级为 `StorageInvalid`、`[012]` `PayloadRef` SHA-256 与内嵌 `payloads` 闭包校验、`[022]` EventStore contract/runtime 编译闭包单向有界、`[023]` `CanonicalEventCodec` 六大操作纯契约分层、`[027]` 退役生命周期词汇保持 fail-closed 拒绝。
  - `requirements/durable-convergence/WHAT.md`：`[001]`—`[005]` 因果父边、DAG 拓扑校验与并发分叉保留。
- **新库草稿差异及裁决（D-04）**：
  - `wanxiangli/src/events` 草稿不得合并或改写 `wanxiangli/eventstore` 的 `SqliteJournal`，也不得以 SQLite 单流序号取代多 writer 的 `EventId` + `Parents` DAG 与 canonical 字节身份协议。两套协议各自对应明确的存储承诺。

## 5. 自治个体（A / B / C）

- **A（`CanonicalEventCodec` & `EnvelopeValidator`，抽入新库 `wanxiangli/events`）**：
  - 负责：
    1. `EventEnvelope` 规范化（`Parents` 与 `PayloadRefs` 去重并按 Ordinal 升序排序，内嵌 `Payloads` 键按 Ordinal 升序排序）；
    2. Canonical JSON 编码（UTF-8 无 BOM、所有对象键按 Unicode 代码点升序递归排序、紧凑单行、末尾单个 `
`）；
    3. 严格解码与往返字节等价校验（任何多余字段如 `version`/`schema_version`、非规范键序、空白漂移、UTF-8 解码错误、base64 损坏、`PayloadRef` SHA-256 失配或缺失内嵌载荷均返回 `Error StorageInvalid`）；
    4. 跨事件纯校验：`checkIdentity` / `mergeByIdentity`（相同 `EventId` 字节相等则去重，字节不等则报 `StorageInvalid` 身份碰撞）、显式词汇表检查（未知或已退役 `event_type` 立即报 `StorageInvalid`）、retained 集合内缺失父事件与三色 DFS 环检测（合法多头并发分叉返回正常多 head，绝不报 `StorageInvalid`）。
  - 拥有状态：纯无状态函数与不可变值类型。
- **B（各业务领域 Fact Codec 与 Fold Owner，留在主仓或各领域卡）**：
  - 负责：将各自领域的强类型事实编码为 `EventEnvelope.Payload`，并定义各自的 `IntegrationRule`。
  - 与 A 不共享可变值：B 不自行拼接 NDJSON 行，也不绕过 A 解析原始字节。
- **C（主仓 `AuthoritativeVocabulary`、`IntegratorEngine` 与 `LocalEventStore` 组合层，与 `CUT-19` 衔接）**：
  - 负责：汇总主仓当前合法的权威事件词汇表（同时排除 `durable-events-027` 已退役的 9 类旧生命周期事件）注入 A；在物理读写与合并边界调用 A 完成校验。

## 6. 公开消息与最小完整契约

- **核心类型**：
  - `PayloadRef`（64 位小写 hex SHA-256 内容寻址引用）、`EventStreamId`、`EventEnvelope`（7 个固定字段：`event_id`、`stream_id`、`event_type`、`parents`、`payload`、`payload_refs`、`payloads`）、`StorageInvalid`。
- **六个核心公开操作（`durable-events-023`）**：
  - `encode : EventEnvelope -> string`
  - `checkIdentity : EventEnvelope -> EventEnvelope -> Result<unit, StorageInvalid>`
  - `mergeByIdentity : EventEnvelope list -> Result<EventEnvelope list, StorageInvalid>`
  - `tryDecode : string -> Result<EventEnvelope, StorageInvalid>`
  - `tryDecodeUtf8Text : byte[] -> Result<string, StorageInvalid>`
  - `tryDecodeUtf8 : byte[] -> Result<EventEnvelope, StorageInvalid>`
- **成功结果及其精确保证**：
  - `encode` 对任意合法信封产生唯一确定性的单行 JSON + LF 字符串；
  - `tryDecode` / `tryDecodeUtf8` 仅当输入字节与规范重编码结果逐字节全等、7 个字段齐全且无额外字段、所有 `payload_refs` 均存在于 `payloads` 且 SHA-256 校验通过时返回 `Ok envelope`；
  - `mergeByIdentity` 对相同 `EventId` 且 canonical 字节完全一致的重复事件幂等去重，保留首次出现顺序。
- **拒绝结果及零副作用保证**：
  - 任何非规范格式、身份碰撞、载荷哈希失配、未知/退役 `event_type`、retained 内部断父或成环依赖，一律返回 `Error StorageInvalid`，零静默跳过、零自动裁剪坏尾。
- **公开 `.d.ts` 与 Fable 交付形状**：
  - 在 `wanxiangli/events` 公开清晰的 ESM 函数与 `.d.ts` 类型声明，不带入 Node `fs` 或 `child_process`。

## 7. 主仓调用方清单与对应测试套件

### 7.1 主仓调用方清单

1. **EventStore 存储、合并与同步核心**：
   - `src/Wanxiangshu/Persistence/EventStore/IntegratorEngine.fs`
   - `src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fs`
   - `src/Wanxiangshu/Persistence/EventStore/Merge.fs`
   - `src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs`
   - `src/Wanxiangshu/Persistence/EventStore/Sync.fs`
   - `src/Wanxiangshu/Persistence/EventStore/LocalEventStore.fs`
   - `src/Wanxiangshu/Persistence/EventStore/Surface.fs`、`StrengthSurface.fs`
2. **业务持久化写入与折叠适配**：
   - `src/Wanxiangshu/Persistence/Journal/{EventStoreJournalWriter,AgentJournal,PromptFactCodec,Surface,ObligationSurface}.fs`
   - `src/Wanxiangshu/Composition/Durable/{Fact,Fold,Projection,JournalIntegrationRule}.fs`
   - `src/Wanxiangshu/Repository/Knowledge/Casebook/{Store,IntegrationRules}.fs`
   - `src/Wanxiangshu/Repository/Programming/Js/{TransactionStore,IntegrationRules}.fs`
   - `src/Wanxiangshu/Strength/{IntegrationRules,Migration,Persistence/DurabilityPort}.fs`
   - `src/Wanxiangshu/Sphinx/V2/Integration.fs`
   - `src/Wanxiangshu/Mission/Planning/EventStore.fs`
   - `src/Wanxiangshu/OpenCode/Host/WorkspaceEventStore.fs`

### 7.2 对应测试套件

- `requirements/durable-events/tests/002.test.mjs`（无版本信封与 additive vocabulary）
- `requirements/durable-events/tests/003.test.mjs`（Canonical JSON 与 Identity Collision）
- `requirements/durable-events/tests/004.test.mjs`（完整 NDJSON 行原语）
- `requirements/durable-events/tests/007.test.mjs`（`StorageInvalid` 全局 fail-closed）
- `requirements/durable-events/tests/008.test.mjs`（合法并发 fork 不升级为 `StorageInvalid`）
- `requirements/durable-events/tests/012.test.mjs`（`PayloadRef` 与本地内嵌 payload 闭包）
- `requirements/durable-events/tests/022.test.mjs`、`023.test.mjs`（Contract/Runtime 编译闭包有界与 `CanonicalEventCodec` 分层）
- `requirements/durable-events/tests/027.test.mjs`（退役生命周期词汇 fail-closed 拒绝）

## 8. 必须能红的最小反例与拆除测试设计

1. **反例 1（同 ID 异字节碰撞 vs 同 ID 同字节幂等）**：构造两个 `EventId` 相同但 `Payload` 或 `Parents` 不同的信封调用 `checkIdentity` / `mergeByIdentity`，必须返回 `Error StorageInvalid`（含 `identity collision`）；而两个完全相同的信封必须幂等合并为 1 条（对应 `durable-events-003`）。
2. **反例 2（键序漂移、BOM、额外 version 字段或半行）**：将合法 canonical 行的对象键顺序颠倒、前置 UTF-8 BOM、注入 `"version":1` 字段或去掉末尾 `
`，调用 `tryDecode` / `tryDecodeUtf8`，必须全部拒绝为 `Error StorageInvalid`（对应 `durable-events-002/003/004`）。
3. **反例 3（内嵌 Payload 缺失或 SHA-256 篡改）**：在 `payload_refs` 中声明某引用但不在 `payloads` 提供对应字节，或篡改 `payloads` 中 base64 编码的 1 个字节使其与 SHA-256 引用名不符，解码必须立即失败为 `StorageInvalid`（对应 `durable-events-012`）。
4. **反例 4（未知/退役事件名、缺失父事件、成环 vs 合法并发分叉）**：
   - 输入含退役事件名（如 `WorkOpened`、`IncumbencyOpened`）或未知 `event_type`，校验器必须以 `unknown durable event type during replay: <event_type>` fail-closed（对应 `durable-events-027`）；
   - 输入在 retained 集合内引用不存在的 parent 或 `A -> B -> A` 成环，必须报 `StorageInvalid`（对应 `durable-events-007`）；
   - 两个 writer 从同一父事件分叉出两条合法子事件，校验必须通过并保留双 head，不得误报 `StorageInvalid`（对应 `durable-events-008` 与 **D-04**）。
5. **独立拆除测试**：在 `wanxiangli` 中独立编译 `events` 子包（零磁盘 I/O、零 SQLite、零 Git），验证全部 6 个 `CanonicalEventCodec` 操作与上述 4 组反例全绿；并在主仓跑 `durable-events/tests/022.test.mjs` 与 `023.test.mjs` 确认编译闭包预算（Contract ≤ 100 production `.fs`）不退化。

## 9. 精确写集合与暂留/相邻卡边界

- **库仓（`wanxiangli`）写集合**：
  - `src/events/*` 及对应工程/导出声明（实现 `CanonicalJson`、`EventEnvelope`、`PayloadRef`、`EventStreamId`、`StorageInvalid`、`CanonicalEventCodec` 与纯 DAG/词汇表校验函数）。
- **主仓（`wanxiangshu`）切换期写集合**：
  - `src/Wanxiangshu/Foundation/CanonicalJson.fs(.fsi)`
  - `src/Wanxiangshu/Persistence/EventStore/Model.fs(.fsi)`
  - `src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs(.fsi)`
  - `src/Wanxiangshu/Persistence/EventStore/EventVocabulary.fs(.fsi)`
  - `src/Wanxiangshu/Wanxiangshu.Owner.dispatch-protocol.foundation-canonical-json.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.durable-events.persistence-eventstore-model.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.durable-events.persistence-eventstore-canonicalcodec.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.durable-events.eventstore-event-vocabulary-contract.fsproj`
- **暂留主仓与相邻卡边界**：
  - `src/Wanxiangshu/Persistence/EventStore/AuthoritativeVocabulary.fs(.fsi)`（聚合主仓 Strength/Casebook/JsTransaction/Sphinx/Agent/Host 词汇表）留在主仓；
  - `src/Wanxiangshu/Persistence/EventStore/{ProcessEventLog,Merge,Sync,LocalEventStore,IntegratorEngine,CanonicalIntegrator}.fs(.fsi)` 的物理段文件追加、k-way 合并、Git Hook 同步与运行时积分器归 `CUT-19`。

## 10. 施工记录 — 2026-10-15（eng-cut18，源码层面）

### 已完成（源码）

**库仓 wanxiangli（0.5.2 → 0.6.0）**：

- `src/events/CanonicalJson.fs`：码点序规范化 JSON（含稀疏数组空洞 null）。
- `src/events/Envelope.fs`：内部 `Envelope` 值与 `normalize`（Parents/PayloadRefs 去重排序）。
- `src/events/CanonicalEventCodec.fs`：`CanonicalEventCodec` 类，8 个纯操作（§6 的 6 个 + `canonicalJson` + `validate`）。`validate` 为显式词汇谓词 + 身份去重 + 父闭包 + 三色 DFS 环检测 + 内联载荷 sha256 闭包的完整纯校验；合法分叉返回多 head，不报 `StorageInvalid`（D-04）。
- `src/events/Wanxiangli.Events.fsproj`：独立编译单元，零 ProjectReference（build.mjs 自动发现，无专用流程）。
- `src/events/index.d.ts`：ESM 类型声明，含 `CodecError` 判别联合。
- `src/events/tests/{002,003,004,007,008,012,027}.test.mjs`：§7.2 七套契约的库侧镜像（WHAT 锚点同名），覆盖 §8 反例 1–4。
- `package.json`：0.6.0、`./events` 导出、files、test 入口。
- 文档：`src/events/AGENTS.md`（SPEC 填证）、`src/AGENTS.md` TODO 表、`施工顺序.md` 第五刀。

**主仓 wanxiangshu**：

- `src/Wanxiangshu/Foundation/CanonicalJson.fs`：删除内联编码算法，改为 `[<Import("CanonicalEventCodec", "wanxiangli/events")>]` 委托；`withoutKeys` 保留本地实现（键剔除是 dispatch metadata 辅助，不属于 §6 六操作契约）。
- `src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs`：删除全部内联 Emit 算法（约 180 行 → 约 120 行 facade），改为同一导入的 F# 类型投影（`EventEnvelope` ↔ JS event 对象、`StorageInvalid` ← `CodecError` 判别映射）。`.fsi` 签名不变，§7.1 全部调用方零改动。
- `Model.fs`、`EventVocabulary.fs`、四个 `.fsproj` 未改：类型面与编译闭包不变（022/023 预算不受影响）。

### 待 DevOps 验证（本卡未运行任何命令）

1. wanxiangli：`npm ci --ignore-scripts && npm test`（全量，含新增 events 7 套）；`npm run build -- events` 单独编译。
2. `npm pack` 产出 `wanxiangli-0.6.0.tgz`，按 README §8 放入主仓 `vendor/` 并更新主仓 `package.json` 依赖行与 lock（vendor/package 元数据归 CUT-33，此处属本卡接线的必要物理步骤，需协调者确认归属）。
3. 主仓：`node scripts/build.mjs` + `npm run format-build-test`；重点 `requirements/durable-events/tests/{002,003,004,007,008,012,022,023,027}.test.mjs`。
4. 主仓 `git status`/diff 复核（本轮未执行 git）。

### 边界与遗留

- **依赖切换的物理步骤未完成**：主仓 `package.json` 仍指向 `vendor/wanxiangli-0.5.2.tgz`（无 `./events` 导出）。facade 源码已切换，但主仓构建在 0.6.0 tarball 落入 vendor 并更新依赖行之前必然失败——这是有意的源码先行，物理集成由 DevOps/协调者按上述第 2 步完成。
- **`validate` 的主仓消费**：库侧 `validate` 已就绪，但 `IntegratorEngine.validateLocalHistory` 的词汇/闭包检查本轮未切换（该文件归 CUT-19 编辑面；其 boot 错误消息形状被 027 测试锁定）。同义校验逻辑在库与 IntegratorEngine 中暂并存，收敛归 CUT-19。
- **D-04 遵守**：库内零 SQLite、零文件 I/O、零 Git；`wanxiangli/eventstore`（SqliteJournal）未触碰；合法分叉在 `validate` 中是正常多 head（008 测试钉住）；多 writer 收敛协议未在本卡表达任何一行。
