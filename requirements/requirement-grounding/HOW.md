# HOW — requirement-grounding 实现模型

本文件非 normative。以下是当前 production 结构与 executable proof 落点。

## 1. catalog + durable runtime

核心分成 catalog/scope/material 与 Host runtime；OpenCode hook 不解释 manifest：

```text
GroundingCatalog.discover(workspace) -> PackageDescriptor[]
GroundingCatalog.resolve(workspace, canonicalPath) -> PackageDescriptor[]
GroundingCatalog.materialize(workspace, package) -> GroundingSnapshot
RequirementGroundingRuntime.requestPaths(journal, workspace, session, paths)
  -> { NeedsGrounding; Requested; Packages }
```

`GroundingCatalog` 位于 `src/Wanxiangshu/Requirement/Grounding/Catalog.fs`。它发现 workspace-local
`requirements/*/WHAT.md`、解析范围并生成 snapshot；durable coverage 由 Host projection 决定。

## 2. APPLIES-TO parser

- 输入路径统一成 workspace-relative POSIX `/` 表示；workspace 外路径不参与。
- self coverage 在 resolver 第一条规则直接成立，先于 manifest，且不可被 `!` 取消。
- manifest 普通行 = include；`!pattern` = exclude；顺序覆盖前值。
- 只借 gitignore/wildmatch 的 pattern 语法，不借它“普通行=忽略、!=重新纳入”的负面语义。
- manifest 声明 `requirements/<self>/**` 视为配置错误，避免两套 self truth。

当前实现复用 `Repository/Programming/Js/GlobFs.fs` 的 `matchesPathPattern`；没有第二套 pattern 方言。

## 3. material set 与 digest

material set 顺序固定：

```text
README.md
WHY.md
WHAT.md
HOW.md
APPLIES-TO
tests/**/*.test.mjs  # lexical path order
```

只包含实际存在文件。内部 planner 对 canonical `(path, bytes)` 序列计算 digest。这样相同包名不同
workspace 不碰撞；内容不变时即使重复触碰多个文件也只自动读取一次。这个 material set/digest 只用于
规划与去重，不形成 provider-visible bundle。

## 4. provider observation path：一次读取，永久投影

OpenCode native `read`/`grep` 在 `tool.execute.after` 解析实际观察路径并提交 durable Request；mutation
在 `tool.execute.before` 解析目标路径并先提交 Request。下一次 provider transform 消费 Pending Request。

ordinary provider 使用 completed synthetic `read` Host tool part，`state.input.filePath` 与 `state.output`
分别承载 call provenance 与冻结 result bytes；Host 按其普通 tool projection 处理。grounding cause/digest
只存在于 durable fact，不进入 provider payload。

首次 Request 冻结 `GroundingSnapshot`；首次投影生成 `RequirementGroundingAnchored`，保存稳定 call id、
args、result bytes、Cursor result bytes、CallGap/ResultGap。后续只 replay occurrence，不重新读取文件。

placement 复用 HOST-013 的 append-only 原则：新 grounding 只能落在本轮追加区。如果当前 turn 同时创建
pair-programming 的 synthetic empty-name `skill`，同一 gap bucket 内 ordinal/order 明确为：

```text
existing real transcript
→ pair-programming pseudo skill
→ requirement read #1
→ requirement read #2
→ ...
```

不要把 grounding read 插在旧 user/tool history 中间。历史 anchor 缺失时也和 pair projection 一样不重定位；
事实保留，完整 transcript 恢复后再在原 anchor replay。

### Cursor 投影

Cursor 沿用 `PairProgrammingThoughtTransform.appendCursorSuffixes`，不构造 synthetic `read` call。
anchored occurrence 保存：

```text
CanonicalPath
ReadArgs
ResultBytes
CallId
CallGap
ResultGap
```

ordinary provider 用这些字段恢复正常 read call/result；Cursor 只消费 `CanonicalPath + ResultBytes + ResultGap`。suffix 顺序固定：

```text
terminal result bytes
NUL+BOM + pair-programming skill-content       # 若存在
NUL+BOM + cursor requirement-read envelope #1
NUL+BOM + cursor requirement-read envelope #2
...
```

Cursor requirement-read envelope 是 ordinary read result 的**最小来源补充**。因为没有 call half，必须把
workspace-relative path 作为稳定 source-path attribute 放在正文外层；正文 bytes 不改写。当前投影使用
单一、确定性、可转义的 XML-like 形状：

```text
<requirement_read path="requirements/finality/WHAT.md">\n
<ordinary read result bytes exactly as observed>\n
</requirement_read>
```

这里的 tag/attribute 是 Cursor projection HOW，可由 provider-projection 以后整体替换；永久语义只有三点：
result-only、path provenance 自足、正文 bytes 不被改写。path 必须 canonical + workspace-relative，并进行
确定性 attribute escaping。禁止塞 package/digest/grounding 标记；这些只存在于内部 durable fact。

若 ordinary `read` 将来增加 offset/limit 等参数，Cursor attribute 仍只负责恢复“哪个文件”这一缺失来源；
range/截断事实若已经体现在 ordinary result bytes 中就不得重复编码，若仅存在于 call-side metadata，则应由
provider-projection 定义同样最小的 result-local attributes。原则是不复制可从正文恢复的事实，也不让 Cursor
丢失 ordinary read 本来通过 call half 明确表达的 provenance。

Cursor 历史 suffix 与 ordinary pair 一样按 occurrence 原字节 replay。首次生成 envelope 后应冻结最终 Cursor
bytes，而不是每轮由 `CanonicalPath + ResultBytes` 重新 render，以避免 renderer 演进破坏 prefix cache。

纯 `glob`/目录 list 只返回路径名时不触发；grep 一旦返回源码行即触发其实际 match file。

## 5. mutation gate

native edit/write/move/remove 在 effect 前通常已经知道目标路径：before hook 直接 resolve。如果缺 grounding：

1. 不调用真实 mutation；
2. append durable `RequirementGroundingRequested`，冻结当前 package snapshot；
3. `tool.execute.before` 以预期、非 fatal 的 `REQUIREMENT_GROUNDING_REQUIRED` 拒绝当前调用；
4. 下一次 provider transform 把 pending snapshot 锚定成普通 read / Cursor result-only observations；
5. 只有 participant 看到这些 observations 后重新发出的 mutation 才进入普通执行。

不得把原 args 缓存后自动重放。

repository-programming 的 target set 可能由用户程序计算得到。`JsToolWorkflow.runWithMutationAdmission`
在 staging 与 `JsTransaction.preflight` 完成后、任何 prepare/write/commit 前把完整 mutation path set 交给
同一 grounding gate。若缺 grounding，返回 typed `REQUIREMENT_GROUNDING_REQUIRED`，stage 不 commit；
下一次是否再次运行程序由 participant 决定。

## 6. dedupe 与 semantic history

当前 durable vocabulary 是 `RequirementGroundingRequested` + `RequirementGroundingAnchored`。一个 anchored
occurrence 保存 `{ Workspace; Package; Digest; Ordinal; Reads; CallGap; ResultGap }`；每条 read 保存稳定
`CallId/Path/ArgsJson/ResultBytes/CursorResultBytes`。`CursorResultBytes` 在首次 occurrence 形成时已经完成
path attribute 包裹并冻结；projection 直接维护 `(Workspace,Package,Digest)` grounded set，不扫描 prompt 文本。

placement 与 Cursor suffix 直接复用 `PairProgrammingThoughtTransform.decideCurrentPlacement`、
`isCursorProvider`、`appendCursorSuffixes`；pair 与 grounding 的 durable payload 各自独立，位置算法不复制。

Fission sibling 是独立 provider context；没有看到这些 read observations 的 sibling 不因“逻辑上是同一个 participant”
而冒充已经看过。若未来把 grounding read transcript 作为 shared prefix 显式复制给 sibling，则复制行为必须同时产生
该 sibling context 的 coverage 事实。

## 7. OpenCode integration

当前生产路径：

```text
src/Wanxiangshu/Requirement/Grounding/
  Catalog.fs
  Model.fs
  Surface.fs

src/Wanxiangshu/OpenCode/Host/RequirementGrounding/
  Model.fs
  Projection.fs
  Runtime.fs
  Gate.fs
  Transform.fs

src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs
  native before/after gate

src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs
  pair 后的 permanent grounding projection

src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolWorkflow.fs
  transaction staged-effect admission
```

Host adapter 只负责把具体工具动作翻译成 `FileObservation` / `FileMutation`，不能按工具名各写一套
grounding state。

## 8. GAP

- GAP-018：CLOSED — workspace catalog / APPLIES resolver 已落地并由 001–004 active proof 覆盖。
- GAP-019：CLOSED — material/digest + durable Requested/Anchored projection 已落地并由 005/006/011/012 覆盖。
- GAP-020：CLOSED — native OpenCode read/mutation gate + pair/Cursor 投影已落地并由 007/008 覆盖。
- GAP-021：CLOSED — repository-programming staged effect admission 已落地并由 009/010 覆盖。

## DEPENDS ON

`requirement-system`, `host-boundary`, `participant-horizon`, `provider-projection`,
`interaction-authority`, `semantic-trace`, `prefix-stability`, `repository-programming`。

## 验证与测试落点

12 条 WHAT 均有 active executable proof；`requirement-trace --strict=requirement-grounding` 必须为 0 finding。

| 命题 | 当前测试落点 | 状态 / GAP | 目标 proof |
|---|---|---|---|
| REQUIREMENT-GROUNDING-001 | `tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-001] discovers requirement packages from the current workspace without a Wanxiangshu package list` | NEW / GAP-018 CLOSED | workspace discovery |
| REQUIREMENT-GROUNDING-002 | `tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-002] treats a package own requirements subtree as implicit coverage that APPLIES-TO cannot cancel` | NEW / GAP-018 CLOSED | implicit self coverage |
| REQUIREMENT-GROUNDING-003 | `tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-003] evaluates APPLIES-TO as ordered positive wildmatch includes with bang exclusions` | NEW / GAP-018 CLOSED | positive ordered matcher |
| REQUIREMENT-GROUNDING-004 | `tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-004] returns every overlapping package in deterministic package-name order` | NEW / GAP-018 CLOSED | overlap union |
| REQUIREMENT-GROUNDING-005 | `tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-005] plans one stable material set from canonical docs APPLIES-TO and package test sources without a provider-visible bundle` | NEW / GAP-019 CLOSED | material closure/order/digest |
| REQUIREMENT-GROUNDING-006 | `tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-006] deduplicates workspace package digest identity while allowing changed package content to ground again` | NEW / GAP-019 CLOSED | digest dedupe + invalidation |
| REQUIREMENT-GROUNDING-007 | `tests/opencode-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-007] ordinary providers replay anchored read call-result pairs while Cursor appends NUL-BOM result-only bytes after the pseudo-skill with stable source-path attributes` | NEW / GAP-020 CLOSED | ordinary/Cursor projection + production order |
| REQUIREMENT-GROUNDING-008 | `tests/opencode-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-008] defers the first ungrounded mutation with zero file effect and never auto-replays the old call` | NEW / GAP-020 CLOSED | zero-effect defer + expected nonfatal hook rejection |
| REQUIREMENT-GROUNDING-009 | `tests/repository-programming-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-009] grounds the union of a staged multi-file effect set before an all-or-nothing commit` | NEW / GAP-021 CLOSED | staged union before commit |
| REQUIREMENT-GROUNDING-010 | `tests/repository-programming-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-010] applies one grounding policy across OpenCode native and repository-programming file tools` | NEW / GAP-021 CLOSED | shared policy |
| REQUIREMENT-GROUNDING-011 | `tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-011] ordinary read observations add knowledge without creating authority or expanding capability` | NEW / GAP-019 CLOSED | authority negative boundary |
| REQUIREMENT-GROUNDING-012 | `tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-012] freezes ordinary read-pair bytes and Cursor path-attributed result bytes for restart replay while changed digests append without rewriting the provider prefix` | NEW / GAP-019 CLOSED | durable frozen replay |

### GAP 事实源

| GAP | 缺口 | 关闭条件 |
|---|---|---|
| GAP-018 | scope catalog / APPLIES matcher | CLOSED：production + active 001–004 proof；full verification 0 fail |
| GAP-019 | material/digest/durable anchored projection | CLOSED：production + active 005/006/011/012 proof；restart + prefix adjacency green |
| GAP-020 | native OpenCode read/mutation gate | CLOSED：production + active 007/008 proof；Host hook + pair projection adjacency green |
| GAP-021 | repository-programming staged effect admission | CLOSED：production + active 009/010 proof；repository-programming adjacency green |

### 运行

```text
node --test requirements/requirement-grounding/tests/*.test.mjs
node scripts/checks/requirement-trace.mjs --strict=requirement-grounding
```

两条命令均成功；full `scripts/check.mjs` / Fable build / authoritative verification 也必须保持 0 fail。
