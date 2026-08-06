# Agent — 理由

Role 与 Tier 分离：权限矩阵若随「换模型」分叉，fast/deep 会演化成两套产品，Peer Fallback 也失去对称前提。

双层权限（Host schema + ToolRegistry）是因为 Host 配置可漂：只信一层会在配置异常时漏工具或越权执行。

`external_directory` 固定 allow 是对 Host 默认 ask 的显式覆盖，不是把路径逃逸塞进角色工具矩阵——后者会污染 AGENT-006 的语义边界，并诱导「工具白名单 = 一切权限」的错误心智。

内部 Agent 从 enum 消失，是防止模型调用本应仅由运行时合成的 Blogger/Executor 路径。

## 备选与被拒

**权限维度：Role 与 Tier 分离 vs 随模型分叉。** 拒分叉：fast/deep 随「换模型」演化成两套产品，Peer Fallback 丢对称前提（AGENT-001 不变角色，CanonicalRole 与 fast/deep 无关）。

**权限载体：双层（Host schema + ToolRegistry）vs 单层可信。** 拒单层：Host 配置可漂；只信一层会在配置异常时漏工具或越权执行（AGENT-006 语义边界）。

**external_directory：固定 allow 元权限 vs 塞进角色工具矩阵。** 拒塞矩阵：污染 AGENT-006 边并诱导「工具白名单=一切权限」的错心智（AGENT-019）。显式覆盖 Host 默认 ask 即可。

**内部路径暴露：Agent enum 留内部 vs 隐去。** 拒保留：模型须无法合成 Blogger/Executor 等运行时专用路径（AGENT-008/ENFORCER-010），只余外部可寻 agent。
