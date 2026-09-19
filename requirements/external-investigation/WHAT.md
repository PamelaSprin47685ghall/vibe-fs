# external-investigation — WHAT

本文件是 `external-investigation` 的**废止声明与演进记录**。本包所有业务功能与角色规范均已撤销，不再作为活跃生产系统的执行规范。

---

## EXTERNAL-INVESTIGATION-012: Browser 角色与专属集成全链撤销且无替代代理

Browser 角色、专属 MCP 适配器（`StealthBrowserMcp`）以及相关启动配置、环境变量（`STEALTH_BROWSER_MCP_*`）与工具别名（`js-browser`）已彻底从系统中清除。系统中不存在任何公开或私有的 Browser 代理实体，亦不建立任何替代代理或空壳代理。

## EXTERNAL-INVESTIGATION-013: 外部调查职责不转移且禁止通过通用执行工具复活

外部网络事实调查与网页浏览职责彻底撤销，不得转移给 Engineer、DevOps 或 Manager 等任何其他角色承担。DevOps 执行已有构建流程时的正常依赖下载不构成外部调查；但严禁任何角色通过 `curl`、`wget`、通用 shell 脚本或临时 JavaScript 代码包装复活外部调查能力。
