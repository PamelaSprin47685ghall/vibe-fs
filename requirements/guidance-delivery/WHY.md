# guidance-delivery — 为什么必须独立存在

## 1. 一个不可替代的存在理由

「这条诊断成立」和「现在应该把这条诊断的处置手册给 Main 看吗、给全文还是给身份」
是**两个不同的问题**。前者是 `behavior-diagnosis` 的领地（evidence 是否满足
trigger/negative/distinction）；后者是本包：**如何把已成立的 diagnosis 变成当前
horizon 内可恢复、不无限膨胀、不伪造新事件的交付**。

历史上最容易犯的两个错：

1. **每次重复全文**：每条 tip 每轮把整篇 `main.md` 塞给 Main。上下文无界膨胀，
   而且「已交付过」与「没交付过」无法区分——模型只能靠记忆猜。
2. **reanchor 后永久丢失或悬空引用**：compaction/重锚把全文挤出 horizon 后，
   要么假装「已交付过」继续只发身份（Main 看到 `tip: x` 但不知道 x 是什么），
   要么把重发全文误记成一次新的 pathology occurrence（历史被重写污染）。

本包的存在理由：**delivery 必须有一组独立于 diagnosis 的语义——occurrence 维度
的单调 Frontier、horizon 维度的语义 Coverage、重复时的 dedupe policy、以及
「重发全文 ≠ 新 occurrence」的边界**。它们不能被一个 durable bool、一个内存
HashSet 或一个文件 ledger 替代。

## 2. 历史上为什么 RED（archive/changes/ 考古）

### 2.1 从「不投 Main」到「双消费者」的反复（`archive/changes/completed/rulebook.md` §13/§27）

Enforcer rebase 文档一度声称「tip 只作为 Blogger history，不投 Main」；而 HOST
实现又规定 prior tip 会进入新的 Main auto-injected pair。两种叙述并存 = 双解释。
Rulebook v2 裁决：**tip 有两个消费者**——Blogger（配对历史观察）与 Main（Host
adopted guidance），两者来源相同、权限语义不同（§27），且不得共用 renderer（§28）。

### 2.2 为什么拒绝「Main fake-user overlay」（`archive/docs/why/enforcer.md`）

向 Main 注入工程 fake-user message = 给 Main 建立第二个 Authority 解释器，污染
投影、seal 与恢复。Main tip 半边必须是正式交付事实（`TipGuidanceDelivered`）+ 
auto-injected tool pair，经投影进 horizon，**不 mint authority**。

### 2.3 为什么拒绝单一 durable bool（`archive/docs/why/enforcer.md` 交付前沿 vs 语义覆盖）

「已交付」≠「全文此刻仍可从 horizon 恢复」。单一 bool 在 reanchor 后要么误删已
交付事实、要么假装全文仍在 horizon。所以：

```text
TipDeliveryFrontier    occurrence 单调；ContextReanchored 不重置
TipSemanticCoverage    TipName / horizon-relative；ContextReanchored 可清空
```

覆盖丢失后再次给出 full main.md 是语义恢复，不是新 occurrence——拒把二者压成
一个 durable bool（ENFORCER-071）。

### 2.4 为什么拒绝「每次 Full」和「仅 Identity」（`archive/docs/why/enforcer.md`）

- 每次 Full：重复烧上下文且无法区分「已交付」。
- 仅 Identity：首次无正文可执行。
- 选 Full 一次 + Identity 重复，且 Identity 仅当 Coverage 仍可恢复全文时合法。

### 2.5 历史字节必须冻结（`archive/changes/completed/rulebook.md` §17）

第一次投递 `main.md` = version A 并 commit 后，repository 更新为 version B：
restart 后历史 pair 必须从 EventStore **byte-identical replay** 当时实际送出的 A，
不得把历史改成 B。substrate 是 EventStore（payload_refs），不是私有 journal/blob
旁路（HOST-013 `MarkerText` 定义）。

## 3. 边界：什么**不**归本包

- diagnosis 是否成立 —— `behavior-diagnosis`。
- provider projection mechanics（Synthetic TOML / renderer）—— `provider-projection`
  （delivery 只消费其输出，不拥有渲染）。
- horizon admission general law —— `participant-horizon`。
- 当前 `main.md/enforcer.md` 物理布局 —— 资源实现细节。
- interaction authority 的创建/继续权 —— `interaction-authority`；delivery 只经
  投影进 horizon，不 mint authority。

## 4. FAILURE MEANING

RED = guidance 可无限重复（无 dedupe）、reanchor 后永久丢失（Coverage 与 Frontier
不分）、或重新交付被误记成新 pathology occurrence；或 IdentityOnly 在全文不可
恢复时仍被发出（悬空引用）；或交付路径创建了新的 interaction authority。
