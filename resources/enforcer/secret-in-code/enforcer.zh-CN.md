# secret-in-code — Enforcer 中文版

## 定义

Secret 进入 source/fixture/log/prompt/committed config，不只是“放错文件”，而是把 confidential authority 放进一个专门用于复制、保留、review、缓存和恢复的介质。

因此 secret exposure 是 temporal security event：删掉最新一行不能让所有 clone/history/cache 忘记。真正修复必须让旧 authority 失效。

## 支配原则

Source control 为复制、保留、review 与恢复而优化——这些恰是 secret 需要的相反属性。凭据一旦进入 repository history，clone、cache、diff 与 log 可能已经保存它。安全边界因此是时间性的：暴露要求 revoke，而不是表面删除。

## 何时触发

- live API token/password/private key/session credential 被 commit；
- debug fixture/log/prompt 包含真实可用 secret；
- ciphertext 与解密 key 一起在 repo；
- `.gitignore` 在 secret 已 commit 后才加入；
- “只是 test token”实际上能访问真实服务/数据。

## 不应触发

- 明确 fake placeholder 无任何 authority；
- OAuth client id、JWKS URL 等协议定义为 public 的 identifiers；
- test 本地产生的 ephemeral key 不授予真实环境权限；
- source 只引用 secret-store/env key name，不含 value。

## 与相邻规则区分

`debug-print-left` 可能通过临时 diagnostics 意外暴露值；`leftover-scaffolding` 可能保留恰好含凭据的 fixtures。本规则管的是 confidential authority 被存放在可复制的代码或文档介质中。Tie-break：如果 live secret 在 replicated content 里，即使 debug print 把它放进去，这个案子也归本规则。

## 判定程序

1. 判断持有该值是否授予 authority 或揭示受保护材料。
2. 若是，把 repository exposure 当作 compromise 处理。
3. 从当前内容移除该值。
4. Rotate 或 revoke 已暴露的 authority，并用批准的 secret-reference boundary 替换来源。

## 例子

- positive：committed `.env` 或 test fixture 含有能对真实服务认证的 live API token。
- near-miss：config 列出 `API_TOKEN_ENV=STRIPE_SECRET`，CI 从 secret store 注入值。
- counterexample：docs 显示 `sk_test_xxx`，是明显无法认证的 fake placeholder。

## Nudge

Repository 是分发系统，不是 vault。Secret 一旦进去，正确动作不是“藏好一点”，而是 revoke/rotate，让传播出去的旧值变成废物。