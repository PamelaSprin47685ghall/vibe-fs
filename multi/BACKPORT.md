# BACKPORT.md — vibe-fs 本体缺陷登记

> 致 vibe-fs 维护者：本文档登记在 Squad PRD 设计核实期间发现的 vibe-fs 本体缺陷。所有论据带 `file:line` 出处，可逐条复核。Squad 与 vibe-fs 互不 import；Squad 自身已绕开这些坑，但 vibe-fs 本体仍中招，应 backport 修复。

缺陷索引：

| # | 标题 | 性质 | 严重度 |
|---|------|------|--------|
| 1 | review 重放读截断切片导致 With-Review 态静默丢失 | 现役正确性缺陷（门控被绕过） | 高（Mux 会话中途；OpenCode/OMP 重启后） |
| 2 | frontmatter 读写是手写土法 YAML，未用已声明的 `yaml` 依赖 | 质量缺陷（DRY 违规 + 死依赖 + 潜在陷阱） | 中（当前消费者仅喂标量，未现役丢数据） |

---

# 缺陷 1：review 重放读截断切片导致 With-Review 态静默丢失

## 0. 一句话定义

vibe-fs 的 review 重放（`runHostMessagesTransform` 的 `IfStoreEmpty` / `Always` 路径）从 `messages.transform` hook 拿到的是 opencode compaction 之后的截断切片，而非全量历史。当 `/loop` 激活消息（携 `task:` front-matter 锚点）落在 compaction 切点之前被截掉时，重放折叠 `inferReviewTaskFromTexts` 看不到锚点 → 返回 `None` → With-Review 态无法重建（OpenCode/OMP，重启后）或被主动清除（Mux，会话中途），submit_review 门控被静默绕过，用户无感知。

这直接违反 vibe-fs 自己写在 `LoopMessages.fs:53-57` 的设计承诺：「the history is the single source of truth and the store is merely a re-buildable projection of it」。重放本应从历史恢复态，却读了历史的截断视图。

---

## 1. 缺陷链条（四环，逐环带出处）

### 环一：opencode 只把 compaction 切片传给 transform hook（前提，已端到端核实）

`packages/opencode/src/session/prompt.ts:1145`：

```ts
let msgs = yield* MessageV2.filterCompactedEffect(sessionID).pipe(
  Effect.provideService(Database.Service, database),
)
```

`packages/opencode/src/session/prompt.ts:1325`：

```ts
yield* plugin.trigger("experimental.chat.messages.transform", {}, { messages: msgs })
```

传给 hook 的 `msgs` 是 `filterCompactedEffect` 的产物。`packages/opencode/src/session/message-v2.ts:532 filterCompacted` 从最近 compaction 切点起，只保留 `tail_start_id` 之后的消息，丢弃头部。故 transform hook 永远只见切片，看不到被压缩进 summary 的旧消息原文。

### 环二：compaction 不删存储，全量历史仍可拉取（修复可行性根基，已核实）

- `compaction.ts:244-248`：`split` 返回 `{ head, tail_start_id }`，只标切点，不删消息。
- `compaction.ts:289-294 prune`：只对 tool part 打 `part.state.time.compacted` 截断 tool output，从不动 user 文本。
- `compaction.ts:299+ processCompaction`：新建一条 summary assistant 消息 + 一个 compaction part 标记切点，旧 head 消息原样留在存储。
- `session.ts:857-880 messages()`：不传 `limit` 时分页循环到 `!page.more`，返回全部存储消息。

结论：被 compaction「压缩」的消息没有从存储消失，`client.session.messages({ sessionID })` 能拿到全量。问题纯粹出在 vibe-fs 重放读了 hook 切片而不去拉全量。

### 环三：vibe-fs 重放折叠的 `texts` 源自 hook 切片（病灶，已核实）

`src/Opencode/MessageTransform.fs:54-55`：

```fsharp
let replayTexts () =
    messagesList |> Messaging.flatten |> textsFromFlatParts
```

`messagesList = MessagingCodec.decodeMessages messagesArr`，而 `messagesArr` 来自 hook 的 `output`（即环一的切片）。`replayTexts` 喂给 `runHostMessagesTransform`：

`src/Shell/MessageTransformHostEntry.fs:20-39`：

```fsharp
let runHostMessagesTransform (reviewStore) (sessionID) (reviewReplayMode) (replayTexts) ... =
    promise {
        replayReviewForMode reviewReplayMode reviewStore sessionID (replayTexts ())
        ...
    }
```

重放的输入 `replayTexts ()` 就是切片文本，全量历史从未进入折叠。

### 环四：折叠看不到锚点 → 态丢失（后果，已核实）

`src/Kernel/LoopMessages.fs:64-73 inferReviewTaskFromTexts`：

```fsharp
let inferReviewTaskFromTexts (texts: string seq) : string option =
    texts
    |> Seq.fold (fun current text ->
        let fields = parseFrontMatterScalars text
        match Map.tryFind taskField fields with
        | Some task when task <> "" -> Some task          // 命中 task: 锚点 → 激活
        | _ ->
            match Map.tryFind verdictField fields with
            | Some verdict when isEndVerdict verdict -> None
            | _ -> current) None                          // 看不到锚点 → 保持 None
```

`/loop` 激活消息是 `buildLoopMessage`（`LoopMessages.fs:39-40`）注入的、携 `task:` front-matter 的最旧消息——最先落到 compaction 切点之前。一旦被截掉，折叠从 `None` 起、全程不见 `task:` → 返回 `None`。

`src/Shell/ReviewRuntime.fs:69-80 syncReviewProjection` 收到 `None` 时：

```fsharp
| None ->
    if store.getReviewState sessionID |> Option.isSome then
        store.deactivateReview sessionID
```

store 空（重启后）→ `getReviewState = None` → 什么都不做 → review 态保持 `Inactive`；store 有活跃态（Mux `Always` 会话中途）→ `deactivateReview` 主动清除。两路都丢 With-Review 态。

review store 是纯内存、无持久化（`ReviewRuntime.fs:28-29` `let mutable state = ...`），故重启后必空，重放是唯一恢复手段——而恢复手段读了截断历史。

---

## 2. 触发条件与复现

必要条件（同时满足）：

1. 一次 `/loop` With-Review 会话足够长，触发 opencode compaction。
2. compaction 切点越过了最初的 `/loop` 激活消息（`task:` 锚点被压进 summary 头部）。
3. 重放被触发：OpenCode/OMP 需重启 opencode（store 清空）；Mux 无需重启（`Always` 每次 transform 都重放）。

复现步骤（OpenCode）：

1. `/loop 实现 X`，进入 With-Review Mode（`reviewStore.activateReview` 直接置态）。
2. 让会话产生足量轮次（多轮 reject/revise 或长输出），触发 compaction，使激活消息落到切点前。
3. 重启 opencode。store 清空。
4. 继续对话触发 `messages.transform`。`IfStoreEmpty` 重放折叠切片 → 不见 `task:` → 态不恢复。
5. 此后 submit_review 门控失效：worker 表现为已退出 With-Review Mode，可不经 review 直接收尾。

Mux 复现更短：上面第 1-2 步后，无需重启，下一次 transform（`Always`）即把活跃 review 清成 `Inactive`。

---

## 3. 影响面（按宿主，区分已验证与待验证）

| 宿主 | 重放模式 | `replayTexts` 源 | 中招时机 | 我方核实程度 |
|------|----------|------------------|----------|--------------|
| OpenCode | `IfStoreEmpty`（`Opencode/MessageTransform.fs:71`）| `messagesList`= hook 切片（`:54-55`）| 重启后恢复路径 | 端到端确证（vibe-fs 重放 + opencode compaction/切片/全量语义全验） |
| OMP | `IfStoreEmpty`（`Omp/MessageTransform.fs:90`）| `decodeEntries entriesArr`= context event 切片（`:59`）| 重启后恢复路径 | vibe-fs 侧同构确证；OMP/Pi 宿主的 context event 是否截断历史**未亲验**，待确认 |
| Mux | `Always`（`Mux/MessageTransform.fs:73`）| `messagesArr`= hook 切片（`:61`）| 会话中途，无需重启 | vibe-fs 侧确证 `Always` 每次以切片覆盖 store；Mux 宿主截断语义**未亲验**，待确认 |

严重度排序：Mux > OpenCode ≈ OMP。

- Mux `Always` 模式结构性更危险：它每次 transform 都用切片折叠结果覆盖内存态，即使 `/loop` 命令已用 `activateReview` 正确置态，一旦锚点被压缩，下一次 transform 就主动 `deactivateReview`——这是在销毁本来正确的活跃态，且会话中途即发生。
- OpenCode/OMP 的 `IfStoreEmpty` 在会话中途安全（store 非空 → `replayReviewIfStoreEmpty` 走 `Some _ -> ()` 跳过），只有重启后 store 空 + 锚点已被压缩两条件同时成立才中招。

注：OMP（oh-my-pi/Pi）与 Mux 各是独立宿主，其 compaction/context 截断语义我未亲读源码。文档只断言「vibe-fs 重放读的是宿主提供的 hook 数组（切片）」这一已验证事实；「该数组是否为截断视图」对 OMP/Mux 需各自宿主侧确认。若宿主提供的就是全量，则该宿主不中招——但 `Always` 模式仍有「每次全量重折叠」的性能与覆盖语义问题，值得一并审视。

---

## 4. 根因

重放的契约是「从 SSOT(历史) 重建投影(store)」，但实现把「历史」错绑为 `messages.transform` hook 的入参。该入参是 opencode 为喂给模型而裁剪的 token 预算视图（compaction 切片），不是 SSOT 全量。投影重建读了被裁剪的源，于是投影必然缺失被裁剪掉的事实。

一句话：把「发给模型的上下文切片」误当成「事件历史全量」来折叠状态。两者在 compaction 触发前恰好相等，掩盖了缺陷；compaction 触发后分叉，缺陷暴露。

---

## 5. 修复方向

核心：重放折叠必须吃全量存储历史，不能吃 hook 切片。环二已证全量可得（`session.messages()` 返回全部存储消息，compaction 不删）。

### 方案 A（首选）：重放路径改拉全量历史

把各宿主 `replayTexts` 从「读 hook 切片」改为「异步拉全量历史再 flatten」。seam 在 `runHostMessagesTransform` 的 `replayTexts: unit -> string seq` 参数——它已是 thunk，只需换实现：

- OpenCode：`replayTexts` 内 `client.session.messages({ sessionID })`（`PluginInput.client` 已提供，返回全量），flatten 后取 texts。
- 因 `runHostMessagesTransform` 已在 `promise{}` 内，可把 `replayTexts` 签名升级为 `unit -> JS.Promise<string seq>`，或在调用点先 `let! fullTexts = ...` 再传 `(fun () -> fullTexts)`。后者改动更小，不动 Kernel 纯函数。
- `inferReviewTaskFromTexts`（Kernel 纯）无需改——喂全量即正确。
- OMP/Mux：需各自宿主的「全量历史」API。若宿主无等价全量接口，则该宿主退回方案 B 或宿主侧补接口。这点请 vibe-fs 维护者按宿主能力定夺，勿照抄 OpenCode 接线。

代价：重放路径多一次全量历史读（异步）。可只在真正需要重放时拉：

- `IfStoreEmpty`：仅 store 空时拉全量（重启后一次性成本，可接受）。把「store 空判定」前移到拉取之前，非空直接跳过，零额外成本。
- Mux `Always`：每次 transform 拉全量代价高。建议借此把 Mux 也改为 `IfStoreEmpty` 语义（见方案 C），否则需缓存全量 texts 或引入增量。

### 方案 B（宿主无全量 API 时的兜底）：激活态持久化锚点前移

若某宿主拿不到全量历史，可在 compaction 边界保护 `task:` 锚点不被截。vibe-fs 已有 `experimental.session.compacting` hook 接入点（`compaction.ts:353-357`，对应 vibe-fs 的 `compactingHandler`）。可在该 hook 把当前活跃 review 的 `task:` 锚点重新注入到 summary 之后的保留区，使其永远落在切点之后。

代价：依赖 compacting hook 时序正确、且每次 compaction 都补注入；比方案 A 脆弱，仅作兜底。

### 方案 C（兼治 Mux 的 `Always` 隐患）：统一为 `IfStoreEmpty` + 全量

Mux 用 `Always` 的初衷推测是「每轮纠正可能漂移的态」，但代价是用切片覆盖正确态。改为 `IfStoreEmpty` + 全量重放后：store 一旦由 `/loop` 命令正确置态，会话中途不再被切片折叠覆盖；只有 store 空（重启）才从全量历史重建。这同时消灭 Mux 的「会话中途静默清态」和「每次全量重折叠」两个问题。需确认 Mux 是否有依赖 `Always` 的其它语义，再决定。

### 明确不该做

不要引入 `.state.json` / NDJSON 之类的旁路持久化来「备份」review 态。那等于承认 SSOT 不可信、另起炉灶，违反 vibe-fs 自己的「历史即 SSOT、store 是可重建投影」原则（`LoopMessages.fs:53-57`）。全量历史本就在存储里且可拉取（环二），正解是「正确地读 SSOT」，不是「在 SSOT 外另存一份」。

---

## 6. 测试建议

- 纯内核：`inferReviewTaskFromTexts` 已可单测——补一例「输入缺 `task:` 锚点（模拟被截）→ 返回 `None`」，钉死折叠语义（这条本就正确，是上游喂错了数据）。
- 重放回归（关键）：构造一组消息，使 `task:` 激活消息位于 compaction 切点之前。
  - 喂「切片视图」（不含激活消息）给当前 `replayTexts` → 断言态丢失（红，复现缺陷）。
  - 喂「全量视图」（含激活消息）给修复后的 `replayTexts` → 断言态正确重建（绿）。
- Mux 专项：`Always` 模式下，store 已 `activateReview`，再喂缺锚点切片 → 当前实现断言被 `deactivateReview`（红）；修复后断言保持活跃（绿）。
- 端到端（可选）：opencode 重启 + 长 review + compaction 的集成场景，验证 submit_review 门控重启后仍生效。

---

## 7. 复核清单（维护者可逐条验证）

opencode 侧（前提）：

- [ ] `packages/opencode/src/session/prompt.ts:1145` — `filterCompactedEffect` 切片
- [ ] `packages/opencode/src/session/prompt.ts:1325` — 切片传入 `messages.transform`
- [ ] `packages/opencode/src/session/message-v2.ts:532` — `filterCompacted` 只留 `tail_start_id` 之后
- [ ] `packages/opencode/src/session/compaction.ts:244-248` / `289-294` / `299+` — 不删存储，只标切点 + 截 tool output
- [ ] `packages/opencode/src/session/session.ts:857-880` — `messages()` 无 limit 拉全量

vibe-fs 侧（病灶）：

- [ ] `src/Opencode/MessageTransform.fs:54-55`（`replayTexts` 取切片）, `:71`（`IfStoreEmpty`）
- [ ] `src/Omp/MessageTransform.fs:59`（取 entries 切片）, `:90`（`IfStoreEmpty`）
- [ ] `src/Mux/MessageTransform.fs:61`（取切片）, `:73`（`Always`）
- [ ] `src/Shell/MessageTransformHostEntry.fs:15-18,34` — `replayReviewForMode` / `runHostMessagesTransform`
- [ ] `src/Shell/ReviewReplaySync.fs:6-13` — `IfStoreEmpty` / `Always` 重放
- [ ] `src/Kernel/LoopMessages.fs:64-73` — 折叠；`:53-57` — 「历史即 SSOT」承诺
- [ ] `src/Shell/ReviewRuntime.fs:28-29`（无持久化）, `:69-80`（`None` → 清态/不动）

---

## 8. 与 Squad 的关系（背景，非修复必需）

Squad 在 PRD 核实 5.4 中独立踩到同一坑的长生命周期版本：其 DAG 态贯穿整个 session，compaction 跨越概率远高于单任务 review。Squad 的对策是 DAG 重放主动调 `client.session.messages()` 拉全量、并以 git refs 作合并事实的第二真理源，绕开了 transform 切片路径。该对策正是本文档方案 A 的同型解——可作 vibe-fs 修复的参照实现。Squad 与 vibe-fs 互不 import，此处仅为佐证「全量历史可得且足够」。

---

# 缺陷 2：frontmatter 读写是手写土法 YAML，未用已声明的 `yaml` 依赖

## 0. 一句话定义

vibe-fs 的 frontmatter 序列化（写）与解析（读）全是手写字符串拼接/扫描，复刻了一个不完整、不对称、且与读写两端各自漂移的 YAML 子集。项目 `package.json` 已声明 `"yaml": "^2.9.0"` 依赖，但该 npm 包从未在任何 F# 源码里被 import——纯属死依赖。手写读端的能力缺口（尤其读不回写端能产出的 YAML 序列）已逼出第二套独立手写解析器，DRY 被破坏。

性质澄清（与缺陷 1 不同）：当前 `parseFrontMatterScalars` 的现役消费者只喂标量字段，故「序列读不回」目前是潜在陷阱 + DRY 违规 + 健壮性缺口，**不是正在丢数据的现役 bug**。但土法 YAML 在面对外部书写、含注释/单引号/流式序列/特殊标量的 frontmatter 时会静默错读，且两套 parser 长期分叉是确定性的维护债。

---

## 1. 证据（土法实现 + 死依赖，逐条带出处）

### 死依赖：`yaml` 包声明了但从未 import

`package.json:33`：

```json
"dependencies": {
  "@fable-org/fable-library-js": "^2.1.1",
  "@opencode-ai/plugin": "^1.17.4",
  "yaml": "^2.9.0"
}
```

全 `src/` 搜 `import "yaml"` / `from "yaml"` / `require("yaml")` → 零命中。`src/` 内 57 处 `yaml`/`Yaml`/`YAML` 匹配全部指向手写 builder（`yamlField`/`yamlSeqField`/`yamlStringValue`/`yamlStringSeqField`/`yamlListItemField`）与手写 parser，无一处触达 npm 的 `yaml` 包。结论：依赖被声明、被 `npm install`、却从未被代码使用。

### 写端：全手写字符串拼接

`src/Kernel/PromptFrontMatter.fs:44-95` 手写完整 YAML scalar 编码决策树：

```fsharp
let private needsQuotes (value: string) : bool =          // :44 手写"何时需引号"启发式
    ...
    elif s.Length > 0 && startsWithYamlIndicator s.[0] then true   // :50 手写 YAML 指示符表
    elif s.Contains(": ") then true
    elif isYamlBoolWord s || isYamlNullWord s || isDocumentMarker s then true
    ...
let private quoteDouble (value: string) : string =        // :55 手写双引号转义
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
let yamlStringValue (value: string) : string =            // :65 手写 plain/quoted/block 三选一
    ...
let yamlSeqField (key: string) (items: string list) : string =     // :85 手写序列拼接
    if items.IsEmpty then key + ": []" else key + ":\n" + String.concat "\n" items
```

`startsWithYamlIndicator`（`:25-42`）手工列举 YAML 指示符字符；`isYamlBoolWord`/`isYamlNullWord`/`isDocumentMarker`（`:12-23`）手工复刻 YAML 标量歧义规则。这些正是 `yaml` 库 `stringify` 内建、且经过规范测试的部分。

### 读端：两套独立手写解析器（DRY 铁证）

解析器甲——`src/Kernel/PromptFrontMatter.fs:97-155`：

```fsharp
let parseYamlStringValue (raw: string) : string option =   // :97 只认双引号
    let t = raw.Trim()
    if t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\"") then
        Some(t.Substring(1, t.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\"))
    else None
let parseFrontMatterScalars (text: string) : Map<string, string> =   // :104 只读标量
    ...
    if line.Length = 0 || line.[0] = ' ' || line.[0] = '\t' then     // :133 跳过所有缩进行 → 序列读不回
        loop (i + 1) acc
    else
        let sep = line.IndexOf(": ")                                 // :136 硬性要求 ": " 分隔
        ...
```

解析器乙——`src/Kernel/ToolOutputInfo.fs:90-156 tryParseInfoList`，自带 `readBlockAt`（`:143`，复刻字面块读取）与 `parseScalarTail`（`:150`，复刻标量解析），专门读 `info:` 序列——正因为解析器甲读不回 `yamlSeqField` 产出的序列。两套 parser 并存即「共享那套不完整」的直接证据。

---

## 2. 具体往返缺口（读端 vs 写端不对称）

写端 `yamlSeqField`/`yamlStringSeqField` 能产出 YAML 序列（实际用于 `affected_files`、`caps`、`info`、`knowledge_graph` 等，见 `ReviewPrompts.fs:50/89`、`CapsFormat.fs:26`、`ToolOutputInfo.fs:84`、`KnowledgeGraph.fs:115`），但解析器甲 `parseFrontMatterScalars` 完全读不回：

| 输入 frontmatter | 标准 YAML 语义 | `parseFrontMatterScalars` 实际结果 | 缺口 |
|------------------|----------------|-------------------------------------|------|
| `k:\n  - a\n  - b` | `k = ["a","b"]` | `:133` 跳过缩进行 → `k` 缺失 | 序列读不回（逼出解析器乙） |
| `k: []` | `k = []`（空列表） | `:148-153` → `k = "[]"`（字面串） | 空序列误读为字符串 |
| `k: 'v'` | `k = "v"` | `:99` 只认双引号 → `k = "'v'"`（含引号） | 单引号不脱壳 |
| `k:`（空值） | `k = null` | `:137` `sep <= 0`（无 `": "`）→ 整行丢弃 | 空值/无空格分隔丢键 |
| `k: v # c` | `k = "v"`（注释剥离） | `k = "v # c"` | 不剥行内注释 |

现役安全性：上表多数缺口当前不爆，因为现役消费者（`KnowledgeGraph.fs:73` 读 `type`/`workspaceRoot`/`kind`/`date`；`LoopMessages.fs:67,79` 读 `task`/`verdict`/`double-check`）只喂「写端自己产出的双引号标量」，读写两端的土法子集恰好在标量上对齐。风险在于：① 任何未来字段升级为序列即静默失配（缺陷 1 的 `affected_files` 重放若改走标量 parser 就会踩中）；② 任何外部书写或经第三方工具改写的 frontmatter 不在土法子集内即错读；③ 两套 parser 长期分叉。

---

## 3. 根因

`src/Kernel/PromptFrontMatter.fs` 位于纯 Kernel 层（README 列为「跨宿主共享格式与协议」）。Kernel 架构纪律要求不碰 Node/宿主对象，作者遂回避「import 一个 npm 包」，改手写一个 YAML 子集塞进 Kernel。该子集：① 不完整（不支持序列读、单引号、空值、注释、流式）；② 不对称（写端能产序列，读端读不回）；③ 不收敛（缺口逼出 `ToolOutputInfo` 第二套 parser，两套独立演化）。

一句话：为守 Kernel 纯度而手搓 YAML，结果搓出一个读写不对称、且自我复制成两份的半成品，同时让已付费的 `yaml` 依赖闲置。

---

## 4. 修复方向

核心：读写共用同一个基于 `yaml` 包的 codec，消灭两套手写 parser 与手写 stringify。`yaml` 包是纯 JS（无 IO、无时钟、无网络、无宿主对象），YAML 序列化/解析是纯变换——按 README 判据「去掉 Node/宿主对象后仍成立就进 Kernel」，它本身具备进 Kernel 的纯度资格。是否允许「Kernel import 一个可移植 npm 库」是 vibe-fs 维护者的架构决策，下列两条按决策分叉，请勿照抄。

### 方案 A（首选若允许 Kernel 引纯库）：单一 `Kernel.Yaml` codec

- 新增 `Kernel.Yaml`，经 Fable `import` 绑定 `yaml` 包的 `parse`/`stringify`。
- `yamlField`/`yamlSeqField`/`yamlStringValue` 全部改为构造结构化值（map/list/scalar）后交 `stringify`；`parseFrontMatterScalars` 改为 `parse` 后取顶层 map，序列天然读回。
- `ToolOutputInfo` 的 `tryParseInfoList`/`readBlockAt`/`parseScalarTail` 整组删除，改用同一 codec 解析 `info:` 序列为结构化值再 match。两套 parser 收敛成一套。

### 方案 B（若 Kernel 须零外部 import）：`YamlCodec` 端口注入

- Kernel 定义 `YamlCodec` 接口（`stringify: YamlValue -> string`、`parse: string -> YamlValue`）+ 纯 `YamlValue` DU（Scalar/Seq/Map）。Kernel 内 builder/parser 只依赖该接口与 DU。
- Shell 新增实现，用 `yaml` 包填充接口（与现有 `ReviewStore`/effects 注入同型）。
- 宿主装配点注入实现。Kernel 保持零 Node import，纯度不破。

### 迁移纪律（两方案共用）

- frontmatter 既是消息历史里的结构锚点，也被测试精确匹配。`yaml` stringify 的字节级输出（引号风格、block vs flow、键序）可能与手写不同，会同时影响 ① 历史中旧锚点的再解析 ② 精确匹配测试。
- 安全顺序：**先升级读端**为 `yaml.parse`（标准 YAML 是土法子集的超集，旧格式照样读回），**再切写端**为 `yaml.stringify`。如此切换瞬间存量历史仍可重放。
- 锚点契约（`task`/`verdict`/`role`/`type` 等键名与取值）由 `LoopMessages.fs`/`ReviewPrompts.fs` 持有，属语义层，不随 codec 实现变化——迁移只换序列化机制，不动字段定义。

### 明确不该做

- 不要「读端换 `yaml`、写端继续手写」——那只是把不对称从读写之间挪到库与手写之间，DRY 仍破。必须读写同源。
- 不要保留 `ToolOutputInfo` 第二套 parser「以防万一」——它存在的唯一理由（共享 parser 读不回序列）在方案 A/B 下消失，留着就是新的漂移源。

---

## 5. 测试建议

- 往返性质测试（关键）：对一组覆盖「序列 / 空序列 / 单引号 / 双引号含转义 / 含 `#` / 含 `:` / 空值 / 多行块 / null/bool 歧义词」的值，断言 `parse(stringify(v)) = v`。当前手写实现会在序列/空序列/单引号/空值上红，修复后全绿。
- 锚点回归：`inferReviewTaskFromTexts`、`tryParseJobMarker`、`ToolOutputInfo.tryParse` 对现有真实 frontmatter 样本的解析结果，迁移前后逐字节比对（保证语义层不回归）。
- 跨端一致：同一结构化值经写端产出、再经读端解析，断言等价——钉死「读写同源」。
- 存量兼容：用旧手写格式（双引号标量）样本喂新读端，断言仍正确（验证"先升级读端"的超集假设）。

---

## 6. 复核清单

死依赖：

- [ ] `package.json:33` 声明 `yaml`；全 `src/` 无 `import "yaml"`

写端（手写）：

- [ ] `src/Kernel/PromptFrontMatter.fs:12-23`（手写 null/bool/marker 词表）, `:25-42`（手写指示符表）, `:44-53`（手写 needsQuotes）, `:55-56`（手写转义）, `:65-89`（手写 scalar/field/seq builder）

读端（两套手写）：

- [ ] `src/Kernel/PromptFrontMatter.fs:97-102`（仅双引号）, `:104-155`（`:133` 跳缩进行读不回序列、`:136` 硬性 `": "`、`:148-153` 空序列误读）
- [ ] `src/Kernel/ToolOutputInfo.fs:90-156`（第二套：`tryParseInfoList`/`readBlockAt`/`parseScalarTail`）

消费者（现役只喂标量，故潜在非现役）：

- [ ] `src/Kernel/KnowledgeGraph.fs:73`（`type`/`workspaceRoot`/`kind`/`date`）
- [ ] `src/Kernel/LoopMessages.fs:67,79`（`task`/`verdict`/`double-check`）

架构：

- [ ] `PromptFrontMatter` 属 Kernel「共享格式协议」层 → 方案 A/B 的分叉点（是否允许 Kernel 引纯 npm 库）

---

## 7. 与 Squad 的关系（背景，非修复必需）

Squad PRD 核实 5.5 已记录此事实：vibe-fs 的标量解析器不认序列，故 Squad 决定自带事件 codec、在 Shell 层用 `yaml` 包全量解析 `depends_on` 序列，Kernel 保持纯——即本文档方案 B 的同型解。Squad 与 vibe-fs 互不 import，此处仅佐证「`yaml` 包可用且足以覆盖序列」。
