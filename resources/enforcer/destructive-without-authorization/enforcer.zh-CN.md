# destructive-without-authorization — Enforcer

Destructive-without-authorization 的核心不是“delete 很危险”，而是不可逆/高影响 effect 只因为调用方**能到达这个 API**就被视为有资格执行，没有一份明确 authority proof 站在 mutation 前面。

删除 file/branch/data、force push、drop schema、terminate session/process、revoke credential、overwrite durable state，这些 operation 的问题不是技术上能不能做，而是**谁有权让这件事成为世界事实**。

以下情形触发：

- user/request-controlled input 可以直接到 destructive primitive；
- “有 path/id 就能删”，没有 capability/owner/role check；
- authorization 只在 UI/调用约定里，backend effect boundary 不校验；
- internal tool 被暴露给不应拥有 destruction authority 的 role；
- broad admin credential 被普通 code path 顺手复用；
- recovery/cleanup 看到“可疑 residue”就删除，但没有 positive ownership/evidence。

不要误杀 owner 自己的 deterministic cleanup。一个 scope 删除自己创建、stable identity 已确认、lifetime 已结束的 temp resource，authority 可以来自 ownership，而不需要再问人一次。CI sandbox 清自己目录也同理。

也不要把 authorization 简化成“加 confirmation dialog”。Human confirmation 是 UX，不一定是 authority。真正 proof 可能是 authenticated capability、role permission、resource ownership、lease、operator-approved migration、scoped cleanup token。

与 `secret-in-code` 区分：credential 暴露是 secret management 问题；本规则即使 credential 存储完美，只要授予范围过宽，仍然是 authority defect。与 `resource-not-scoped` 区分：cleanup lifetime 错会导致资源没删或乱删；这里最锋利的是 caller 没有权删。

判定问题：**在 effect 执行前，哪一个事实证明这个 principal 有资格改变/销毁这个 resource？** 如果答案只是“它调用了这个函数”，authorization 根本不存在。

> Capability 不是 API 可见性。能伸手摸到按钮，不等于世界授权你按下去。