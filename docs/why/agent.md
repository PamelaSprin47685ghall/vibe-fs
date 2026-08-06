# Agent — 理由

Role 与 Tier 分离：权限矩阵若随「换模型」分叉，fast/deep 会演化成两套产品，Peer Fallback 也失去对称前提。

双层权限（Host schema + ToolRegistry）是因为 Host 配置可漂：只信一层会在配置异常时漏工具或越权执行。

`external_directory` 固定 allow 是对 Host 默认 ask 的显式覆盖，不是把路径逃逸塞进角色工具矩阵——后者会污染 AGENT-006 的语义边界，并诱导「工具白名单 = 一切权限」的错误心智。

内部 Agent 从 enum 消失，是防止模型调用本应仅由运行时合成的 Blogger/Executor 路径。
