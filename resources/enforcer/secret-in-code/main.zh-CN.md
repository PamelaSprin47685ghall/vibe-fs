# secret-in-code — Main 中文版

## 现在该做什么
若值授予真实 authority，先按 compromise 处理：rotate/revoke；再从 current source 与相关 artifact 移除，并改用项目批准的 secret injection boundary。必要时清理 history，但不能把 history rewrite 当成 rotation 的替代。

## 为什么这很重要
Source tree 天生会复制到 clone、CI、review、cache、backup。Secret 进入后，你无法证明所有副本都被删净；唯一可靠的补救是让泄露值失去能力。

## 常见假修复
- 只删除最新 commit 中的字符串。
- base64/简单加密，key 也在仓库。
- 搬到另一个 committed file。
- 事后 `.gitignore` 就认为历史安全。
- 加 scanner 但不 rotate 已暴露 credential。

## 验证
旧 credential 必须无法再认证；current runtime 通过 intended secret boundary 获得 replacement；repository 不再含 live sensitive material。

## 完成条件
拿到 source tree 不等于拿到 secret authority；任何曾暴露的真实 credential 已被失效，而非仅被隐藏。
