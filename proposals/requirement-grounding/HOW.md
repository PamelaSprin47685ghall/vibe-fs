# HOW — requirement-grounding 实现模型

本文件非 normative。当前 runtime 尚未落地；以下结构是实现 WHAT 的最短充分模型，并对应
PROOF/GAP 的施工边界。

## 1. 三段纯内核

推荐把核心压成三个纯模块，不让 OpenCode hook 自己解释 manifest：

```text
PackageCatalog.discover(workspace)
  -> PackageDescriptor[]

ScopeResolver.resolve(catalog, canonicalPath)
  -> PackageDescriptor[]

GroundingPlanner.plan(contextCoverage, packages, access)
  -> AlreadyGrounded
   | ReadWithObservation materials
   | BlockMutationAndRead materials
```

`PackageCatalog` 发现 workspace-local `requirements/*/WHAT.md`，读取 `APPLIES-TO`，但不读取
provider history。`ScopeResolver` 只做 canonical path + wildmatch 正向集合求值。`GroundingPlanner`
只比较 package content digest 与当前 context 已交付 identity，不碰文件系统 effect。

## 2. APPLIES-TO parser

- 输入路径统一成 workspace-relative POSIX `/` 表示；workspace 外路径不参与。
- self coverage 在 resolver 第一条规则直接成立，先于 manifest，且不可被 `!` 取消。
- manifest 普通行 = include；`!pattern` = exclude；顺序覆盖前值。
- 只借 gitignore/wildmatch 的 pattern 语法，不借它“普通行=忽略、!=重新纳入”的负面语义。
- manifest 声明 `requirements/<self>/**` 视为配置错误，避免两套 self truth。

建议复用 repository-programming 已有 wildmatch/glob 语义实现，而不是再写第二套 pattern 方言；
若其 API 不适合正向求值，抽出共享 pure matcher，不把 repository tool behavior 反向变成 owner。

## 3. material set 与 digest

material set 顺序固定：

```text
README.md
WHY.md
WHAT.md
HOW.md
HOW.md
APPLIES-TO
tests/**/*.test.mjs  # lexical path order
```

只包含实际存在文件。内部 planner 对 canonical `(path, bytes)` 序列计算 digest。这样相同包名不同
workspace 不碰撞；内容不变时即使重复触碰多个文件也只自动读取一次。这个 material set/digest 只用于
规划与去重，不形成 provider-visible bundle。

## 4. provider observation path：一次读取，永久投影

OpenCode native `read`/`grep` 的物理 tool result 在 provider 看见前有最后一个 Host 边界。适配层先从
tool args/result 得到真实 observed path 集合，resolve package union，再让 `GroundingPlanner` 生成
`ReadWithObservation`。

关键实现约束：**不要实现新的 grounding renderer。** 自动 material 必须复用模型主动 `read` 的同一条
read semantic surface / Host codec / provider projection。provider transcript 只能看到普通 read tool-call 与
普通 read tool-result；路径、range、truncation、error、source attribution 全部由现有 read 机制决定。
若一个文件需要多个 range 才能完整进入 horizon，就像模型自己继续 read 一样追加普通 read observations。
grounding cause 与 digest occurrence 只能留在 provider 不可见的内部事实里。

首次生成 read observation 后，不要在后续 transform 中重新读取文件。把每个已完成 read 转成与
`PairProgrammingGuidelineAnchored` 同类的 durable gap-anchored projection record：保存稳定 call id、read args、
exact result bytes、CallGap、ResultGap。projection 每轮只把这些历史 read pair 原位 replay。

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

Cursor 沿用 `PairProgrammingThoughtTransform` 已有 terminal-result suffix 路径，而不是构造 synthetic
`read` call。建议 anchored tool projection payload 同时保存：

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
workspace-relative path 作为稳定 source-path attribute 放在正文外层；正文 bytes 不改写。推荐使用单一、确定性、可转义的
XML-like 形状，例如：

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
2. 通过普通 read surface 读取缺失 material；
3. 当前 mutation 以普通“未执行/需重新发起”结果结束，不把 material 包进 mutation result；
4. 记录内部 GroundingDelivered occurrence；
5. 等 participant 在正常 read observations 之后自行决定是否重发 mutation。

不得把原 args 缓存后自动重放。

repository-programming 的 target set 可能由用户程序计算得到。它已有 transaction staging；应在 commit 前
暴露完整 staged effect set 给 grounding gate。若缺 grounding，正常丢弃 stage，产生普通 read observations；下一次
是否再次运行程序由 participant 决定。这样复用其 all-or-nothing 与 crash-no-auto-rerun 语义。

## 6. dedupe 与 semantic history

推荐新增 typed `RequirementGroundingReadAnchored` semantic occurrence，进入当前 participant trace。一个
package digest 可对应多条 read occurrence；每条保存 `{ Workspace; Package; Digest; Ordinal; CallId; Args;
CanonicalPath; ResultBytes; CursorResultBytes; CallGap; ResultGap }`。`CursorResultBytes` 是首次 occurrence
形成时已完成 path attribute 包裹的冻结字节；coverage projection 只需由完整 occurrence 集 fold 出 `(Workspace,Package,Digest)`
identity set；planner O(1)/有界查集合，不扫描 prompt 文本。

这里应优先**泛化现有 pair-programming gap projection**，而不是再造第二套 transcript 插入算法：pair guideline
与 requirement read 的 provider tool 名/args/result 不同，但“durable anchors + stable ordinal + byte-identical replay
+ append-only new placement”是同一个机制知识。可以抽成通用 anchored tool projection，再由
pair-programming 与 requirement-grounding 各自提供 payload。

Fission sibling 是独立 provider context；没有看到这些 read observations 的 sibling 不因“逻辑上是同一个 participant”
而冒充已经看过。若未来把 grounding read transcript 作为 shared prefix 显式复制给 sibling，则复制行为必须同时产生
该 sibling context 的 coverage 事实。

## 7. OpenCode integration

建议生产路径：

```text
src/Wanxiangshu/Requirement/Grounding/
  Catalog.fs
  Scope.fs
  Bundle.fs
  Planner.fs
  Projection.fs
  Surface.fs

src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs
  native tool before/after adapter

src/Wanxiangshu/Repository/Programming/
  transaction staged-effect adapter
```

Host adapter 只负责把具体工具动作翻译成 `FileObservation` / `FileMutation`，不能按工具名各写一套
grounding state。

## 8. 当前 GAP

- GAP-017：catalog + APPLIES-TO parser + scope resolver 尚无 production surface。
- GAP-018：material/digest + durable anchored read projection + trace-backed once-per-context delivery 尚无 production surface。
- GAP-019：native OpenCode observation/mutation gate 尚未接入。
- GAP-020：repository-programming staged effect set 与跨工具 no-bypass proof 尚未接入。

## DEPENDS ON

`requirement-system`, `host-boundary`, `participant-horizon`, `provider-projection`,
`interaction-authority`, `semantic-trace`, `prefix-stability`, `repository-programming`。

## 验证与测试落点

当前 package 已有正式合同与待实现契约测试，但 runtime 尚未落地。`test.todo` 是施工期 executable
spec，不计 active proof；因此 REQUIREMENT-SYSTEM-018 strict trace 应继续把本包报告为未证明，直到
对应 production semantic surface 落地并把 todo 转成 active test。

| 命题 | 当前测试落点 | 状态 / GAP | 目标 proof |
|---|---|---|---|
| REQUIREMENT-GROUNDING-001 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | 临时 workspace discovery，不 hard-code 万象术包集 |
| REQUIREMENT-GROUNDING-002 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | package self path 无 APPLIES-TO 仍命中；manifest 不能排除 self |
| REQUIREMENT-GROUNDING-003 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | 正向 include + `!` exclude + 顺序 + comments + absent manifest |
| REQUIREMENT-GROUNDING-004 | `tests/scope-resolution.test.mjs` | OPEN / GAP-017 | overlap 返回全 package set + stable order |
| REQUIREMENT-GROUNDING-005 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | material 文件闭包、稳定排序与 digest；无 provider-visible bundle |
| REQUIREMENT-GROUNDING-006 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | 同 digest 一次；内容变更可重新 grounding；workspace 隔离 |
| REQUIREMENT-GROUNDING-007 | `tests/opencode-gate.test.mjs` | OPEN / GAP-019 | ordinary 自动 grounding 与普通 read wire 完全一致；Cursor 只在 terminal result 的 NUL+BOM suffix 中先 skill 后 read-result，并以稳定 source-path attribute 补回缺失的 call-side provenance；历史 byte-identical replay |
| REQUIREMENT-GROUNDING-008 | `tests/opencode-gate.test.mjs` | OPEN / GAP-019 | 首次 ungrounded mutation 零 effect；grounding 后新调用才执行 |
| REQUIREMENT-GROUNDING-009 | `tests/repository-programming-gate.test.mjs` | OPEN / GAP-020 | multi-file staged effect union；缺 grounding 全丢弃、零 partial commit/auto-rerun |
| REQUIREMENT-GROUNDING-010 | `tests/repository-programming-gate.test.mjs` | OPEN / GAP-020 | native/custom 同一 policy；换工具不能绕过 |
| REQUIREMENT-GROUNDING-011 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | 普通 read observation 不造 HumanRoot、不改 role/capability |
| REQUIREMENT-GROUNDING-012 | `tests/grounding-delivery.test.mjs` | OPEN / GAP-018 | typed anchored-read occurrence 同时冻结 ordinary pair 与 Cursor path-attributed result suffix；retry/restart 原字节原位 replay；新 digest 只尾部追加；prefix law；internal loader 无递归 |

### GAP 事实源

| GAP | 缺口 | 关闭条件 |
|---|---|---|
| GAP-017 | scope catalog / APPLIES-TO matcher 尚不存在 production owner | `scope-resolution.test.mjs` 全部从 todo 转 active，命中正式 JS semantic surface，单跑绿 |
| GAP-018 | material/digest/durable anchored-read projection 与 authority-negative read projection 尚不存在 | `grounding-delivery.test.mjs` 全部 active；证明 ordinary/Cursor 两种冻结 bytes、restart 原位 replay、digest append-only、prefix law、dedupe/authority |
| GAP-019 | OpenCode native file observation/mutation 尚未经过 grounding gate，且尚无 ordinary-read 与 Cursor-result-only 双投影 oracle | `opencode-gate.test.mjs` active；真实 transform canary 证明 ordinary read wire 等价；Cursor NUL+BOM 顺序=skill→source-path-attributed read results、无 call half、正文原字节；mutation zero-effect defer |
| GAP-020 | repository-programming 动态 multi-file effect set 尚未接 grounding，跨工具 no-bypass 无 oracle | `repository-programming-gate.test.mjs` active；transaction staging + native/custom equivalence 绿 |

### 运行

```text
node --test requirements/requirement-grounding/tests/*.test.mjs
node scripts/checks/requirement-trace.mjs --strict=requirement-grounding
```

第一条当前应表现为 todo-only、进程成功；第二条当前应失败并列出未 active proof，这正是 GAP-017..020
尚未关闭的机器表现。
