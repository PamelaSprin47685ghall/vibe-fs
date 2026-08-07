# Inspector Casebook — 持久复用、增量刷新与 Git Raw 同步

## 摘要

Inspector 的一次调用天然形成一个有价值的知识单元：

```text
Question
→ Inspector 调查 repository
→ Answer
```

当前该知识在调用结束后主要只存在于 Session/transcript 中。后续 Inspector 即使面对相同或高度相关的问题，也必须重新调查；已经消耗过的 read/glob/grep 等证据无法被可靠复用。

本 Change 引入一个**可选的 Inspector Casebook**：

1. 每个 Inspector Session 保存一个当前 Q&A（capture 全程 best-effort）；
2. 同时保存该答案所依据的、可重放的 repository observations（能捕获多少捕获多少，缺失允许）；
3. 后续 Inspector 可以通过 `fetch(session_id)` 取得旧答案；
4. `fetch` 不直接信任旧答案，而是先针对**当前 worktree**重放 observations；
5. 未检测到 observation 变化时直接复用旧 A；no-delta 只是 freshness hint，**不构成正确性证明**；
6. 检测到 observation 变化时，启动一个私有 Bookkeeper Agent，根据 Q/A 与 evidence diff 修订 Q/A；
7. Bookkeeper 成功且 evidence 再次稳定 → 更新 snapshot 并返回新 A；失败 → 保留旧 Case，返回旧 A（允许过时——这是预期产品语义）；
8. Casebook 不进入任何 worktree，不制造 branch commit，不产生产品意义上的版本历史；
9. Casebook 使用 Git object database + 一个 custom tree ref 作为 repository-global 当前状态；
10. linked worktree 共享同一个 Casebook；
11. 与普通 Git remote 的同步统一采用 dumb-remote 模式：
    `fetch → merge → CAS-push`；
12. replica merge 只使用简单的 `revision + wall_clock` LWW，同值任取；
13. timestamp/revision 只解决 replica 收敛；observation replay 只是 freshness hint——任何机制都**不证明答案正确**；
14. Casebook 使用有限 LRU，长期无人使用的条目自动淘汰；
15. 本功能完全 opt-in：只有 repository 中存在指定 marker directory 时才启用；不存在时工具、Prompt 注入、Bookkeeper 和同步行为全部静默消失。

Compatibility：**Compatible**。未启用 Casebook 的 repository 行为必须保持不变。

---

# 0. 设计姿态

Inspector Casebook 明确选择 **availability and reuse over freshness guarantees**。

Casebook 是 hopefully useful 的 best-effort semantic cache，不是证明系统。旧答案可能因为 observation capture 不完整、shell 阅读未识别、未观察到的新文件、repository 并发变化、Bookkeeper 失败或其它信息缺口而过时或不准确；这些情况属于允许的产品行为。

## 0.1 必须严格保证

以下性质是机械安全边界，必须 fail closed：

```text
- Inspector 不修改 subject worktree；
- Casebook 动态数据不污染 git status / Orchestrator Clean Gate；
- Case publication 原子，不出现 Q/A/snapshot 撕裂状态；
- Git canonical ref 更新使用 CAS；
- remote push 使用 lease，不 blind force；
- Bookkeeper 只能通过 edit-qa 修改 staged Q/A；
- Casebook 内容不逃逸 Synthetic TOML data containment；
- 同一 Inspector PrefixEpoch 的 Casebook index 字节稳定；
- Casebook mutation 不主动制造 PrefixEpoch；
- ToolResultBound 始终生效；
- 路径不得逃逸 repository / .git 安全边界；
- 有界 retry，不建立第二运行时或无限同步循环。
```

## 0.2 明确不保证

以下性质只 best effort：

```text
- Inspector 是否捕获了全部 answer-relevant evidence；
- cat / sed / head / tail 或其它 executor 阅读是否被完整识别；
- glob / grep observation 是否代表整个逻辑搜索空间；
- snapshot 是否足以发现所有会影响旧答案的 repository 变化；
- 没检测到 observation delta 是否意味着旧答案仍然正确；
- Bookkeeper 是否成功把旧答案刷新到当前 repository 的最佳答案；
- 两个并发 replica 中 LWW winner 是否语义上“更正确”。
```

因此：

```text
observation replay = freshness hint
revision / wall_clock = replica ordering
Bookkeeper = opportunistic maintenance
A.md = reusable best-effort knowledge
```

任何上述机制都不得被提升为 correctness proof。

---

# 1. 目标

## 1.1 核心目标

把 Inspector 已完成的调查从“一次性模型消费”变成：

```text
repository-scoped
+ evidence-backed
+ freshness-hinted
+ self-maintaining
+ remotely synchronizable
```

的知识缓存。

它不是普通字符串 cache。

Casebook 中真正可复用的对象是：

```text
Question
Answer
Evidence snapshot
Replayable observations
```

Answer 是否直接复用，由 observations 在当前 worktree 上的 best-effort 重放结果给出提示；未检测到变化即视为 cache hit，允许直接复用。

---

## 1.2 定位：best-effort semantic cache

本 Change 不声称“正确性保证”。

框架保证的只是机制方向，不是答案质量：

```text
revision / wall_clock
    = replica conflict resolution

observation replay
    = freshness hint
```

```text
no detected change
≠ proved unchanged world

no detected change
= good enough cache hit
```

旧 A 可以因为 capture 缺口、未识别命令、未观察区域、并发变化、Bookkeeper 失败等原因过时——这是允许的产品行为。

---

## 1.3 fetch 对 Inspector 免费

`fetch` 在模型决策语义上必须被描述为**零成本**。

Inspector system 必须包含以下指令（或语义等价的表述）：

```text
Inspector Casebook entries are available through fetch(session_id).

Treat fetch as free. Do not conserve, ration, or avoid fetch because of
cost, latency, token usage, or implementation concerns.

When an existing question appears even plausibly relevant, prefer fetching
it before repeating repository investigation.

Relevance-not cost-is the reason to decide whether to fetch.

Fetched answers are best-effort cached knowledge and may be stale or
imperfect. Use them freely, then continue investigating whenever useful.
```

后台真实成本（Git read、observation replay、Bookkeeper provider call、CAS、remote sync）由 runtime 承担，Inspector 不可见，也不得据其优化调用。

`fetch` 仍是阻塞工具，但“阻塞”是执行语义，不是决策成本信号。

---

# 2. 非目标

本 Change 明确不做以下事情。

## 2.1 不建立知识数据库

不增加：

```text
embedding index
vector database
semantic search service
knowledge graph
coverage graph
自动主题分类
问题聚类
answer confidence score
```

Inspector 只看到一个简单的：

```text
session_id -- full question
```

列表，并自行判断是否值得 `fetch`。

---

## 2.2 不引入 commit history

Casebook 不创建：

```text
Casebook commit
parent commit
Casebook branch
tag
merge commit
历史版本链
```

Git 只被用作：

```text
content-addressed blob/tree store
+ atomic ref CAS
+ remote object transport
```

---

## 2.3 不保证历史 Q/A 可追溯

`Q.md` 和 `A.md` 是**当前 canonical 文档**。

旧 Git objects 在 ref 更新后可以变成 unreachable，并最终被 Git GC。

产品层不存在：

```text
previous revision
history()
rollback()
show old answer
```

`revision` 只是 merge scalar，不是用户可查询的版本历史。

---

## 2.4 不用 timestamp 判断 freshness

即使某个 remote case：

```text
revision = 999999
wall_clock = tomorrow
```

也不得因此跳过 evidence replay。

---

## 2.5 不改变 subject worktree

Inspector、Bookkeeper、Casebook synchronization 都不得：

```text
write subject source files
stage files
commit files
stash
checkout
rebase
改变 branch
```

Casebook 的动态内容不进入 worktree，因此不得使 Clean Gate 变 dirty。

---

# 3. Opt-in

## 3.1 Marker

功能启用 marker：

```text
.wanxiang/casebook/
```

Git 不保存空目录，因此 repository 可以提交：

```text
.wanxiang/casebook/.keep
```

但运行时只判断：

```text
directory exists?
```

不解释 `.keep` 内容，也不要求 README、manifest、配置文件或 schema 文件。

---

## 3.2 Feature disabled

若：

```text
.wanxiang/casebook/
```

不存在，则 Casebook 功能必须整体静默消失。

具体包括：

```text
Inspector provider schema 中没有 fetch
Inspector system prompt 中没有 Casebook index
不创建 Bookkeeper
不要求 Bookkeeper Agent 配置存在
不采集 Casebook observations
不创建/更新 Casebook canonical ref
不主动 fetch/push Casebook ref
Git hook 即使残留也必须立即 no-op
```

已有：

```text
refs/wanxiang/inspector-casebook
```

可以保留，不要求删除。

重新创建 marker 后可以重新使用。

---

# 4. Git 物理模型

## 4.1 Canonical local ref

每个 Git repository 只有一份 canonical Casebook：

```text
refs/wanxiang/inspector-casebook
```

该 ref：

```text
直接指向 tree object
```

而不是 commit。

因此：

```text
refs/wanxiang/inspector-casebook
        │
        ▼
     root tree
```

---

## 4.2 linked worktree 共享

Casebook 的 repository identity 取 Git common directory。

不同 linked worktree：

```text
repo/
repo-worktree-a/
repo-worktree-b/
```

必须解析到同一个：

```text
Git common object database
refs/wanxiang/inspector-casebook
```

不得为每个 worktree 创建不同 Casebook copy。

因此：

```text
worktree A ─┐
worktree B ─┼──► one canonical Casebook ref
worktree C ─┘
```

但 `fetch(session_id)` 的 freshness replay 永远针对**调用 Inspector 所在的当前 worktree**。

---

## 4.3 没有 canonical ref

marker 存在但 canonical ref 不存在表示：

```text
Casebook enabled
Casebook currently empty
```

不是错误。

第一条可永久化 Case 创建时 CAS-create ref。

---

# 5. Root tree 格式

推荐逻辑布局：

```text
<root-tree>/
  cases/
    <encoded-session-id>/
      Q.md
      A.md
      meta.toml
      snapshot/
        observations.toml
        files/
          <repository-relative-path...>
```

不建立 root manifest。

---

## 5.1 Session path

模型不得提供 Case 路径。

路径只能从真实 Inspector SessionId 确定性编码：

```text
encode(SessionId)
→ one safe Git tree path segment
```

必须拒绝：

```text
/
..
NUL
path traversal
平台相关歧义
```

`Q.md` / `A.md` / `meta.toml` / `snapshot` 均为框架固定名字。

---

# 6. Case 内容

## 6.1 Q.md

新 Inspector Case 创建时：

```text
Q.md = Inspector invocation 的完整 initial prompt
```

不摘要。

不做 ToolResultBound truncation。

Bookkeeper 后续**允许修改 Q.md**。

因此长期语义是：

```text
Q.md
= current canonical question

initially initialized from original Inspector prompt
```

不是 immutable forensic record。

Casebook 不保留一个额外：

```text
OriginalQ.md
```

也不保留旧 Q history。

---

## 6.2 A.md

新 Case 创建时：

```text
A.md
= Inspector tool 实际返回给 caller 的 ToolResult body
```

必须先经过现有 ToolResultBound。

因此：

```text
内部 Inspector Session 可能产生更长文本

但

A.md == caller 真正拿到的 bounded answer
```

不存在：

```text
A.full.md
A.raw.md
hidden untruncated answer
```

---

## 6.3 Bookkeeper 更新后的 A.md

Bookkeeper 修改 A 后，最终 candidate A 仍必须满足同一个 ToolResultBound。

Casebook 中保存的 A：

```text
就是 fetch 最终能够返回的 bounded bytes
```

因此以后：

```text
fetch(id)
→ stored A.md
```

不需要另一套 Casebook 专属截断规则。

普通 tool boundary 再应用一次相同 bound 必须是幂等的。

---

# 7. meta.toml

每个 Case：

```toml
revision = 42
wall_clock = "2026-08-08T03:58:12.123Z"
last_access = "2026-08-08T04:00:01.001Z"
```

只有这三个 merge/cache 字段。

不得增加：

```text
confidence
status
fresh
stale
validated
phase
owner
generation
semantic score
```

---

## 7.1 revision

新 Case：

```text
revision = 1
```

每当 Q/A/snapshot/observations 这个**逻辑 Case 内容整体**发生一次 refresh publication：

```text
revision =
    max(all observed competing revisions) + 1
```

仅 LRU touch：

```text
revision 不变
```

Replica merge：

```text
revision 不变
```

revision 不得因：

```text
fetch remote
CAS retry
push retry
LRU merge
```

虚增。

revision 达到整数表示上界时不得 wrap；该 Case 直接淘汰（evict），具体实现不得回绕为 0。

---

## 7.2 wall_clock

`wall_clock` 是产生该 content revision 时的 wall-clock timestamp。

只在 revision 内容更新时改变。

比较只作为 revision 相同情况下的 tie-breaker。

系统时钟漂移不会影响 correctness。

---

## 7.3 last_access

以下成功事件更新：

```text
新 Case publication
成功 fetch(session_id)
```

不要求更新时间单调可信。

它只服务 LRU。

时钟漂移最多影响 cache retention，不影响 answer validity。

---

# 8. Replica merge

设本地和 remote 都存在同一个 `session_id`。

内容 winner 使用：

```text
(revision, wall_clock)
```

lexicographic max。

规则：

```text
revision 大 → 赢

revision 相同
wall_clock 大 → 赢

revision 和 wall_clock 都相同
→ 任取一个
```

为了让同一输入的纯 merge 实现稳定，最后一种“任取”建议机械地使用：

```text
canonical content-tree OID lexicographic max
```

这不是第三个业务 timestamp，也不进入 `meta.toml`。

只是 deterministic arbitrary choice。

---

## 8.1 原子 Case winner

winner 必须整体选择：

```text
Q.md
A.md
snapshot/
revision
wall_clock
```

禁止：

```text
local Q
+ remote A
+ local snapshot
```

或任意字段级 semantic merge。

---

## 8.2 last_access 独立 merge

无论 content winner 来自哪边：

```text
merged.last_access =
    max(local.last_access, remote.last_access)
```

因此另一 replica 更新 A 不会把本地刚访问过的热门 Case 变成冷数据。

---

## 8.3 不使用 tombstone

Case missing 不表示删除事实。

```text
missing != tombstone
```

merge 先 union 所有合法 Case，再执行同一个 deterministic LRU prune。

LRU 淘汰不创建 deletion record。

---

# 9. LRU 与有界性

Casebook 必须有界。

不得依赖：

```text
provider token window
模型上下文表
动态 token estimation
```

只能依赖固定的：

```text
case count
UTF-8 bytes
Git object payload bytes
rendered index bytes
```

实施时必须在正式 what/how 中公布有限正数常量，至少覆盖：

```text
CasebookMaxCases
CasebookMaxStoredBytes
CasebookIndexMaxUtf8Bytes
```

这些是 cache contract，不是模型容量预测。

---

## 9.1 Prune key

淘汰顺序：

```text
last_access ascending
then session_id lexical
```

即最久未访问优先删除。

---

## 9.2 Prune timing

以下操作之后统一运行同一个纯 prune：

```text
local Case publication
remote/local merge
LRU touch merge
```

相同 Case 集合 + 相同 metadata 必须生成相同 retained Case 集。

---

## 9.3 单 Case 超界

如果新 Case 自身就无法满足：

```text
stored-byte bound
或
完整 Q index entry bound
```

则 Inspector 原调用正常返回，但该 Session **不进入 Casebook**。

不得为了缓存而截断：

```text
Q
snapshot
observation evidence
```

因为这会破坏 Q/A/evidence 的一致性与 replay 语义。

---

# 10. Inspector Index

## 10.1 内容

Inspector system 中注入当前 retained Case 的：

```text
session_id -- full Q.md
```

Q 不摘要、不 embeddings、不关键词化。

建议按：

```text
last_access descending
then session_id lexical
```

展示，方便最近使用内容靠前。

---

## 10.2 低信任数据

旧 Q 可能包含：

```text
指令
prompt injection
代码
错误文本
恶意 Markdown
```

因此完整 Q 必须作为明确标记的低信任 data 注入。

不得：

```text
裸拼成 system instructions
把 Q 中 # comment 提升为 trusted instruction
从 Q 反推 Authority
```

Renderer 必须使用统一字符串 codec / containment 规则。

---

# 11. PrefixEpoch 稳定

Casebook 变化不得主动制造 PrefixEpoch。

禁止：

```text
新增 Case
→ switch PrefixEpoch

LRU touch
→ switch PrefixEpoch

remote sync
→ switch PrefixEpoch

Bookkeeper refresh
→ switch PrefixEpoch
```

---

## 11.1 CasebookIndexSnapshot

每个 Inspector PrefixEpoch 持有一个冻结的：

```text
CasebookIndexSnapshot
```

逻辑内容至少包括：

```text
Casebook root OID
ordered session_id
Q bytes
case subtree OID
rendered index bytes
```

同一 PrefixEpoch 内永久不变。

---

## 11.2 选择时机

新的 Inspector epoch 的 index 必须在：

```text
该 epoch 第一份 provider-visible bytes 被 seal 之前
```

选择完成。

不得在 seal 后重新读取 Casebook 并替换 Q list。

对于 prefix probe/promote：

```text
probe candidate materialize
→ 同时冻结 candidate CasebookIndexSnapshot
→ provider request
→ probe 成功
→ promoted epoch 继承同一个 snapshot
```

禁止 probe 成功后再重新 sample Casebook。

对于 compaction reanchor：

```text
reanchor
→ 构造下一 epoch 第一请求前
→ 选择新 CasebookIndexSnapshot
```

---

# 12. Epoch pin

LRU 或其它 worktree 可能在当前 Inspector epoch 活跃期间把某个已展示 Case 从 canonical root 淘汰。

因此 index 不能只保存 session id。

当前 epoch 必须让其 index root 在物理上保持可读。

推荐使用 local-only pin：

```text
refs/wanxiang/pins/<host-session-id>/<epoch-id>
```

指向该 epoch 建 index 时的 root tree。

该 ref：

```text
永不 push
永不 fetch
不进入 Casebook merge
不进入 Inspector index
```

Epoch retire / Session dispose 后删除。

这样 Git GC 不会在活跃 epoch 中清除已展示 Case 的对象。

---

# 13. fetch(session_id)

## 13.1 工具可见性

只有：

```text
Casebook enabled
且
Role = Inspector
```

时 provider schema 和 execution registry 同时暴露：

```text
fetch(session_id)
```

其它 Agent 均没有该工具。

feature disabled 时不存在空壳 tool。

---

## 13.2 Lookup

调用时：

1. 首选 canonical current root 中同 `session_id` Case；
2. 若 canonical 已因 LRU 不存在，但当前 Inspector epoch 的 frozen index 中存在该 ID，则可从 epoch pin 的 Case subtree 恢复；
3. 两者都没有则返回 typed tool failure：
   `CASE_NOT_FOUND`。

不得从 Session transcript 猜旧答案。

---

## 13.3 失败面

`fetch` 只在无法提供答案时失败：

```text
session_id 不存在
Case 损坏到无法读取
A.md 缺失
```

只要存在可读取的 A：

```text
prefer answer over failure
```

refresh 失败、remote 失败、publication 失败都不构成 fetch 失败（§22.3、§29.1、§36）。

---

# 14. Observation capture

Casebook 的 freshness 依赖 observation capture。

Inspector 运行期间必须从真实 tool execution 记录 observation，而不是 Session 结束后从自然语言反推“它看过什么”。

---

## 14.1 原则

```text
capture = best effort，无资格门槛
```

含义：

```text
能捕获多少 observation 就捕获多少
不能可靠解释的 observation 就跳过
不因此阻止 Case creation
```

不设置 `capture_complete` 之类的元数据字段——一旦引入，实现者容易据此重新长出 correctness protocol。

漏捕获的后果只是未来少一次变化检测机会，不是归档失败。

---

# 15. read observation

成功的 `read` 至少记录：

```text
repository-relative path
read arguments / range
tool-visible semantic result digest
snapshot full-file bytes
```

`snapshot/files/<path>` 保存该 repository 文件在 observation capture point 的完整快照。

---

## 15.1 Capture race

如果 Host read 与 after-hook 之间文件发生变化，导致无法证明 snapshot 能产生 Inspector 实际看到的 read result：

```text
跳过该文件的快照
Case 照常归档
```

该文件因此失去未来 freshness 检测机会——可接受的 evidence loss。

绝不允许把：

```text
Inspector 看见旧内容
snapshot 却保存新内容
```

写成“相容证据”；快照要么相容，要么不存在。

---

# 16. glob observation

记录完整 canonical：

```text
pattern
root/path
options
complete result
```

Replay 使用同一 semantic capability。

比较的是 canonical result，不比较 stdout 格式噪声。

Host glob 无法证明 result 完整时（如无法识别的 truncation），如实记录已有结果并照常重放比较；完整性存疑只是降低检测能力，不阻止归档。

---

# 17. grep observation

记录：

```text
pattern
scope/path
options
complete semantic result
```

必须能够区分：

```text
zero matches
```

和：

```text
result 被截断后刚好只显示 zero/部分 matches
```

无法区分时按已记录结果重放比较（best-effort）；不阻止归档。

---

# 18. executor 阅读容错

Inspector 仍可使用现有 executor。

Casebook 积极识别明确的纯读取命令，识别成功则编译为等价 replayable observation 并记录底层文件 snapshot。正例（不限于）：

```text
cat file
cat -n file
cat file1 file2
head file
head -n N file
head -N file
tail -f file
tail -n N file
tail -N file
sed -n 'A,Bp' file
sed -n A,Bp file
cat file | grep bar
```

pipeline 中首段能确认是纯文件读取时，同样记录该文件被阅读。

---

## 18.1 无法识别的命令

以下形式不做解析：

```text
cat "$(command)"
sed ... $(find ...)
sh -c ...
bash -c ...
命令替换
无法确定读取目标的复杂 pipeline
```

遇到无法解释的 executor command：

```text
跳过该命令的 observation
不影响 Case 归档
```

原则：cheap useful inference preferred。漏识别（false negative）与轻微误识别（false positive）都可接受——误识别最多导致一次多余 refresh 或漏检，不构成安全问题（路径安全是独立硬边界，见 §19）。

---

# 19. Repository containment

Casebook 是可以推送远端的 repository artifact。

因此 evidence 永久化必须满足：

```text
路径属于当前 subject repository
realpath 不逃逸 worktree
不是 .git 内容
不是 Casebook marker/data 自己
不是其它 repository/submodule 的外部内容
```

---

## 19.1 快照范围

快照只永久化：

```text
repository 内、非 .git、非 casebook 自身、realpath 不逃逸 worktree 的文件
```

以下文件**不做快照**（但 Case 照常归档，仅跳过这些证据）：

```text
ignored file
untracked file
外部 file
symlink 逃逸后的 target
submodule / 外部 repository 内容
```

理由：

Casebook 会和 remote 同步，不能把 `.gitignore` 或 repository boundary 变成秘密旁路。

这是单文件证据策略，不是 Case 资格门槛。

---

## 19.2 已跟踪但未提交的修改

Git-tracked 文件即使当前存在 unstaged/staged 修改，仍可以进入 snapshot。

这是刻意行为。

因此启用 Casebook 即表示接受：

> Inspector 对 tracked working-tree 内容形成的 evidence snapshot 可能通过隐藏 Casebook ref 同步到 `origin`，即使该内容尚未通过普通 branch commit 发布。

这一点必须在正式用户文档中明确说明。

如果产品不愿接受该行为，应另行修改批准范围；不得在实现阶段偷偷改成 “只缓存 HEAD”。

---

# 20. fetch observation 的传递性

Inspector A 可以调用：

```text
fetch(old-session-id)
```

然后基于旧 Case 的答案继续调查。

新 Case 的 correctness 不能只记录：

```text
“我调用过 fetch X”
```

否则会形成递归 Case dependency graph。

---

## 20.1 Evidence flattening

当 `fetch(X)` 成功时，runtime 已经得到：

```text
X 的 A
X 的当前 captured observation bundle
X 的当前 snapshot
```

当前 Inspector 的 capture 必须**吸收 X 的 observation bundle**。

因此新 Case publication 时：

```text
current direct observations
+
all fetched Case captured observations
→ flattened observation set
```

Git blob/tree 内容寻址负责物理 dedupe。

---

## 20.2 不建立 fetch dependency graph

Case 不保存：

```text
depends_on = [session-id...]
```

也不在未来 replay 时递归 fetch 历史 Case。

每个可复用 Case 在 publication 时都是 evidence-self-contained。

这防止：

```text
A fetch B
B fetch C
C fetch D
...
```

形成永久链式运行时。

---

# 21. Observation normalization

相同 observation 被 direct read 与 fetched Case evidence 重复带入时，按 canonical observation identity 去重。

identity 必须来自 typed request/evidence，不从字符串猜测。

相同 identity 但 observation 内容矛盾：

```text
以当前 replay 结果为准
```

不得随机选一个，也不得因此拒绝归档。

---

# 22. fetch freshness flow

`fetch(session_id)` 是阻塞工具。

正常流程：

```text
lookup Case
→ replay observations against current worktree
→ compare
```

---

## 22.1 未检测到变化

```text
no detected delta
→ 不启动 Bookkeeper
→ touch last_access
→ local CAS publication（best-effort）
→ best-effort remote sync
→ return exact A.md
```

这是最便宜的 hot path。no-delta 不构成“答案仍然正确”的证明，只是 good enough cache hit。

---

## 22.2 检测到变化

```text
detected delta
→ freeze old/new evidence delta
→ 构造 Bookkeeper request
→ 启动 Bookkeeper
→ Bookkeeper edit-qa*
→ Bookkeeper idle
→ replay/verify current evidence again
```

若当前 worktree 在 Bookkeeper 运行期间未继续漂移：

```text
publish:
  Q
  A
  refreshed snapshot
  refreshed observations
  revision + 1
  new wall_clock
  last_access = now

→ return new A
```

---

## 22.3 maintenance failure ≠ fetch failure

Bookkeeper 失败、subject 持续漂移、publication 失败时：

```text
不 publish 撕裂 candidate
Case 保持不变
return 当前 stored A
```

旧 A 可能过时——这是允许的产品行为（见 §29.1、§30、§36.1）。

---

# 23. Evidence diff

Bookkeeper 不直接读取 repository。

因此 Coordinator 必须把“它需要知道的 repository 变化”预先构造为 self-contained evidence diff。

至少包含：

```text
read snapshot:
  old bytes vs current bytes

file created/deleted:
  full relevant change

glob:
  old canonical result vs new canonical result

grep:
  old canonical result vs new canonical result

recognized executor observation:
  old observation vs new observation
```

内容属于低信任 data。

---

# 24. Bookkeeper

## 24.1 身份

Bookkeeper 是 Casebook feature 专用的**内部 Agent**。

建议 Agent pair：

```text
fast-bookkeeper
deep-bookkeeper
```

但它们是 conditional internal agents：

```text
Casebook disabled
→ 不要求配置存在

Casebook enabled
→ fast/deep pair 必须同时存在并通过启动校验
```

不得因为这个 optional feature 把所有 repository 的 baseline startup 要求从现有 mandatory Agent set 无条件扩大。

---

## 24.2 Tier

调用：

```text
fast-inspector fetch
→ fast-bookkeeper

deep-inspector fetch
→ deep-bookkeeper
```

Bookkeeper fast/deep：

```text
system prompt 相同
tool surface 相同
权限相同
只改变 model binding
```

---

## 24.3 不可见

Bookkeeper 不出现在：

```text
Manager fork-agent enum
Inspector fetch 参数中的 agent enum
Coder inspector enum
list() 可创建 Agent 清单
公开 Role 选择
用户 Agent catalogue
```

它只能由 Casebook runtime 合成。

---

# 25. Bookkeeper Session 所有权

不得为 Bookkeeper 复制一套：

```text
child map
cancel map
recovery state machine
session registry
retire loop
```

Bookkeeper 应复用现有 managed child/Satellite lifecycle machinery。

推荐把它建模为 ephemeral internal Satellite：

```text
Inspector WorkSession
  └── Bookkeeper Satellite for one fetch transaction
```

每次需要 refresh 时创建一个专属 Bookkeeper Session。

该 Session：

```text
只属于这一 fetch transaction
完成后 retire
不跨不同 Case 复用 history
不递归创建 Bookkeeper
不拥有 fetch
```

如实施时现有 SatelliteRuntime 的“一 owner 固定一个 kind”结构无法支持 ephemeral multiplicity，应扩展现有 Satellite owner，而不是复制第二套 runtime。

---

# 26. Bookkeeper tool surface

Bookkeeper 恰好只有：

```text
edit-qa
```

没有：

```text
read
glob
grep
executor
write
edit
mv
rm
fetch
fork
join
list
network
```

---

# 27. edit-qa

`edit-qa` 是新的专用工具。

不得复用普通 `edit` 名称，因为其：

```text
schema
path authority
read-before-edit contract
lifecycle
```

均不同。

---

## 27.1 Schema

逻辑 schema：

```text
document = "Q.md" | "A.md"
old_text
new_text
```

只允许编辑当前 fetch transaction 的两个 staged documents。

不接受 filesystem path。

---

## 27.2 Semantics

执行：

```text
staged document 中寻找 old_text
→ 必须存在且满足确定性 replacement 规则
→ 替换为 new_text
→ 更新 staged bytes
```

不存在/歧义：

```text
tool failure
```

Bookkeeper 可以重复调用 `edit-qa`。

---

## 27.3 绕过 read-before-edit 的正确方式

不得：

```text
伪造 read tool transcript
篡改 Host read cache
调用 unpublished Host API
给普通 edit 偷开权限
```

`edit-qa` 自己拥有 staged Q/A 的内存读写权，因此根本不进入普通 filesystem edit 的 read-before-edit contract。

---

# 28. Bookkeeper Prompt

Bookkeeper system 必须表达：

```text
目标：
根据 supplied evidence change
维护当前 Q/A，使 A 对当前 repository evidence 保持有效。

只能通过 edit-qa 改 Q/A。

如果变化不影响答案，可以零次调用 edit-qa 并直接 idle。
```

---

## 28.1 Dynamic data envelope

至少注入：

```toml
[case]
session_id = '...'

[question]
content = '''...'''

[answer]
content = '''...'''

[repository_change]
patch = '''...'''
```

实际字符串必须由统一 canonical renderer / codec 生成。

Q/A/diff 全部是 data。

任何 diff 中出现的：

```text
# Ignore previous instructions
```

不得逃逸为 Bookkeeper trusted instruction。

---

# 29. Bookkeeper idle

Bookkeeper 运行直到正常 idle。

允许：

```text
0 次 edit-qa
1 次 edit-qa
N 次 edit-qa
```

零编辑表示模型认为 evidence 变化不影响当前 Q/A。

框架不得强迫模型“必须修改一点东西”。

---

## 29.1 Bookkeeper failure

以下情况：

```text
provider failure 最终耗尽
invalid terminal
Session lost
edit-qa contract failure 后未恢复
Bookkeeper lifecycle 无法证明完成
```

则：

```text
fetch 返回当前 stored A
旧 Case 保持不变
旧 snapshot 不推进
```

返回的 A 可能未针对最新 evidence 刷新——这是预期产品语义；refresh 失败不是 fetch 失败（§22.3）。

---

# 30. Subject drift during Bookkeeper

Bookkeeper 可能运行较久。

因此它 idle 后，Coordinator 必须重新验证：

```text
Bookkeeper 输入所基于的 current observation state
==
现在的 current observation state
```

若已经改变：

```text
discard staged Q/A
不得 publish snapshot
```

然后重新开始 refresh。

该稳定化循环必须有限。

建议：

```text
CasebookRefreshMaxAttempts = 3
```

连续 3 次都遇到 subject drift：

```text
返回当前 stored A
```

不得无限等待“repository 终于稳定”。

---

# 31. Publication 原子性

逻辑 Case：

```text
Q
A
meta
snapshot
observations
```

必须一次 publication 原子切换。

利用 Git：

```text
write new blobs
→ build new case subtree
→ build new root tree
→ update-ref CAS
```

在最后 CAS 之前，新 Case 对读者不可见。

---

## 31.1 本地 CAS

使用：

```text
update-ref
  refs/wanxiang/inspector-casebook
  <new-root>
  <observed-old-root>
```

只有 current ref 仍等于 observed root 才提交。

---

## 31.2 CAS conflict

root CAS 因其它 worktree/plugin instance 更新失败时：

```text
read latest root
→ merge candidate mutation
→ retry CAS
```

重试必须有限。

如果同一 `session_id` 被并发修改，并且 competing Case 的 `(revision, wall_clock)` 已胜出，则当前 fetch 不能直接假装自己的 candidate 已永久化。

必须：

```text
重新读取 winner
→ 对当前 worktree 重新 freshness check
→ 必要时生成 revision = winner.revision + 1 的新 refresh
```

若持续竞争超过有限预算：

```text
fetch fail
```

---

# 32. Remote

v1 唯一 remote policy：

```text
若 origin 存在
→ origin 是 Casebook sync remote

若 origin 不存在
→ local-only Casebook
```

不自动推断 upstream branch remote。

不支持多 remote CRDT。

未来若需要其它 remote，另行设计。

---

# 33. Remote refs

Remote canonical：

```text
refs/wanxiang/inspector-casebook
```

Local remote-tracking copy：

```text
refs/wanxiang/remotes/origin/inspector-casebook
```

配置普通 fetch refspec，使无显式 branch/refspec 的：

```text
git fetch
git fetch origin
git pull
```

可以顺便 transport 该 custom ref。

---

# 34. Dumb-remote sync

禁止 server-side merge protocol。

禁止要求：

```text
pre-receive
proc-receive
post-receive
自建 Git server
```

所有 remote 都按 dumb replica 处理。

统一算法：

```text
fetch remote Casebook ref
→ merge(local canonical, fetched remote)
→ local CAS
→ CAS-push merged root
```

---

# 35. Remote CAS-push

Tree ref replacement 采用 explicit lease：

```text
observed remote = R0
merged local    = M

push M
only if remote ref still == R0
```

语义等价于：

```text
--force-with-lease=<casebook-ref>:<R0>
```

同时只 push Casebook exact ref。

不得使用：

```text
blind --force
```

---

## 35.1 Lease rejection

若 push lease rejected：

```text
fetch new remote R1
→ merge(local, R1)
→ CAS local
→ push with expect R1
```

有限 retry。

达到 retry budget：

```text
remote sync deferred/failed
```

但已经成功 publication 的 local Case 仍然有效。

---

# 36. Local correctness 与 remote availability 分离

## 36.1 Local publication failure

若当前 `fetch(session_id)` 需要刷新 Case，而本地 Git CAS publication 无法完成：

```text
Case 保持不变
fetch 返回当前 stored A
```

本地 canonical state 未推进，下次 fetch 会重试 refresh；答案可用性优先于刷新成功。

---

## 36.2 Remote failure

以下情况：

```text
offline
DNS failure
auth failure
remote custom ref rejected
lease contention budget exhausted
```

不得让本地已发布的 Answer 失效。

行为：

```text
local Casebook 保留
fetch 可以正常返回本地 A
remote sync 留到以后
```

Remote 是 replica，不是 authority。

---

# 37. 普通 git fetch 集成

Git 没有专门的 pre/post-fetch product hook。

使用：

```text
reference-transaction
```

作为 fetch-side accelerator。

Hook 只关心：

```text
state = committed
且
ref =
refs/wanxiang/remotes/origin/inspector-casebook
```

其它 ref update 立即忽略。

---

## 37.1 Hook 工作

tracking ref 更新后：

```text
merge tracking remote into local canonical
→ local CAS
→ best-effort CAS-push merged root
```

这样普通：

```text
git fetch
```

也趋向完成双向 convergence。

---

## 37.2 Hook recursion

Hook 内部 Casebook `update-ref` 会再次触发 `reference-transaction`。

必须有确定性 recursion guard，例如：

```text
WANXIANG_CASEBOOK_HOOK_ACTIVE=1
```

内部 ref update 检测到 guard 后立即 no-op。

不得靠：

```text
“应该不会递归”
```

作为假设。

---

# 38. Hook ownership

不得覆盖用户已有的非万象术：

```text
.git/hooks/reference-transaction
```

行为：

```text
hook absent
→ 可安装万象术 shim

hook 已是万象术拥有
→ 可幂等维护

hook 是用户/其它系统拥有
→ 不覆盖、不 rename、不 patch
→ 记录非敏感诊断
→ fetch hook acceleration disabled
```

Casebook correctness 不得依赖 hook 存在。

---

## 38.1 Hook unavailable fallback

即使 hook 不能安装：

```text
fetch(session_id)
Inspector epoch bootstrap
local Case publication
```

仍应主动 merge 已存在的 remote-tracking Casebook state。

因此 hook 只减少同步延迟，不是 correctness dependency。

---

# 39. Bootstrap

Casebook marker 首次被发现时：

1. 初始化 Casebook runtime；
2. 若 `origin` 存在，幂等确保 custom fetch refspec；
3. 尝试安全安装/确认 reference-transaction shim；
4. 若 local tracking ref 尚不存在，可进行一次 casebook-only best-effort bootstrap fetch；
5. fetch 失败不阻止 Inspector；
6. 不创建空 Casebook commit；
7. 在没有 Case 时 canonical ref 可以继续 absent。

---

# 40. Feature disable cleanup

marker 消失后：

```text
provider surface 立即关闭
```

可以幂等移除万象术自己添加的：

```text
remote.origin.fetch exact refspec
reference-transaction hook shim（仅当可证明为万象术拥有）
```

不得删除：

```text
用户 hook
canonical local Casebook ref
remote Casebook ref
Git objects
```

残留 Wanxiang hook 即使尚未清理也必须先检测 marker 并 no-op。

---

# 41. Inspector completion → Case creation

Inspector 正常完成后：

```text
if feature disabled:
    return as today

else:
    Q = full initial Inspector prompt
    A = exact bounded Inspector ToolResult
    snapshot = captured evidence snapshot
    observations = flattened replayable observations
    meta.revision = 1
    meta.wall_clock = now
    meta.last_access = now
    local CAS publish（best-effort）
    best-effort remote sync
    return same A
```

capture 有缺口不阻止归档：能捕获多少就保存多少。

Casebook publication 不得改变原 Inspector caller 已应获得的 Answer bytes。

---

# 42. Publication failure on initial archive

第一次 Inspector 已经成功完成，但 archive write 失败时：

```text
Inspector 原 tool call 仍应返回其正常 A
```

因为 Casebook 是附加可选缓存，不得把一次已经完成的只读调查变成“因为 cache 写失败所以调查失败”。

区别于 `fetch` refresh：

```text
initial archival failure
→ skip cache, original Inspector answer still succeeds

fetch refresh publication failure
→ Case 保持不变，返回当前 stored A
```

两者都不让调用方拿不到答案；区别只在 archive 是否推进。

---

# 43. Casebook 与 Journal

Casebook 当前 tree ref 自身就是 Casebook authority。

不得把完整：

```text
Q
A
snapshot
observation result
Bookkeeper patch
```

复制进 Journal 作为第二份 truth。

Journal/日志如需诊断，只保存：

```text
session id
root/tree/blob OID
revision
byte count
observation count
result/error code
duration
```

不得记录大段 Case 内容。

---

# 44. Casebook 与 Worktree Clean Gate

动态 Casebook 内容只能进入：

```text
Git object database
refs/wanxiang/*
```

不得生成：

```text
.wanxiang/casebook/<session>/
```

这样的动态 worktree 文件。

`.wanxiang/casebook/.keep` 是静态 opt-in marker，正常由 repository commit 管理。

因此 Casebook refresh/sync 不应出现在：

```text
git status
```

也不得使 Orchestrator workspace dirty。

---

# 45. Casebook 与 Inspector “只读”

Inspector 模型本身仍只有只读调查能力。

它不能：

```text
写 source workspace
直接写 Casebook
直接 edit Q/A
调用 update-ref
调用 git push
```

`fetch` 是一个受 runtime 控制的知识读取/刷新工具。

Bookkeeper 对 Q/A 的修改发生在 Casebook staged documents 中，不授予 Inspector filesystem write 权限。

因此“Inspector 对 subject repository 只读”保持成立。

---

# 46. Q/A edit 与 snapshot commit 顺序

必须严格：

```text
freeze old Case
→ replay evidence
→ construct diff
→ Bookkeeper
→ final staged Q/A
→ final current evidence verification
→ build refreshed snapshot
→ build new tree
→ CAS publication
→ return A
```

禁止：

```text
先换 snapshot
再跑 Bookkeeper
```

否则 Bookkeeper 失败后会得到：

```text
new snapshot + old A
```

下一次 replay 将错误地认为没有变化。

---

# 47. Corruption / invalid Case

Case parser 必须验证：

```text
safe session path
Q.md readable
A.md readable and within ToolResult contract
meta.toml schema valid
revision valid positive integer
timestamps parseable
observations schema valid
snapshot path containment valid
Git objects存在且类型正确
```

无效 Case：

```text
不得进入 Inspector index
不得被 fetch 返回
```

Casebook 是 cache，因此可以在后续 deterministic prune/repair 中淘汰坏 Case。

不得从自然语言 Q/A 猜缺失 metadata。

---

# 48. Remote malformed data

Remote Casebook 含非法 Case 时：

```text
非法 Case 不得覆盖本地合法 Case
```

Merge validator 必须先验证 candidate。

不得因为 remote revision 更大就绕过 schema/evidence validation。

---

# 49. Bookkeeper security

Bookkeeper system 中：

```text
Q
A
patch
file content
grep output
glob output
```

全部视为 untrusted data。

唯一 trusted instruction 是框架固定的维护任务。

`edit-qa` 的参数同样是模型输出，必须经过：

```text
document enum gate
size gate
exact replacement gate
UTF-8 gate
```

---

# 50. No second runtime

Casebook flow 必须使用普通结构化程序表达：

```text
let!
match
bounded retry
ordinary task/resource scope
```

不得创建：

```text
CasebookStage
FetchPhase
BookkeeperPhase
SyncStateMachine
Command/Reply interpreter
Step AST
```

`revision` 是真实 replica data，不是程序计数器。

`PrefixEpoch` 是已有领域事实，不新增 CasebookGeneration 类伪阶段。

---

# 51. 并发

## 51.1 不同 Case

不同 `session_id` 可以并发准备 blobs/trees。

最终 root mutation 经 Git CAS 收敛。

---

## 51.2 同一 Case / 同一 worktree

同一 Inspector runtime 对：

```text
(worktree identity, session_id)
```

建议 single-flight `fetch` refresh，避免同时启动两个 Bookkeeper。

该 single-flight 是进程内物理所有权，不写入 meta/Journal。

---

## 51.3 不同 worktree 同一 Case

允许并发。

因为各自针对不同 current worktree validation。

两边可能产生：

```text
revision = N+1
```

不同 candidate。

Replica merge：

```text
revision
→ wall_clock
→ deterministic arbitrary OID tie
```

最终只保存一个 canonical current Case。

另一个 worktree 下一次使用时再次 replay observations；若不适合其 tree，会再次 refresh。

这是预期 eventual behavior，不建立 multi-version Case。

---

# 52. Remote convergence

只要：

```text
两 replica 后续能互相 fetch
且没有无限新的 mutation
```

重复执行：

```text
union
LWW merge
last_access max
deterministic prune
CAS-push
```

应 eventually 收敛到相同 root tree。

不要求 vector clock、HLC、CRDT conflict set。

---

# 53. timestamp 同值

若两个并发更新产生：

```text
same revision
same wall_clock
```

无需进一步业务语义。

任取一个即可。

实现只需选择稳定的 arbitrary winner，以避免同一 merge 输入产生随机 root。

不得因此增加：

```text
replica_id
Lamport clock
vector clock
UUID timestamp
```

到产品 schema。

---

# 54. Sync failure 不建立第二状态机

不保存：

```text
PendingSync
NeedsPush
RemoteDirty
SyncGeneration
```

下一次合法同步入口直接从：

```text
local canonical ref
remote tracking ref
remote observed ref
```

重新计算该做什么。

物理状态就是事实。

---

# 55. Git reflog / history

Casebook canonical ref 不主动创建 reflog。

不要求：

```text
--create-reflog
```

产品不提供历史恢复。

Git 因自身配置临时留下 unreachable object 或 reflog，不改变产品语义。

---

# 56. Git GC

当前 canonical ref 保证当前 Casebook tree reachable。

活跃 Inspector epoch pin 保证其 frozen index root reachable。

未被 canonical/pin 引用的旧 Case objects 可以由正常 Git GC 回收。

不得自己实现第二套 object GC。

---

# 57. Formal specification impact

实施该 Change 时，应建立一个独立正式主题，例如：

```text
docs/why/inspector-casebook.md
docs/what/inspector-casebook.md
docs/shape/inspector-casebook.md
docs/how/inspector-casebook.md
docs/proof/inspector-casebook.md
```

Change 文件本身不定义正式 Clause ID。

---

## 57.1 architecture

需要正式化：

```text
Casebook best-effort freshness vs replica convergence 分离
Casebook raw Git ref 不进入 worktree
PrefixEpoch Casebook index freeze
Casebook 不制造新 epoch
不建立第二运行时
```

---

## 57.2 agent

需要修改/补充：

```text
Inspector conditional fetch tool
Bookkeeper internal Agent
conditional fast/deep Bookkeeper pair
feature disabled 时不要求 Bookkeeper config
Bookkeeper 不可见
edit-qa 唯一工具
```

---

## 57.3 host / execution

需要正式化：

```text
Bookkeeper ephemeral managed/Satellite lifecycle
owner / cancel / retire
fetch blocking semantics
single-flight
无第二 child runtime
```

---

## 57.4 projection / companion / context

需要正式化：

```text
Inspector system CasebookIndexSnapshot
同 epoch 字节稳定
probe promotion 继承 frozen index
Casebook 更新不制造 epoch
低信任 Q list containment
```

---

## 57.5 synthetic TOML

需要把以下 LLM-visible surface 纳入现有统一 renderer/inventory：

```text
Inspector Casebook index
Bookkeeper dynamic Q/A/evidence envelope
fetch/edit-qa 自定义 tool text result（如适用）
```

---

## 57.6 persist

需要明确：

```text
Casebook 不属于 Journal
Casebook custom Git ref 是自己的 authority
Journal/log 不复制 Q/A/snapshot
```

---

## 57.7 orchestrator

需要 proof 确认：

```text
Casebook refresh/sync 不产生 worktree dirty
Clean Gate 行为不变
```

---

# 58. 推荐实现分层

## Domain / Kernel

纯类型与算法：

```text
CaseRevision
CaseMetadata
Observation
ObservationIdentity
ObservationReplayResult
CaseMerge
CasebookMerge
LruPrune
```

纯函数：

```text
compareCase
mergeCase
mergeCasebooks
prune
classifyReplay
normalizeObservations
```

不得 Git I/O。

---

## Application

结构化 workflow：

```text
archiveInspectorResult
fetchCase
refreshCase
publishCase
syncCasebook
```

直接使用 CE / Task / match。

---

## Infrastructure

仅实现能力：

```text
Git object read/write
tree materialization
update-ref CAS
fetch custom ref
push with explicit lease
reference-transaction shim
filesystem evidence reads
Host tool observation adapter
SyntheticToml renderer
wall clock
```

---

## Session / Process

物理 ownership：

```text
Inspector epoch pin
same-worktree fetch single-flight
Bookkeeper child lifetime
hook recursion guard
```

不得把这些镜像成长期领域状态机。

---

# 59. 推荐主要端口

概念上保持具名 capability，不建立 generic Git Command bus：

```text
readCasebookRoot
readCase
writeCaseTree
compareAndSwapCasebookRoot

fetchRemoteCasebook
pushRemoteCasebookWithLease

captureReadObservation
replayObservation

startBookkeeper
awaitBookkeeperIdle

pinCasebookRoot
releaseCasebookPin
```

---

# 60. 测试与 proof：Feature gating

必须证明：

```text
marker absent
→ Inspector 无 fetch schema
→ ToolRegistry execute fetch 也拒绝
→ 无 Casebook index
→ 无 Bookkeeper config requirement
→ 无 archive
→ hook no-op
```

双门都必须测：

```text
provider schema
execution registry
```

不能只隐藏 schema。

---

# 61. 测试与 proof：Q/A

必须证明：

1. 新 Case Q.md 逐字等于完整 Inspector initial prompt；
2. Q 不经过摘要；
3. A.md 逐字等于实际 Inspector ToolResult body；
4. oversized Inspector answer 先走现有 ToolResultBound，再写 A；
5. Bookkeeper 可以改 Q；
6. Bookkeeper 可以改 A；
7. Bookkeeper 可以连续多次 edit-qa；
8. 零 edit idle 合法；
9. edit-qa 不能写第三个文件；
10. Bookkeeper 最终 A 仍满足 ToolResultBound。

---

# 62. 测试与 proof：Observation capture

至少覆盖：

```text
read full file
read range
glob zero result
glob multi result
grep zero result
grep multi result
文件 deletion
文件 create
文件 rename 导致 glob/grep 变化
```

并证明：

```text
capture 不完整（如 executor 命令无法识别）
→ original Inspector 成功
→ Case 照常归档
→ 缺失的 observation 只是未来少一次变化检测机会
```

---

# 63. 测试与 proof：executor parser

正例（必须识别为 observation）：

```text
cat file
cat -n file
head file
head -n 30 file
tail -100 file
tail -f file
sed -n '20,80p' file
cat file | grep bar
```

负例（必须安全跳过、不报错、不阻止归档）：

```text
cat "$(...)"
sh -c ...
bash -c ...
命令替换
无法确定读取目标的复杂 pipeline
```

负例命中：

```text
该命令的 observation 被跳过
Case 仍归档
```

---

# 64. 测试与 proof：路径安全

覆盖：

```text
tracked file
tracked modified file
untracked file
ignored file
.git path
../ escape
absolute external path
symlink escape
submodule/external repo
```

批准范围内 evidence 永久化；范围外（untracked/ignored/external/.git）证据跳过，Case 仍归档。

---

# 65. 测试与 proof：Freshness

## unchanged

```text
Case old snapshot
current worktree identical
fetch
→ Bookkeeper zero launches
→ A exact
```

## changed read

```text
file bytes changed
→ 启动 Bookkeeper refresh（一次 refresh flight）
→ 成功返回新 A；失败返回旧 A
```

## changed glob

```text
new matching tracked path
→ 检测到 delta → 启动 refresh
```

## changed grep

```text
matching line set changes
→ 检测到 delta → 启动 refresh
```

这三类尤其证明：

> freshness 检测不限于“被 read 的旧文件没变”，而覆盖 negative/search evidence。

## refresh failure

```text
changed evidence + Bookkeeper 失败
→ 返回旧 A
→ Case/snapshot 不推进
```

---

# 66. 测试与 proof：Bookkeeper failure

故障注入：

```text
create Session fail
provider fail
edit-qa fail
invalid terminal
idle wait fail
```

期望：

```text
old root OID unchanged
old snapshot unchanged
fetch 返回旧 A（内容可能过时，这是预期语义）
```

---

# 67. 测试与 proof：Subject drift

剧本：

```text
replay change A
→ Bookkeeper starts
→ worktree changes to B before idle
```

必须：

```text
candidate discarded
snapshot A 不提交
重新 refresh
```

连续超过 bounded attempts：

```text
返回旧 A
```

---

# 68. 测试与 proof：Atomic publication

在以下每一点故障注入：

```text
blob write
subtree build
root tree build
CAS
```

Casebook reader只能看见：

```text
old complete Case
或
new complete Case
```

绝不能：

```text
new Q + old A
new A + old snapshot
```

---

# 69. 测试与 proof：Local multi-worktree CAS

模拟：

```text
A observes T0
B observes T0

A publishes T1
B CAS T0→T2 fails
B reloads T1
B merges candidate
B publishes T3
```

最终 root 同时包含双方不冲突 Case。

---

# 70. 测试与 proof：same-case conflict

两个 worktree：

```text
revision 10
```

同时 refresh：

```text
A → (11, t1)
B → (11, t2)
```

必须选：

```text
max((11,t1),(11,t2))
```

相同 timestamp 再按 arbitrary deterministic content OID 收敛。

---

# 71. 测试与 proof：Remote sync

覆盖：

```text
remote absent ref
remote same ref
remote newer case
local newer case
双方不同 cases
双方同 case conflict
lease rejected once
lease repeatedly rejected until budget
network failure
auth failure
remote rejects tree custom ref
```

Remote failure 不得回滚已成功 local publication。

---

# 72. 测试与 proof：ordinary git fetch integration

证明：

```text
remote tracking Casebook ref 更新
→ reference-transaction committed hook sees exact ref
→ local merge
```

并证明：

```text
普通 branch/tag ref update
→ Casebook hook no-op
```

以及 recursion guard。

---

# 73. 测试与 proof：existing hook

准备用户自定义：

```text
.git/hooks/reference-transaction
```

Feature bootstrap：

```text
不得覆盖
不得 rename
不得修改 bytes
```

Casebook 仍可通过非 hook 路径工作。

---

# 74. 测试与 proof：Prefix stability

同 Inspector PrefixEpoch：

```text
request 1 index = I0

后台：
新增 Case
fetch Case
LRU touch
remote merge

request 2 index 必须仍逐字 = I0
```

只有下一合法 epoch 建立时才允许 index 变为 I1。

---

# 75. 测试与 proof：probe boundary

专门测试：

```text
candidate epoch index selected = I1
probe provider sees I1
probe promoted
next request still I1
```

禁止：

```text
probe 时 I1
promotion 后重新 sample 得 I2
```

---

# 76. 测试与 proof：Epoch pin

```text
epoch index 包含 Case X
→ canonical LRU 淘汰 X
→ Git GC 可运行
→ active epoch fetch(X) 仍可从 pin 读取
```

Epoch retire 后 pin 必须清理。

---

# 77. 测试与 proof：fetch evidence flattening

剧本：

```text
Case A 已存在
Inspector B calls fetch(A)
B 再 read file2
B 完成
```

B 的 observations 必须包含：

```text
A 的 captured evidence
+ B direct evidence
```

之后删除/淘汰 A：

```text
fetch(B)
```

仍可独立 freshness replay。

不得要求 A 仍存在。

---

# 78. 测试与 proof：LRU

验证：

```text
successful fetch updates last_access
content revision 不变

Bookkeeper refresh
revision +1
wall_clock changes
last_access updates

merge
last_access = max
```

同 ties 必须 deterministic。

---

# 79. 测试与 proof：Clean Gate

执行：

```text
Inspector archival
fetch refresh
LRU touch
remote merge
remote push
```

前后：

```text
git status porcelain
```

subject worktree 状态必须完全不因 Casebook 动态数据改变。

---

# 80. 测试与 proof：知识旁路

扫描：

```text
Journal
normal diagnostic logs
runtime metadata
Host business facts
```

不得出现不必要的完整：

```text
Q.md
A.md
snapshot
Bookkeeper patch
```

Casebook Git objects 是这些数据的唯一新永久化面。

---

# 81. 发布完成判据

本 Change 只有同时满足以下条件才可关闭：

1. feature disabled repository 的 Inspector 行为与当前完全兼容；
2. marker 启用后 Inspector 有 conditional `fetch`；
3. Inspector Case 完成后可形成 Q/A/evidence Case（capture best-effort）；
4. Q 初始完整，不摘要；
5. A 与实际 bounded ToolResult 一致；
6. read/glob/grep observation 可重放；
7. 未识别的 executor 命令只跳过其 observation，Case 仍归档；
8. external/untracked/ignored/.git 证据不永久化，Case 仍归档；
9. unchanged evidence 不启动 Bookkeeper；
10. changed evidence 触发 best-effort refresh；refresh 失败返回旧 A；
11. Bookkeeper 只有 `edit-qa`；
12. Bookkeeper 可修改 Q 和/或 A；零 edit idle 合法；
13. Bookkeeper failure 不推进 snapshot，fetch 返回旧 A；
14. Bookkeeper idle 后重新验证 subject stability（有界重试）；
15. Q/A/snapshot publication 为一个 Git-tree CAS；
16. Casebook canonical state 是 custom tree ref，不创建 commit；
17. 所有 worktree 共享同一 canonical ref；
18. 动态 Casebook 内容不污染 worktree；
19. full-Q Inspector index 同 PrefixEpoch 字节稳定；
20. Casebook mutation 不主动切 PrefixEpoch；
21. epoch pin 防止活跃 index Case 被 LRU/GC 破坏；
22. `fetch` 使用 current worktree 做 freshness replay；
23. fetched Case evidence 被 flatten，不形成递归 dependency runtime；
24. replica content merge 只用 `(revision, wall_clock)`；
25. timestamp/revision 与 no-delta 都不构成正确性证明；
26. last_access 独立 max；
27. deterministic LRU 有界；
28. remote 统一采用 dumb `fetch → merge → CAS-push`；
29. remote push 必须 explicit lease，不 blind force；
30. lease/network failure 不破坏 local Case；
31. reference-transaction hook 只是同步 accelerator；
32. 不覆盖现有用户 Git hook；
33. 没有 server-side hook requirement；
34. 没有 Casebook commit/version/history API；
35. 没有第二运行时、Stage/Phase/Sync state machine；
36. Inspector prompt 明确 fetch 免费（§1.3）；
37. why/what/shape/how/proof 与实现、proof tests 全部闭环；
38. spec/lint/architecture/DSL/static gates 与相关 unit/integration/e2e 全绿；
39. 新增静态门均有受控反例证明能够判红。

---

# 82. 推荐实施顺序

为避免边写边发明语义，建议实施者严格按以下顺序工作。

### Step 1 — 正式规范

先建立 Casebook 的：

```text
why
what
shape
how
proof
```

并同步更新：

```text
architecture
agent
host/execution
projection/context
synthetic-toml
persist
orchestrator proof
glossary/navigation
```

先把所有权和硬边界写完整。

### Step 2 — 纯 Domain

实现并测：

```text
metadata parse/render
case validation
observation identity / dedupe
LWW merge
last_access merge
LRU prune
```

无 Git/Host I/O。

### Step 3 — Git raw store

实现：

```text
tree read
blob/tree build
local CAS
pin refs
root validation
```

先证明多 worktree local correctness，不碰 remote。

### Step 4 — Observation capture/replay

接：

```text
read
glob
grep
recognized executor
path safety
tracked gate
flattened fetch evidence
```

做到“能捕获多少捕获多少；不可解释的证据跳过；路径安全 gate 拦截非法证据”。

### Step 5 — Inspector archive

把现有 Inspector terminal ToolResult 接到：

```text
Q
A
observations
snapshot
local Case publication
```

保证 archive failure 不影响原 Inspector Answer。

### Step 6 — Inspector fetch hot path

先实现：

```text
lookup
replay unchanged
return A
last_access touch
```

证明不启动 Bookkeeper。

### Step 7 — Bookkeeper

增加：

```text
conditional internal pair
ephemeral lifecycle
system TOML
edit-qa
changed-evidence refresh
post-idle stability verify
```

### Step 8 — Prefix index

最后接 Prompt projection：

```text
full Q list
epoch freeze
candidate epoch sampling
pin root
```

避免先污染 provider prefix 再补 seal 证明。

### Step 9 — Remote

实现：

```text
origin custom refspec
fetch tracking ref
pure merge
lease push
bounded retry
```

Remote failure全部保持 local success。

### Step 10 — Git fetch accelerator

最后加：

```text
reference-transaction shim
ownership detection
recursion guard
feature-disabled no-op
```

Hook 不是核心 correctness 路径，因此必须最后接入。

### Step 11 — Crash/concurrency/e2e

完成：

```text
CAS races
Bookkeeper drift
GC/pin
LRU
remote contention
hook conflict
feature off compatibility
```

之后再申请 Reviewer。

---

# 83. 最终设计原则

本 Change 最终应保持以下五句话成立：

```text
1. Casebook 是 best-effort semantic cache，不是第二个产品数据库，
   也不是证明系统；允许旧答案过时或不准确。

2. Git ref 解决“当前缓存状态放哪里”；
   observation replay 提供 freshness hint，不证明答案正确。

3. revision / wall_clock 只解决 replica 谁赢，
   永远不解决答案对不对。

4. Inspector 仍然只读 subject repository；
   Bookkeeper 只能编辑 staged Q/A。

5. 如果 Casebook marker 不存在，
   整个能力应当像从未实现过一样消失。
```
