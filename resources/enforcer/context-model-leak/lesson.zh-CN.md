# context-model-leak — Lesson

## 评价

### 定义

Context model leak 不是“共享类型不好”。真正的问题是：一个 master model 被多个 bounded context 复用，而这些 context 对同一字段赋予不同意义、invariant、lifetime 或 authority。

Shape 一样不代表 concept 一样。Auth 的 User、Billing 的 Account Holder、Session 的 Participant 都可能有 `id/name`，但它们回答的问题不同。共用 mega-model 会让某个 context 为自己新增的字段，自动变成所有 context 都“看得见且似乎有意义”的事实。

### 支配原则

形状相同不等于概念相同。Auth 里的 User、Billing 里的 Account Holder、Session 里的 Participant 可能共享同一个 identifier 与 display name，却回答着完全不同的问题。复用同一个 model 会让这些 context 共享超出领域本身的知识，于是为一个目的新增的字段会悄悄变得处处可见——而且看似处处有意义。

### 何时触发

- 一个 `User` 同时携带 credential、balance、session state、UI preference；
- 很多字段在某些 context 中只能 nullable，因为“这里用不到”；
- 一个 context 修改字段，另一个 context 被迫重编译/迁移，却没有业务原因；
- authorization/lifecycle rule 开始根据“当前是哪种使用场景”解释同一对象；
- context boundary 只传整个 master object，而不是所需 facts。

### 不应触发

- `Money`、opaque ID 等 tiny value object 在各处语义完全相同，可以共享；
- context 之间传同一 identity，不代表传同一 model；
- persistence DTO 在一个 context 内映射成本地 model，不是 leak；
- 不要为了“bounded context”把真正同一概念复制出不同空壳类型。

### 与相邻规则区分

`boundary-collapse` 更广，任何 internals 越界都算；这里专门指**同一 representation 被迫扮演多个 context 的概念**。

`primitive-obsession` 是概念身份太弱；这里常常恰好相反：一个“很强的大类型”强到吞掉多个概念。

### 判定程序

问每个字段：**这个 context 为什么有权知道它？它在这里有什么 invariant？**

若答案是“因为 master model 里本来就有”，边界已经失去语义选择。

### 例子

- 正例：全系统共享一个 `User`，含 password hash、credit limit、theme、activeSessionId。
- 近邻：各 context 共享 `UserId`，然后各自加载自己的 model。
- 反例：Auth 输出 `AuthenticatedSubject` contract，Billing 显式翻译成自己需要的 customer identity。

## 建议

### 现在该做什么

从每个 bounded context 的问题与 invariant 重新定义 local model，只通过明确 boundary contract 传递真正共享的 facts。不要从现有 master object 的字段列表出发拆 DTO。

### 为什么重要

Universal model 会制造“semantic coupling disguised as reuse”。一个字段为了 Billing 加入后，Auth/Session/UI 全部开始知道它；很快 nullable、context flags、conditional validation 堆积起来，因为类型已经无法说明哪个解释现在有效。

### 修复策略

- 列出每个 context 要回答的问题；
- 为每个 context 建最小 model；
- 共享真正语义稳定的 value objects/IDs；
- crossing facts 通过 explicit contract 翻译；
- 删除在某 context 中永远“not applicable”的 foreign fields；
- persistence schema 不直接决定 domain model 边界。

### 决策分支

- 若一个类型回答不同 context 的问题，拆开它，只翻译真正共享的 facts；
- 若某字段在一个 context 里没有意义，它在那里必须无法表示，而不是在 master object 上 nullable；
- 若一个 tiny value object 处处含义真正相同，共享它不是 leak。

### 常见假修复

- 把 master model 复制到每个 package，但字段仍一模一样、同步演化。
- 加 `contextType` 决定哪些字段有效。
- 给不适用字段全部加 `Option/null`。
- 用 `authEmail/billingEmail/...` 把多个概念继续塞同一个对象。
- 为每个 context 再包一层 view，却底下仍暴露完整 master object。

### 验证

一个 context 的核心逻辑应能只依赖自己的 model 与显式 crossing contracts 编译/测试。另一个 context 新增字段，不应让本 context 莫名发生 source change。

### 完成条件

每个 model 有一个 semantic owner 和一个 reason to change；跨 context 传递的是明确 facts，不是一个无所不知的 master object。

### Nudge

共享字段并不等于共享含义。Model 应属于提出问题的 context，而不是属于数据库里恰好有多少列。
