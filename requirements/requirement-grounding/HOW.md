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
PROOF.md
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
ResultBytes; CallGap; ResultGap }`。coverage projection 只需由完整 occurrence 集 fold 出 `(Workspace,Package,Digest)`
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

