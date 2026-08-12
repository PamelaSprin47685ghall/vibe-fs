# context-model-leak — Enforcer 中文版

## 定义
Context model leak 不是“共享类型不好”。真正的问题是：一个 master model 被多个 bounded context 复用，而这些 context 对同一字段赋予不同意义、invariant、lifetime 或 authority。

Shape 一样不代表 concept 一样。Auth 的 User、Billing 的 Account Holder、Session 的 Participant 都可能有 `id/name`，但它们回答的问题不同。共用 mega-model 会让某个 context 为自己新增的字段，自动变成所有 context 都“看得见且似乎有意义”的事实。

## 何时触发
- 一个 `User` 同时携带 credential、balance、session state、UI preference；
- 很多字段在某些 context 中只能 nullable，因为“这里用不到”；
- 一个 context 修改字段，另一个 context 被迫重编译/迁移，却没有业务原因；
- authorization/lifecycle rule 开始根据“当前是哪种使用场景”解释同一对象；
- context boundary 只传整个 master object，而不是所需 facts。

## 不要误判
- `Money`、opaque ID 等 tiny value object 在各处语义完全相同，可以共享；
- context 之间传同一 identity，不代表传同一 model；
- persistence DTO 在一个 context 内映射成本地 model，不是 leak；
- 不要为了“bounded context”把真正同一概念复制出不同空壳类型。

## 刀口
问每个字段：**这个 context 为什么有权知道它？它在这里有什么 invariant？**

若答案是“因为 master model 里本来就有”，边界已经失去语义选择。

## 与近邻区分
`boundary-collapse` 更广，任何 internals 越界都算；这里专门指**同一 representation 被迫扮演多个 context 的概念**。

`primitive-obsession` 是概念身份太弱；这里常常恰好相反：一个“很强的大类型”强到吞掉多个概念。

## 例子
- 正例：全系统共享一个 `User`，含 password hash、credit limit、theme、activeSessionId。
- 近邻：各 context 共享 `UserId`，然后各自加载自己的 model。
- 反例：Auth 输出 `AuthenticatedSubject` contract，Billing 显式翻译成自己需要的 customer identity。

## 提醒
共享字段并不等于共享含义。Model 应属于提出问题的 context，而不是属于数据库里恰好有多少列。
