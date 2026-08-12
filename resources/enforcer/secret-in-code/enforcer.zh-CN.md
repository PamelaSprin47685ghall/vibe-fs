# secret-in-code — Enforcer 中文版

## 定义
Secret 进入 source/fixture/log/prompt/committed config，不只是“放错文件”，而是把 confidential authority 放进一个专门用于复制、保留、review、缓存和恢复的介质。

因此 secret exposure 是 temporal security event：删掉最新一行不能让所有 clone/history/cache 忘记。真正修复必须让旧 authority 失效。

## 何时触发
- live API token/password/private key/session credential 被 commit；
- debug fixture/log/prompt 包含真实可用 secret；
- ciphertext 与解密 key 一起在 repo；
- `.gitignore` 在 secret 已 commit 后才加入；
- “只是 test token”实际上能访问真实服务/数据。

## 不要误判
- 明确 fake placeholder 无任何 authority；
- OAuth client id、JWKS URL 等协议定义为 public 的 identifiers；
- test 本地产生的 ephemeral key 不授予真实环境权限；
- source 只引用 secret-store/env key name，不含 value。

## 刀口
 possession of this value 是否授予真实 authority 或泄露保护材料？若是，repository exposure 就按 compromise 处理，不按“代码清理”处理。

## 提醒
Repository 是分发系统，不是 vault。Secret 一旦进去，正确动作不是“藏好一点”，而是 revoke/rotate，让传播出去的旧值变成废物。
