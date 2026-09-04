# host-provider-failure-ownership — WHY

OpenCode 自带 chat retry，而 Wanxiangshu 已拥有 durable provider recovery。两者同时重试会制造无法对账的重复上游请求、容量记账和错误提示；仅在 plugin event observer 里“看见” session.error 又不能阻止 Desktop/CLI 的默认消费者。必须把物理失败 owner 和错误 presentation 明确成一个 Host contract。

