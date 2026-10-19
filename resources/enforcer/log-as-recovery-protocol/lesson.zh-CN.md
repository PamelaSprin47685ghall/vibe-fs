# log-as-recovery-protocol — Lesson

## 评价

### 定义

Log-as-recovery-protocol 的缺陷，是 restart logic 把**写给人看的 diagnostic output**当成“什么事实真正 commit 过”的 authoritative record。

“log”这个词本身很混乱，所以必须按 semantics 判断，不能按文件名判断。

一个真正 event journal 也可能是 append-only text/JSON，甚至文件就叫 `.log`。只要它有 schema、durability guarantee、ordering model、stable identity、atomic commit、replay contract，而且系统就是有意把 business fact 写在那里，那它是 durable fact store，不是本规则的坏味道。

### 支配原则

本规则打的是：**diagnostic commentary 被抬成 machine truth。**

Diagnostic channel 天然可能更弱：

- effect commit 前就先 emit message；
- process crash 时 buffer 还没 flush；
- line 会 sampled、duplicated、reordered、rotated、truncated、redacted、dropped；
- wording 会因 clarity/localization 改；
- structured logging pipeline 会 transform field；
- 一个 business fact 可能打多条 log，也可能一条没有；
- retry 可能重复打印同一句，但真正 effect 只 commit 一次。

这些对 observability 不一定是 bug；但 recovery 一旦依赖 presence/order/wording，就会致命。

### 何时触发

以下情形触发：

- restart grep `INFO order committed` 决定哪些 order 存在；
- daemon 从上次 stdout/stderr 找“最后成功 step”；
- 因为 JSON log “已经 structured”，就直接拿来当 lifecycle record；
- 没有某条 log 被当成“effect 没发生”的证据；
- async worker 的 log arrival order 被当 causal order；
- 为了 recovery parser，operator message 不敢改 wording；
- metric/tracing event 被当 business completion 的 durable source。

### 不应触发

不要误杀纯解释性 log。也不要误杀真正 durable event journal；如果 observability event 本身就拥有完整 commit contract，那就把它正式称为 journal，别把 durability 当 logging 的偶然属性。

### 与相邻规则区分

邻近规则：

- `stringly-typed-error`：normal control flow 依赖 human error prose；
- `recovery-by-filesystem-state`：restart 从 artifact residue 猜 progress；
- `memory-before-disk`：runtime state 先于 durable fact；
- `unrecorded-decision`：关键 decision 根本没有 durable record。

### 判定程序

决定性实验：完全 suppress diagnostic output，但 business effect 与 durable store 保持。Recovery 如果因此改变，observability 就被授予了不该有的 authority。

反方向也要测：只 emit 同一句 diagnostic line，但不 commit underlying effect。如果 recovery 因此相信 effect 发生，log 已经成了 counterfeit testimony。

### 例子


## 建议

### 现在该做什么

把 restart authority 移到真正为 commitment 设计的 channel。

对每个 recovery question，找出能带着所需 guarantee 回答它的 source：

- 哪个 operation 被请求；
- 它是否 commit；
- 哪个 logical identity commit；
- durable current state 是什么；
- external effect 是 known success、known failure 还是 unknown。

这个 source 可以是 event journal、transaction table、durable command inbox/outbox、provider status endpoint、authoritative database row，或其他 typed store。关键是它真正拥有 recovery 所需 semantics，而不是刚好在附近打印过一句话。

### 为什么重要

然后把 diagnostic log 降回本职：explanation、correlation、debugging、operator context。Log 可以带 ID 帮人定位 authoritative fact，但不能**自己冒充 fact**。

### 修复策略

如果现有 diagnostic channel 真要升级成 recovery store，就正式升级：定义 typed schema、stable identity、commit boundary、durability、ordering、retention、replay、corruption semantics、migration。做到这一步后，它已经不是“just logging”，而是 journal，ownership 名字也应该改，防止未来代码继续把 observability guarantee 当偶然福利。

### 决策分支


### 常见假修复

常见假修复：

- 冻结 log wording、加 version，就当 protocol，但 durability/atomicity 完全没定义；
- plain text 换 JSON，就觉得 structure = commitment；
- journal 与 log 双写，restart 因 grep 方便反而优先 log；
- transaction commit 前先 emit “committed”，靠“这几行之间不会 crash”的约定维持；
- trace/span completion 被当 underlying effect commit 的证据；
- “real store 丢了时用 log fallback”，结果真实 authority 丢失被静默掩盖；
- child process 明明有 status/result protocol，却解析 stdout 重建 business state。

### 验证

验证必须证明 diagnostics 与 recovery 解耦：

1. suppress/drop/rotate human log → recovery 不变；
2. duplicate/reorder diagnostic message → recovery 不变；
3. wording/localization 改 → recovery 不变；
4. emit diagnostic 但 business fact 不 commit → recovery 不能相信；
5. business fact commit 但 log 被 suppress → recovery 仍必须相信。

Structured observability 还要测试 sampling。Tracing backend 丢掉 10% span，不能让任何 business fact 因此不可恢复。

Retention 也要看。Recovery truth 不能活在一个会按 observability 运维策略自动删旧记录、而与 business retention 无关的 channel 里。

### 完成条件

完成时每个 recovery decision 都能指出一个 typed durable authority，而任何 log line 全部消失也不会改变 machine belief。

> 好 log 告诉人“系统相信什么”；recovery protocol 告诉系统“它有资格相信什么”。

### Nudge

> Diagnostics 可以描述 history；它不应该成为 history 被相信的唯一理由。
