# Proposal：fork 增加可选参数 `attach`

**Status:** Proposed（由用户明确要求创建；尚未 Active，禁止实现）
**Scope:** Manager `fork` 工具面 + `ForkChildPayload` 附件渲染 + prose + docs / proof / gates / tests
**Compatibility:** 纯增量。`fork(calling?, name, charge, keywords?)` 形状不变，只追加可选 `attach`。`commission` 不动。旧 `fork-clone` 提案已撤销（无 clone / Host fork / 顶层 session）。
**Proposed file:** `changes/proposed/fork-attach.md`

---

# 0. 用户已冻结的裁决

```text
fork(calling?, name, charge, keywords?, attach?)
  attach  可选  X = 另一个 session 的 Byname（本 mission 内已 fork 的 person）

  语义：首 prompt 附上 X 的 canonical LWR（includeOpening=true，即「父→子版本」，
        含 Opening / Chronicle / Recent work）。
        X 的 LWR 是附件：可能对本次工作有价值，但不改变本次工作内容（charge）。
```

---

# 1. 现状（调研）

## 1.1 fork 首 prompt 的组装（`ForkChildPayload.fs` + `HostForkAgent.fs`）

现有首 prompt 由 `ForkChildPayload.relay` 渲染，含四类输入：

```text
Assignment            charge（任务，唯一决定「什么该由你承担」）
CommissionerRecord    parent 的 LWR（includeOpening=true）作背景
RootRequirements      Reviewer 专用 HumanRoot 文本（非 Reviewer 恒空）
Payload               可选机器可读数据（SyntheticToml content 字段）
```

`commissioner_record` 的 prose 已经明确「那是委派人的历史，不是你的；看得见≠变成你的」（`delegation/fork-child-commissioner-record`）。附件必须复用同一精神，但指向**第三方** person。

## 1.2 LWR 物化（`LifecycleWorkRecordProjection.fs`）

```text
lifecycleWorkRecord journal sessionId includeOpening
  includeOpening=true  → 父→子版本：渲染 Opening / Chronicle / Recent work
  includeOpening=false → 子→父版本：Opening 不回传
  Opening 未 capture → None（LWR 未定义）
```

用户指定「包括 Opening，也就是父→子版本」→ 用 `includeOpening=true`。渲染段标题 `Opening / Chronicle / Recent work`（GLORY-025）。

## 1.3 Byname → session 解析

`HandleProjection.tryFindByByname`（parent 自己的 handle projection）→ `ChildSessionId`。因此 `attach` 只能指向本 mission 内**已 fork 的 person**，不能指向任意 Host session（这是自然边界：Manager 只能附上自己认识的人的历史）。

---

# 2. 新语法与渲染

```text
fork(..., attach="Ada"):
  sourceHandle = tryFindByByname "Ada"            // 不存在 → 自然语言错误
  record = lifecycleWorkRecord journal sourceHandle.ChildSessionId true   // 父→子版本
  record = None（Opening 未 capture）→ 附件为空（见 §4 待裁决）
  渲染进首 prompt，作为独立「附件」段，区别于 commissioner_record
```

`ForkChildPayload` 增 `Attachment: string option`（或独立 prose 路径 + 数据），渲染顺序在 `commissioner_record` 之后、`RootRequirements` 之前：

```text
Assignment
Base
CommissionerRecord（parent LWR；现状不变）
Attachment（X 的 LWR；新增，明确「附件」框定）
Requirements
```

新增 prose：`delegation/fork-child-attachment/{en,zh-CN}.md`，框定语必须同时满足：

- X 的 LWR 是**附件**（背景材料），可能对工作有价值；
- 它**不改变**你的 charge / 工作内容；
- 附件里的未竟工作不因此变成你的义务（对齐 commissioner_record 的「看得见≠变成你的」）；
- 不泄漏 X 的 session id / agent id / 机器身份（EXEC-030）。

ARCH-010 指令/数据分离：附件属背景数据，不进入 `Assignment`；与 `Payload` 的 machine-readable 字段分离（附件是 prose 段，不是 SyntheticToml 数据表）。

---

# 3. 解析与边界

| 场景 | 行为 |
|---|---|
| `attach` 缺省 | 现状，无附件 |
| `attach` Byname 不存在 | 自然语言错误（对齐 PersonUnknown 风格），不投影机器身份 |
| `attach` 解析成功但 LWR=None（Opening 未 capture） | 见 §4 待裁决：静默省略 / 显式「该 person 尚无工作记录」 |
| `attach` = 本次被 fork/被续做的 name（自附） | 拒绝或 no-op（见 §4） |
| `attach` 指向 retired/abandoned person | LWR 仍可物化（Journal 有），允许；只读，无副作用 |
| busy nudge（续做现有 active run） | 不物化附件；仅在开启新 work unit 时 attach（对齐 EXEC-032 warm-start 纪律） |

`keywords` 的 AGENT-032 role gate **不套用于 `attach`**：attach 只读他人 LWR，任何 forkable role 均可受益；但不排除后续按需加 gate（见 §4）。

---

# 4. 待裁决（实现前冻结）

1. **附件为空（LWR=None）**：静默省略
2. **自附**（attach == 本次 name）：拒绝报错
3. **是否允许多个 attach**：单 `attach: string`（用户裁决为单参数）
4. **`attach` 的 prose 段标题命名**：正式术语 `attached_work_record`；Glossary 需登记
5. **Reuse 路径是否也吃 `attach`**：吃

---

# 5. 范围 / 非目标

**做：**
- `fork` 增可选 `attach`（schema + decode + 解析 + 渲染）。
- `ForkChildPayload` 增附件输入 + prose 路径 + 框定语（两 locale）。
- `ForkTool` / `HostForkAgent` 传附件物化文本。
- docs（why/what/shape/how/proof）、Glossary、tool arg prose、language-parity、unit + 契约测试。

**不做：**
- clone / Host `session.fork` / 顶层 session（已撤销的 fork-clone 提案全部废弃）。
- `commission` 改动。
- `attach` 指向非 child 的任意 session、多个 attach、改变 charge 语义。
- 复制 X 的 Wanxiangshu Journal 内部投影（Magic Todo / review 等）——attach 只物化 canonical LWR 文本。

---

# 6. 受影响条款与文件（blast radius）

## 6.1 正式条款

- `EXEC-002`（Fork 语义）：追加可选 `attach` 参数语义。
- `AGENT-032`（warm-start gate）：明确 attach 不受 keywords gate 约束，或补 gate（§4）。
- `EXEC-030`（provider leak）：附件只投影 LWR prose，不投影 session/agent/机器 DTO。
- `ARCH-010`（指令/数据分离）：附件属背景数据，不混入 Assignment。
- `COMPANION-003` / `GLORY-025`（LWR 唯一物化器 + 渲染段）：attach 复用 `lifecycleWorkRecord includeOpening=true`，禁止第二 renderer。
- Glossary：`attach` / attachment 词条。

## 6.2 实现文件

- `src/Wanxiangshu/Domain/ForkChildPayload.fs`（Attachment 输入 + render）
- `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ForkTool.fs`（schema + 解析 byname→LWR）
- `src/Wanxiangshu/Session/HostForkAgent.fs`（附件传参进 relay）
- `resources/provider/delegation/fork-child-attachment/{en,zh-CN}.md`（新增）
- `resources/provider/tool/fork/arg-attach/{en,zh-CN}.md`（新增 arg 描述）
- `scripts/checks/semantic-anchors.mjs` + `scripts/checks/language-parity-gate.mjs`（attach arg 两 locale anchor）
- `tests/unit/tools/fork-tool.test.mjs` + `tests/integration/plugin/manager-tool-contract.test.mjs`（attach 解析/渲染/未知名/LWR 为空/自附/busy）

## 6.3 门禁

- Gate A：`fork` schema owner 唯一；`attach` 不引入别名。
- language-parity：`attach` 描述 + attachment prose 两 locale 同 anchor。
- `kolmogorov-size-baseline.json`（ForkTool.fs / ForkChildPayload.fs 行数上限）。

---

# 7. 最小可验证闭环（实现顺序草案）

```text
1. ForkChildPayload: Attachment 输入 + prose 路径 + 渲染
2. ForkTool: attach 参数 decode + byname→LWR 解析 + 错误文案
3. HostForkAgent: 附件传参进 relay
4. prose + arg 描述 + semantic-anchor / language-parity 同步
5. docs why→what→shape→how→proof 全链 + 单测 + 契约测试
```

---

# 8. 一句话

`fork` 追加可选 `attach: X`：把本 mission 内另一 person X 的 canonical LWR（includeOpening=true，父→子版本）作为**背景附件**注入首 prompt——它可能对本次工作有价值，但不改变 charge，也不把 X 的未竟工作变成被 fork 人的义务。
