# Proposal：fork 工具改为 cloneFrom / cloneTo 语法

**Status:** Proposed（由用户明确要求创建；尚未 Active，禁止实现）
**Scope:** Manager `fork` 工具面 + Host 会话克隆接面 + 持久化 lineage + docs / proof / gates / tests
**Compatibility:** `fork` 工具名保留；`commission` 不改（用户裁决）。旧 `calling?/name/charge` 形状整体替换为 `cloneFrom/cloneTo/charge/keywords?`，不保留双轨 alias。
**Proposed file:** `changes/proposed/fork-clone.md`

---

# 0. 用户已冻结的裁决

```text
fork(cloneFrom, cloneTo, charge, keywords?)
  cloneFrom  必填  通用名（Office/persona）或具体名（既有 Byname）
  cloneTo    必填  新名字（新建 / 克隆新建时的 Byname）
  charge     必填  任务（语义不变）
  keywords   可选  语义不变

  通用名        → 新建 person（现状 calling 路径）
  具体名        → 基于该现有 session 克隆新建（新能力）
  cloneFrom == cloneTo → 原地复用（现状 Reuse 路径）
```

- 工具名 **保留 `fork`**（用户裁决）；仅改参数形状。
- **仅 `fork`**（用户裁决）；`commission` 保持 `calling?/name/charge` 不动。

---

# 1. 现状调研

## 1.1 Manager `fork` 现状（`ForkTool.fs`）

```text
managerSpec  args = [ calling(optional enum), name(required), charge(required), keywords(optional) ]
executeManager:
  hasCalling → tryCalling(managerCallingBindings) 解析 Office
              → runtime.Fork(handleId, role, managed.Name, charge, byname=name)   // 新建
  else       → tryFindByByname name
              → runtime.Reuse(agentId, charge)                                     // 复用
```

- 通用名 = `PersonaCatalog.persona`（Coder/Engineer/Scout/Investigator/Technician/Operator/Navigator/Researcher/Analyst/Inquirer），大小写不敏感。
- 具体名 = `HandleProjection.tryFindByByname`（OrdinalIgnoreCase；retired 记录仍可命中，防止名字被静默回收）。
- `Fork` vs `Reuse`（`HostForkAgent.fs`）：
  - Fork：`CreateChildSession`（新 childId）→ `HandleLinked` → 首 prompt 注入 parent LWR 作 `commissioner_record`（`ForkChildPayload.relay`）。
  - Reuse：**同一 childId**，`linkNamed` 重开 Labor，同一 Host session 上新 work unit。

## 1.2 Host 原生 fork 能力（`../opencode`，关键调研结论）

`../opencode/packages/opencode/src/session/session.ts`：

```ts
readonly fork: (input: { sessionID: SessionID; messageID?: MessageID }) => Effect.Effect<Info, NotFound>
```

SDK：`client.session.fork({ sessionID, messageID? })`；HTTP：`POST /api/session/{sessionID}/fork`（`ForkPayload = { messageID? }`）。

实测语义：

```text
Session.fork(source):
  1. get(source)
  2. title = getForkedTitle(original.title)           // "…(fork #N)"
  3. createNext:
       directory/path/workspaceID = source 的
       metadata = structuredClone(source.metadata)
       无 parentID（顶层 session，不是 child）
       无 agent / permission / model
  4. messages(source) 逐条复制：
       - 新 ascending MessageID；assistant 的 parentID 重映射
       - 每个 part：新 PartID + 新 messageID + 新 sessionID；compaction tail_start_id 重映射
       - 若给 messageID 边界：msg.id >= messageID 处 break（不含边界，复制其之前全部）
  5. 返回新 Session.Info
```

结论：Host 已有**字节级 transcript 复制**原语。`cloneFrom=具体名` 的「克隆新建」直接映射到 Host `session.fork`，不是万象术侧拼 WorkRecord。

关键差异点（必须写进 docs / 实现门禁）：

| 事实 | 现状 CreateChildSession | Host fork |
|---|---|---|
| parentID | 物理挂 family root（HOST-015） | **无 parentID（顶层）** |
| agent | 带 `fast-*` 绑定 | **不带**（后续 SendPrompt 由 opts.Agent 携带） |
| permission/model | 由 Host 默认 | 不复制 |
| transcript | 空 | 逐字节复制 source 全历史 |
| title | byname/agent | `(fork #N)` 后缀 |
| metadata | — | structuredClone |

---

# 2. 新语法与三路分派

```text
fork(cloneFrom, cloneTo, charge, keywords?):
  A. cloneFrom = 通用名（命中 PersonaCatalog，大小写不敏感）
       → 现状「新建」：CreateChildSession + HandleLinked(Byname=cloneTo)
  B. cloneFrom = 具体名 ∧ cloneFrom == cloneTo
       → 现状「复用」：同一 childId，Reuse(agentId, charge)
  C. cloneFrom = 具体名 ∧ cloneFrom != cloneTo
       → 新「克隆新建」：
           sourceHandle = tryFindByByname(cloneFrom)
           childId = Host session.fork(sourceHandle.ChildSessionId)
           HandleLinked(新 agentId, Byname=cloneTo, role=sourceHandle.CanonicalRole,
                        TargetAgent=sourceHandle.TargetAgent, ChildSessionId=childId,
                        携 clone lineage)
           首 prompt = 新 charge 的 work unit（复用 Reuse 的 send 语义）
```

- `charge` / `keywords?` 语义与现状完全一致（`keywords` 仍受 AGENT-032 warm-start role gate）。
- 成功后果文案保持「`<cloneTo>` carries this charge now.」；不投影 agent_id / role / tier / session / lineage / 机器 DTO（EXEC-030）。

---

# 3. 待裁决的设计点（实现前必须逐条冻结）

## 3.1 clone 的物理语义 = Host transcript 字节复制（已倾向）

`cloneTo` 继承 source 的**完整 provider 历史**（opening + tool calls + results），再追加本次 `charge` 作新 work unit。
**否决**「只注入 source LWR 作 commissioner_record」：Host 原生 fork 已提供更完整、更便宜的 transcript 复制；前者是弱化近似。用户「基于某个现有 session 克隆新建」明确指向 session 级复制。

## 3.2 Host fork 产生顶层 session，破坏 HOST-015 family-root 物理父子

Host fork 的 session 无 parentID。影响：

- `InjectedSessionPort.registerChild` 不会登记它；`FamilyRootOf` / `AbortChildren` / Host `ListChildren` 都看不到它。
- 万象术恢复靠 Journal（`HandleLinked.ChildSessionId`）而非 Host parentID，**能**重绑；但 HOST-015「每个 managed child 物理挂 family root」被打破。

**需裁决其一：**
- (a) 接受「clone 是顶层 session」，在 docs 明确 HOST-015 的 clone 例外 + 在 Journal 记录 lineage 以自恢复；
- (b) fork 后再 `CreateChildSession` 兜底重挂 parentID（Host fork 无此参数，需额外 Host 支持 / `update` 无 parentID 字段，当前不可行）——**实测不可行**；
- (c) 要求 Host 给 fork 增加 parentID 入参（改 `../opencode`，跨仓协作，成本高）。

倾向 (a)：Journal lineage 自恢复，放弃物理 parentID，明确 docs 例外。

## 3.3 clone 不复制 `agent` / `permission` / `model`

Host fork 后 session 无 agent 绑定。万象术 clone 的 role/tier 由 `sourceHandle.CanonicalRole` / `TargetAgent` 确定，首 prompt 经 `SendPrompt` 的 `opts.Agent` 携带绑定（现状 `SendPrompt` 已带 Agent）。需 canary：fork 出的 session 首 prompt 后 Host 是否按 `opts.Agent` 稳定绑 agent，而非 defaultInfo 成 Deep。

## 3.4 持久化 lineage（恢复与审计必需）

`HandleLinked` 需携带 clone 来源，否则 crash 后无法证明「这个 childId 是 fork 出来的顶层 session」与 source 关系。

草案：`HandleLinked` 增可选 `ClonedFromSessionId` / `ClonedFromHandle`（或独立 `HandleCloned` fact）。字段只存稳定身份（source SessionId / source Handle），不存路径/时间。
恢复：`HostForkRestart` 按 ChildSessionId 重绑（现状机制），clone 的顶层 session 由 Journal 指向，不依赖 Host `children(parentID)`。

## 3.5 单字段 `cloneFrom` 的歧义裁决

一个字段同时承载「通用 Office 名」和「具体 Byname」。优先序需冻结：

- 倾向：**先按 PersonaCatalog 通用名匹配（大小写不敏感）**；未命中再 `tryFindByByname`。
- 冲突面：若某 Byname 恰好等于 persona 名（如「Coder」），将永远被判为通用名。**冻结**：创建时拒绝 Byname 与任一 persona 名大小写不敏感相等（扩充 `name-already-belongs` 检查为「byname 不得撞 persona 名」）。
- `cloneFrom == cloneTo == 未知名` → 复用路径报 `No continuing person is known by that name`（现状语义），**不**静默降级为新建。

## 3.6 源生命周期资格

- retired / abandoned source 是否可克隆？`tryFindByByname` 现在命中 retired（防名字回收）。克隆只读 source transcript，不动 source 生命周期。
- 倾向：**允许克隆 retired/abandoned source**（transcript 仍在 Host），但明确不可对 source 造成任何副作用。
- busy（active run）source：Host fork 只复制**已持久化** projected history，in-flight streaming turn 不保证进入 clone。冻结：clone 边界 = durable 投影；不等待 in-flight 收敛（或按 3.2 需明确）。

---

# 4. 范围 / 非目标

**做：**
- `fork` 参数形状 `cloneFrom/cloneTo/charge/keywords?`；三路分派 A/B/C。
- 新增 Host port `ForkSession(sourceSessionId)`（`IOpenCodePort` + SdkClientPort + HttpPort + `ISessionHostPort`/`InjectedSessionPort`）。
- Journal clone lineage（3.4）。
- 语义 docs（why/what/shape/how/proof）、tool description / arg prose、semantic-anchor / language-parity gate、unit + 契约测试。

**不做：**
- `commission` 改动（用户裁决保持现状）。
- `messageID` 边界暴露（Host 有，V1 不暴露给 provider；留作后续 Proposal）。
- 复制 Wanxiangshu Journal 内部投影（Magic Todo / review barrier 等）——clone 只复制 Host transcript，万象术侧状态从新 session 独立 fold。此点必须写进 docs，防止实现者误以为「clone = 全状态复制」。
- 工具名改名 `clone`；旧 `calling/name` alias 双轨；`fork-manager` 复活。

---

# 5. 受影响条款与文件（blast radius）

## 5.1 正式条款

- `EXEC-002`（Fork 语义）：`calling?/name/charge` → `cloneFrom/cloneTo/charge/keywords?`，新增 clone 分派。
- `AGENT-009/015`（managerForkableRoles / calling enum）：参数名迁移，enum 语义不变。
- `HOST-015`（family-root 物理父子）：记录 clone 顶层 session 例外（3.2）。
- `AGENT-032`（keywords warm-start gate）：clone 路径同样守 gate。
- `EXEC-030`（provider leak 禁令）：clone 不投影 lineage / session / machine DTO。
- `GLORY-068`（同 session 续做）：clone ≠ 续做，clone 是新 session；「同 session 续做」只由 cloneFrom==cloneTo 表达。
- Glossary：Byname / `fork` / 新增 `cloneFrom` / `cloneTo` 词条。

## 5.2 实现文件

- `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ForkTool.fs`（schema + 三路分派）
- `src/Wanxiangshu/Infrastructure/OpenCode/Host/OpenCodePort.fs` + `Sessions.fs`（`ForkSession` port）
- `src/Wanxiangshu/Session/HostForkAgent.fs`（clone fork 路径，或新 extension）
- `src/Wanxiangshu/Session/HandleController.fs` / `Journal/LinkageProjection.fs` / `Journal/ExecutionFactFold.fs`（lineage fact）
- `resources/provider/tool/fork/{description,arg-*}/en.md + zh-CN.md`
- `scripts/checks/semantic-anchors.mjs`（`create-and-continue` anchor 及 fork description anchor）
- `scripts/checks/language-parity-gate.mjs`
- `tests/unit/tools/fork-tool.test.mjs` + `tests/integration/plugin/manager-tool-contract.test.mjs` + 新增 clone 契约测试

## 5.3 门禁

- Gate A（工具 referential integrity）：`fork` schema owner 唯一；`calling/name` 旧字段在 wire 上消失。
- language-parity：`cloneFrom/cloneTo` 两 locale 描述同 anchor。
- `kolmogorov-size-baseline.json`（ForkTool.fs 行数上限）。
- 契约测试走真实 `session.fork`（canary），不 mock 私有路径。

---

# 6. 最小可验证闭环（实现顺序草案）

```text
1. port: IOpenCodePort/ISessionHostPort 增 ForkSession(sourceSessionId)
2. Journal: HandleLinked 携可选 clone lineage（向后兼容旧 fact）
3. ForkTool: cloneFrom/cloneTo 三路分派；prose/schema/gate 同步
4. HostForkAgent: clone fork 路径（Host fork + HandleLinked + 首 prompt send）
5. docs why→what→shape→how→proof 全链 + Gate A/parity + 单测 + 契约测试
6. canary: 真实 Host session.fork → clone session 历史字节一致 + agent 稳定绑定
```

---

# 7. 一句话

`fork` 从「calling 决定新建 / 省略 calling 决定续做」改为「cloneFrom 决定来源（通用名=新建，具体名=克隆新建），cloneFrom==cloneTo 决定原地复用」。克隆新建的物理实现是 OpenCode 原生 `session.fork`（字节级 transcript 复制）；万象术侧补 Journal lineage 自恢复与 provider 面零泄漏。
