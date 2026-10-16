# secret-in-code — Lesson

## 评价

### 定义

Secret 进入 source/fixture/log/prompt/committed config，不只是“放错文件”，而是把 confidential authority 放进一个专门用于复制、保留、review、缓存和恢复的介质。

因此 secret exposure 是 temporal security event：删掉最新一行不能让所有 clone/history/cache 忘记。真正修复必须让旧 authority 失效。

### 支配原则

Source control 为复制、保留、review 与恢复而优化——这些恰是 secret 需要的相反属性。凭据一旦进入 repository history，clone、cache、diff 与 log 可能已经保存它。安全边界因此是时间性的：暴露要求 revoke，而不是表面删除。

### 何时触发

- live API token/password/private key/session credential 被 commit；
- debug fixture/log/prompt 包含真实可用 secret；
- ciphertext 与解密 key 一起在 repo；
- `.gitignore` 在 secret 已 commit 后才加入；
- “只是 test token”实际上能访问真实服务/数据。

### 不应触发

- 明确 fake placeholder 无任何 authority；
- OAuth client id、JWKS URL 等协议定义为 public 的 identifiers；
- test 本地产生的 ephemeral key 不授予真实环境权限；
- source 只引用 secret-store/env key name，不含 value。

### 与相邻规则区分

`debug-print-left` 可能通过临时 diagnostics 意外暴露值；`leftover-scaffolding` 可能保留恰好含凭据的 fixtures。本规则管的是 confidential authority 被存放在可复制的代码或文档介质中。Tie-break：如果 live secret 在 replicated content 里，即使 debug print 把它放进去，这个案子也归本规则。

### 判定程序

1. 判断持有该值是否授予 authority 或揭示受保护材料。
2. 若是，把 repository exposure 当作 compromise 处理。
3. 从当前内容移除该值。
4. Rotate 或 revoke 已暴露的 authority，并用批准的 secret-reference boundary 替换来源。

### 例子

- positive：committed `.env` 或 test fixture 含有能对真实服务认证的 live API token。
- near-miss：config 列出 `API_TOKEN_ENV=STRIPE_SECRET`，CI 从 secret store 注入值。
- counterexample：docs 显示 `sk_test_xxx`，是明显无法认证的 fake placeholder。

## 建议

### 现在该做什么

若值授予真实 authority，先按 compromise 处理：rotate/revoke；再从 current source 与相关 artifact 移除，并改用项目批准的 secret injection boundary。必要时清理 history，但不能把 history rewrite 当成 rotation 的替代。

### 为什么重要

Source tree 天生会复制到 clone、CI、review、cache、backup。Secret 进入后，你无法证明所有副本都被删净；唯一可靠的补救是让泄露值失去能力。

### 修复策略

在 exposure window 仍重要时先 rotate；从 current source 移除值；使用 environment 或 secret-store injection；并加一道防止复发的合适 gate，不在 tests 里存真实凭据。

### 决策分支

- 若值授予真实 authority，立即 rotate 或 revoke，然后从 current source 与 history-facing artifacts 移除。
- 若 tests 需要凭据，用 fakes 或注入的 secret boundary——绝不 commit live material。
- 若字符串是文档化的 public identifier 或明显 placeholder，不把它当本 defect。

### 常见假修复

- 只删除最新 commit 中的字符串。
- base64/简单加密，key 也在仓库。
- 搬到另一个 committed file。
- 事后 `.gitignore` 就认为历史安全。
- 加 scanner 但不 rotate 已暴露 credential。

### 验证

旧 credential 必须无法再认证；current runtime 通过 intended secret boundary 获得 replacement；repository 不再含 live sensitive material。

### 完成条件

拿到 source tree 不等于拿到 secret authority；任何曾暴露的真实 credential 已被失效，而非仅被隐藏。

### Nudge

Repository 是分发系统，不是 vault。Secret 一旦进去，正确动作不是“藏好一点”，而是 revoke/rotate，让传播出去的旧值变成废物。
