# semantic-trace — WHY

## 1. 不可替代的存在理由

X（Work Session）的一生会跨过：多次 provider attempt、Peer Fallback 换模型、Host compaction /
reanchor、Strength 投机调查、process review、Finality。这中间每一环都需要回答同一个问题：

> 当时到底发生了什么？

答案不能是「现在还能看见什么」：

- Host transcript 数组下标会被 compaction 重编号（HOST-006 重锚）；
- session head 摘要随「读者是谁 / 什么时候读」变化；
- provider 换绑定后 model 名变了，但历史语义没变。

**semantic-trace 保证：X 的原始语义历史有一个 append-only、与传输噪声无关、可精确定位的事实表示（XTrace）。**
后续 review / context / recovery 消费它时，能证明「这段工作记录对应哪个真实历史 frontier」，而不是猜。

## 2. 独立存在测试（Independent Change Test）

把 XTrace 的存储 / part 编码 / 文件布局整体重写——只要 semantic capture、frontier 与 provenance
合同不变，其它包（work-record、context-compression、prefix-stability、review-assurance）的 WHAT
一律不需要改。反过来，把「语义 capture 边界」改掉（例如允许 Activity part 进历史、允许
未 promote 的 Candidate 写入），会让所有下游对「发生了什么」的判断失真——这是一个独立的失败域。

## 3. 失败意义（FAILURE MEANING）

RED = 满足下列任一：

1. 后续系统无法证明一段工作记录对应哪个真实历史 frontier（cursor 不单调、可回退、可重复）；
2. 临时 / 未发生材料可以污染 canonical history（speculative Candidate 入迹、失败 probe 写事实）；
3. 同一段历史存在两个互相矛盾的解析（Y delta 与 LWR gap 分叉）；
4. Host compaction 删除 / 归零了 XTrace。

## 4. 历史考古：曾经 RED 过什么

### 4.1 `strength.md`（changes/completed/strength.md）—— Candidate 差点变成历史

Replica 行为是 intervention。primary 尚未消费时，它既不是用户行为，也不是 primary 已发生的因果历史。
「提前写入 XTrace/Companion 会让未发生的世界污染未来请求；反过来，primary 已消费后若重启时丢失，
又会删除真实因果历史。」因此必须以 durable Candidate → consumption proof → Promotion 分开
「准备好」与「已经影响 primary」。`StrengthCandidatePrepared` 只是孤立的准备事实，不进入活动历史；
`StrengthReplay` 只处理 durable Promoted frames，**绝不重放 Candidate**（strength.md §6/§11/§14）。

这正是 semantic-trace 的 capture 侧负律：**未 Promote 的 Candidate ≠ 历史**（HANDOFF §18.6 的
cross-boundary invariant；本包拥有 capture 侧，promotion 因果归 `speculative-investigation`）。

### 4.2 `cursor-pair-hint.md` / `cache.md` —— XTrace 与 HOST-013 的互斥

HOST-013 synthetic pair 是**会影响 prompt bytes、Prefix Cache、ReviewSeal 的合成历史**，但
pair 正文**不得进入 XTrace / Companion decode / Blogger delta / work record / compaction input**
（HOST-013 行为约束 4）。XTrace 只记真实语义材料；合成 marker 的 durable 投影事实单独存在。
这条排除线属于 prefix-stability 的 HOST-013 部分（见该包），本包负责「XTrace 里没有 synthetic 正文」这一半。

### 4.3 X9 之前：`context_ratio` 式容量估算进 Journal

`ctx014.test.mjs` 的注释记录：估算器曾在 X9 被删除，本测试是 tombstone。容量估算字段一旦进
Journal，就会把「模型窗口猜测」写成产品事实——这属于 `context-compression` 的 CTX-001/014，
但教训同构：**Journal 里只许写已发生、可重放的事实**。

### 4.4 XTrace capture 曾因 provenance 命名空间不一致而全量重写

`x-trace-capture-hardening.test.mjs` 头部记录 review 发现的两个 blocking 缺陷：

1. `captureProjection` 幂等失效：recorded 集合与 fold 存储的 provenance 命名空间不一致，
   导致每轮 transform 全量重写 XTrace；
2. opening 捕获曾嵌套 transport envelope：fork 的 AgentOwnerRoot 首 prompt 是渲染信封
   （含 parent_work_record），child opening 必须是原始 assignment。

两者都是「capture 边界」问题：provenance 是幂等的唯一身份、opening 是 preserved 而非重建。

## 5. 与相邻包的边界

| 看似相邻 | 为什么不归本包 |
|---|---|
| prefix epoch / 字节稳定 | trace 记录**事实**；「已呈现前缀不重排」是 provider 边界保证 → `prefix-stability` |
| 何时/哪些历史可压缩 | 那是压缩 policy → `context-compression` |
| Candidate 何时 promote | 那是投机因果 → `speculative-investigation` |
| 事件如何落盘 / fold 拒绝 | substrate → `durable-events`（本包消费其 guarantee） |
| 一段 work 的 canonical 陈述 | 那是 work-record（从 trace 物化，不是第二事实源） |

## 6. 源材料

- `docs/why/host.md`、`docs/what/host.md`（HOST-005）、`docs/what/companion.md`（COMPANION-003/007/014）
- `docs/why/context.md`、`docs/what/context.md`（CTX-015/016 交叉）
- `docs/why/strength.md`（Candidate ≠ 历史）、`changes/completed/strength.md`
- `requirements-design/13-context-continuity.md`（semantic-trace card）
- `requirements-design/COVERAGE.md`（HOST-005 / COMPANION-003 / COMPANION-007 行）
- `requirements-design/EVIDENCE.md`（semantic-trace 行：`Domain/XTrace.fs` 等）
