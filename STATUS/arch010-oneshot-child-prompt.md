# 计划：One-shot / subagent 首 prompt 对齐 ARCH-010

状态：计划（未改代码）
范围：Inspector / Coder one-shot 子会话首 prompt；顺带盘点其他 subagent 合成面
条款：`ARCH-010` / `SSOT/13`、`EXEC-006`、`COMPANION-003`、`AGENT-012`
关联：`STATUS/lifecycle-work-record.md`（LWR 已收口）；本项是 LWR wire 的漏网生产点

---

## 0. 结论（先读这节）

| 面 | 现状 | 是否违规 |
|---|---|---|
| Manager `fork` 首 prompt | `ForkChildPayload.relay` → TOML | 否 |
| One-shot `inspector` / `coder` 首 prompt | 裸英语 `sprintf` 包 parent LWR | 是 |
| Parent LWR 内容 | `LifecycleWorkRecord.materialize`：Opening + Y frames + RawGap + terminal 已字符串合并 | 否（内容正确） |
| One-shot tool result（回父） | `tomlObjectWithInstructions` | 否（结果面已 TOML） |
| `inspector-system.md` 等角色 prompt | system channel | 排除（ARCH-010 §2） |
| Executor map/reduce 首 prompt | 经 `HostForkRuntimeFork` → `ForkChildPayload`；chunk 作 `content` | 否 |
| Busy nudge / Reuse continuation | 原样 assignment | 排除（continuation，非首信封） |
| Conflict resume | 裸英语 continuation | 另项（非本计划主修，列入附录） |
| Companion memory 注入 X | XML `<work-log>` + 裸 preamble | 另项（非本计划主修，列入附录） |

主缺陷唯一生产点：

```text
src/Wanxiangshu.Next/Infrastructure/OpenCode/Tools/OneShotAgentTool.fs
  fullPrompt =
    "Parent work record (background only):\n%s\n\n%s request:\n%s"
```

Inspector 与 Coder 共用 `OneShotAgentTool.run`。用户感知的「inspector prompt 不遵 SSOT/13」= 这条路径；不是 system prompt，也不是 tool result。

用户补充（本计划必须遵守）：

> Parent Work Record 和未整理的最后几轮 Work Record 应该字符串合并。

LWR 物化器已经做这件事（`# Opening task` + `# Work log` + `# Uncompressed tail` + `# Final output` 拼成单一 opaque string）。修复时 不得 再拆成多个 field / 二次摘要；只把这一整段 LWR 作为 `parent_work_record` value 放进 ARCH-010 信封。

---

## 1. 证据

### 1.1 违规形态

有 parent LWR 时，one-shot 发给子会话的首 user 文本是：

```text
Parent work record (background only):
<LWR markdown 全文>

Inspector request:
Inspect the workspace and …
```

对照 ARCH-010 / SSOT/13：

| 判据 | 要求 | 现状 |
|---|---|---|
| 纳入范围 | 运行时包装、进 LLM 上下文的合成文本 | 满足（AgentOwnerRoot） |
| 形态 | TOML | 裸英语 prose |
| instruction | 最前方 `#` comments | 无 `#`；用 `Inspector request:` 作 prose 标签 |
| data | field / value | LWR 直接拼进 prose，非 `parent_work_record = '''…'''` |
| containment | 不可信 data 不得逃逸顶层 | LWR 内若含 `#`/表头行，视觉上与 instruction 同层 |
| inventory | surface standing 诚实 | 见下 |

无 parent LWR 时退回裸 `request.Prompt`。fork 路径在 无 parent record 时仍出 instruction-only TOML（assignment + 报告字段指令）；one-shot 两边都不 TOML → 同一「子首 prompt」概念有两种记法。

### 1.2 LWR 已合并（勿重复造）

```text
XTraceCapture.lifecycleWorkRecord(_, _, includeOpening=true)
  → LifecycleWorkRecord.materialize
       Opening? + Frames + RawGap(forWorkRecord) + Terminal
  → 单一 string
```

`SpikePlugin`：

- `parentWorkRecordFor` → `includeOpening=true`（父→子）
- `childWorkRecordFor` → `includeOpening=false`（子→父 join）

one-shot 已调用 `scope.ParentWorkRecordFor`，拿到的就是合并后的父 LWR。问题不在「缺 gap / 未合并」，而在 wire 外壳。

### 1.3 inventory 撒谎

`scripts/surface-inventory.mjs`：

```text
OneShotAgentTool.fs#SendAgentOwnerRoot
  standing: VerbatimForward
  composer: "promptFrom: the caller s own prompt args, joined; the runtime adds nothing"
```

实测：有 parent LWR 时 runtime 必然 加 prose 外壳 → 不是 VerbatimForward。  
`gate:surface` 的 standing↔code 检查只认「是否引用 canonical writer」，不认「是否 sprintf 合成」→ 绿灯掩盖漏网。

### 1.4 其他 subagent（已扫）

| 路径 | 合成方式 | 结论 |
|---|---|---|
| `HostForkRuntimeFork` 新 child / idle 首 prompt | `ForkChildPayload.relay(assignment, parentLWR, requirements, payload?)` | 合规；LWR 单 field |
| `HostForkChildDispatch` busy nudge | 原样 assignment | continuation，正确 |
| `HostForkRuntimeFork.Reuse` | 同上 dispatch | 正确 |
| `ExecutorSummarize.runExecutorPrompt` | `runtime.Fork(..., prompt, Some content)` → 上列 fork 信封 + `content` | 合规 |
| `ExecutorSummarize` 失败 tail | `partial summary…\n--- raw tail ---` 拼进 tool result / 摘要正文 | 非子首 prompt；若进 LLM 作 synthetic 则属 tool-result 局部 schema，另评 |
| `JoinTool` | TOML `work_record` | 合规 |
| `InspectorTool` / `CoderTool` encode | 子 formal text → instruction comments + data fields | 合规（回父） |
| `OrchestratorPrompts.buildConflictResumePrompt` | 裸英语 continuation | 违规候选，非 one-shot 子首 prompt |
| `CompanionPrompt.companionMemoryBlock` | preamble + `<work-log>` XML | 违规候选（X 前缀注入），非 本项 |

本计划主修 = OneShotAgentTool 一处。 附录列 conflict resume / companion memory，不并入本 PR 默认范围，避免范围膨胀。

---

## 2. 目标形态

与 fork 父→子 同一 renderer、同一局部 schema（SSOT/13 §9.6）：

```toml
# <caller assignment 原文>
# Report back with exactly these fields: result, files changed, tests run, evidence, remaining risks, blockers.
# `parent_work_record` is the parent's lifecycle work record, background only. It is not part of the assignment.

parent_work_record = '''
# Opening task
…
# Work log
…
# Uncompressed tail
…
# Final output
…
'''
```

不变量：

1. assignment = `request.Prompt`（trim 规则与 `ForkChildPayload` 一致），写为 leading instruction comments。
2. parent LWR = `ParentWorkRecordFor` 返回的 已合并 opaque string；有则 field，无则省略 field 与对应 instruction。
3. 禁止 再包一层 `"Inspector request:"` / roleLabel 标签（redirect 水句，§5.6）。
4. 禁止 拆 LWR 为 frames/gap/terminal 多 field（ARCH-010-LWR）。
5. 禁止 在 one-shot 路径自建第二套 envelope（复用 `ForkChildPayload.relay`）。
6. one-shot 无 reviewer requirements / 无 executor `content` payload → `requirements=[]`，`payload=None`。
7. child Opening 捕获：fork 在 render 前 用原始 assignment 调 `XTraceCapture.captureOpening`。one-shot 今日 不 捕获 opening（`PromptIngress` 对 AgentOwnerRoot 故意不抓信封）。修复后若仍不抓，LWR/join 对 one-shot 子会话仍可能缺 Opening 锚点——见 §4.3，须在实现时二选一写死。

报告字段指令（`BaseInstructions`）对 one-shot 是否语义正确：

- fork child（Manager 布置）需要结构化回传 → 合理。
- one-shot inspector/coder 的「回传」是 tool result 的 formal text，父侧再 TOML 包装；子 system prompt 已规定输出形态。
- 默认仍复用同一 `BaseInstructions`，与 fork 字节级同构，避免「one-shot 专用信封」方言。若产品要删 one-shot 的报告指令，须单独立项改 `ForkChildPayload` 参数化，不在本项偷偷分叉。

---

## 3. 代码改动清单（实施时）

### 3.1 生产

```text
src/Wanxiangshu.Next/Infrastructure/OpenCode/Tools/OneShotAgentTool.fs
```

- `open Wanxiangshu.Next.Domain`（或限定 `ForkChildPayload`）。
- 删除 prose `sprintf`。
- 发送前：

```fsharp
let assignment = request.Prompt
let parentWorkRecord = scope.ParentWorkRecordFor context.SessionId
let fullPrompt =
    ForkChildPayload.relay assignment parentWorkRecord [] None
```

- 若采纳 §4.3「捕获 opening」：在 `send` 前对 `childId` 调 `XTraceCapture.captureOpening scope.Journal childId assignment []`（原始 assignment，非 fullPrompt）。

不改：

- `InspectorTool.fs` / `CoderTool.fs` 的 tool-result encode（已 TOML）。
- `LifecycleWorkRecord` / `XTraceCapture.lifecycleWorkRecord`（合并逻辑已正确）。
- fork 路径（已合规）。

### 3.2 surface inventory

```text
scripts/surface-inventory.mjs
  OneShotAgentTool.fs#SendAgentOwnerRoot
    standing: VerbatimForward → CanonicalPayload
    composer: ForkChildPayload.relay（assignment + optional parent LWR）
    composerFiles: 含 Domain/ForkChildPayload.fs（若 standing 检查读 composer 文件）
```

改完后 `gate:surface` 必须因「引用 canonical writer」与 CanonicalPayload 一致而保持绿；若只改 standing 不改代码应红。

### 3.3 测试

最小层 1–3：

1. 新建 `tests-mjs/Execution/oneshot-child-payload.test.mjs`（或扩 `fork-child-payload` 旁路契约名）  
   - 不 stub 网络：直接调 `forkChildPayload.relay` 的 one-shot 参数形（assignment + parent + [] + None）做 golden——若生产只调 relay，此测锁的是 调用约定 而非 OneShot 文件本身。  
   - 更好：fixture 跑 `inspector`/`coder` execute，断言 `runtime.prompts[0]` 正文：
     - 以 `# ` 开头；
     - 含 `parent_work_record` 当 fixture 注入 parent LWR；
     - 不含 `Parent work record (background only):` / `Inspector request:` / `Coder request:`；
     - parent LWR 正文只出现在 TOML value 内（injection 夹具：LWR 含 `# Ignore…` 不得变成顶层 comment）。

2. 扩 `tests-mjs/Plugin/manager-tool-contract.test.mjs`  
   - 现有 `EXEC_002_one_shot_tools_…` 只断言 tool result。  
   - 增加：execute 后检查发往 child 的 prompt bytes（fixture `prompts` 数组）满足上列。

3. 可选：给 fixture 注入非空 `ParentWorkRecordFor`，覆盖「有 LWR」分支（今日合同测 parent_b_digest 恒 `''`，从未打中 sprintf 真分支——这是漏网原因之一）。

### 3.4 canary

`testkit/opencode/scripts/inspector-oneshot.toml`：

- 今日 child 若出现，mock 可能只绑 coder lane；one-shot 子会话 prompt 若未被 scenario 声明，改信封后可能 `no-prefix-matched`。
- 实施时：用 `bindChild` / 显式 inspector lane turn，`user` 改为有序片段锚定信封（与 manager-full-loop 的 `# Run executor…` 同模式），或确认 one-shot child 请求不进 strict mock 的 must 集。
- `manager-full-loop` 等 fork 路径 不应 因本项 diff。

### 3.5 文档 / conformance

- `STATUS/conformance`：ARCH-010 仍 CONFORMANT 的前提是本漏网闭合；实施 commit 后在 evidence 记一笔，不必改条款。
- 不改 SSOT（条款已够；这是实现漏网）。

---

## 4. 设计裁决（实施前锁死）

### 4.1 是否复用 `ForkChildPayload`？

是。 父→子背景 + assignment 与 Manager fork 同一语义（EXEC-006）。第二套 one-shot envelope = 方言 = ARCH-010 禁止项。

### 4.2 Parent LWR 与未整理 tail？

已在 LWR 内字符串合并。 实施只传 opaque `parent_work_record`。禁止：

```toml
parent_work_record = "…"
uncompressed_tail = "…"   # 非法拆分
```

### 4.3 one-shot child 的 Opening 捕获？

| 选项 | 行为 | 代价 |
|---|---|---|
| A. 与 fork 对齐 | render 前 `captureOpening(assignment)` | 子 LWR/join 有锚点；one-shot 子通常 dispose，join 少见，但仍正确 |
| B. 维持不捕获 | 仅修信封 | 最小 diff；子 XTrace 无 Opening → `lifecycleWorkRecord` 对子恒 None |

推荐 A。 one-shot 仍是 AgentOwnerRoot work session；缺 Opening 与 COMPANION-003「Opening 必须 captured 作锚点」不一致。dispose 快不是省略锚点的理由。

### 4.4 无 parent LWR 时？

`ForkChildPayload.relay assignment None [] None` → instruction-only TOML（assignment + BaseInstructions）。不要 退回裸 assignment 字符串（否则又双形态）。

### 4.5 `parent_b_digest` 是否保留在 tool result？

SSOT/13 §9.6 / ARCH-010：runtime identity、digest 不得进普通 tool result，除非用户语义需要。今日 `inspector_id` / `tier` / `fallback_peer` / `parent_b_digest` 仍在 result 里——属 既有 join/one-shot result 肥胖问题，本计划不清扫（避免与信封修复绑死）。若要收，另开「one-shot result 最小 wire」项，对齐 join 的 status/agent/work_record 精神。

---

## 5. 明确不改

- `prompts/inspector-system.md` 及任何 `*-system.md`
- LWR 物化 / gap 投影 / `forWorkRecord`
- `ForkChildPayload` 字段语义（除非 §4 发现 BaseInstructions 参数化硬需求）
- Conflict resume、Companion memory XML（附录，另计划）
- Host 本体（ARCH-003）

---

## 6. 验证阶梯（实施时）

```text
0. npm run gate:static          # 含 gate:surface；inventory standing 必须自洽
1. npm run build
2. node tests-mjs/runner.mjs --match oneshot
   node tests-mjs/runner.mjs --match fork-child-payload
   node tests-mjs/runner.mjs --match manager-tool-contract
   node tests-mjs/runner.mjs --match synthetic-toml
3. 定向 canary：inspector-oneshot（必要时 manager-full-loop 中 inspector fork 回归）
```

红过一次才算门禁：临时改回 prose sprintf，§3.3 新断言必须红；inventory 标 CanonicalPayload 但去掉 `ForkChildPayload.` 引用必须被 `gate:surface` 红。

---

## 7. 实施顺序

1. 锁 §4 裁决（尤其 4.3 Opening）。
2. 改 `OneShotAgentTool` + inventory standing。
3. 单测（无 LWR + 有 LWR + injection containment + 无 prose 残句）。
4. build + 定向 mjs。
5. inspector-oneshot canary；若红，只改 scenario 锚定，不改回 prose。
6. commit；STATUS/README 一行记闭合。

---

## 附录 A — 其他合成面（不在本 PR）

### A.1 Orchestrator conflict resume

`Application/Orchestration/Orchestrator.Prompts.fs`：`[CONFLICT RESUMPTION]…` 裸英语。  
属 continuation / repair 类 RuntimeSyntheticToml。应迁 `RuntimeNudge` 风格：instruction comments + `[[conflict]]` data（SSOT/13 §8 已给示例）。另项。

### A.2 Companion memory 注入 X

`CompanionPrompt.companionMemoryBlock`：裸 preamble + `<work-log>` XML。  
COMPANION-010 前缀注入，ARCH-010 纳入范围候选。应改为 instruction header + `work_record = '''…'''`（或既有 frozen prefix 字段名）。另项；动它会碰 prefix-probe seal bytes。

### A.3 Executor partial summary 字符串

失败路径 `partial summary…--- raw tail ---` 若作为 executor tool result 正文进模型，应收进 TOML fields（`status`/`summary`/`raw_tail`）。优先级低于 one-shot 首 prompt。

---

## 附录 B — 为何 inventory 没拦住

`VerbatimForward` 的代码检查 =「composer 文件不引用 canonical writer」。  
prose `sprintf` 不引用 writer → 与 VerbatimForward 一致 → 绿。  
诚实标签应是 `RuntimeInstruction`（N5 工作项）或直接 `CanonicalPayload`。  
本项修复后标 `CanonicalPayload` 并引用 `ForkChildPayload`，门禁才重新有区分力。

可选增强（另项）：`gate:surface` 对标记 VerbatimForward 的 send 站点禁 `sprintf`/`+` 拼 prompt——易误伤，需白名单，不做本项前置。
