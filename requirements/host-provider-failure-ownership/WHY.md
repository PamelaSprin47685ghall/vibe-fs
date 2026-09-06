# host-provider-failure-ownership — WHY

OpenCode 自带 chat retry，而 Wanxiangshu 已拥有 durable provider recovery。两者同时重试会制造无法对账的重复上游请求、容量记账和错误提示。外部 server-plugin 的 event hook 仅能在 EventV2Bridge 广播后被动观察，根本无法拦截或改写 Host 原生 session.error 或上游 UI 呈现。必须把物理失败 owner 和错误 presentation 明确成一个 Host contract：Host 重试无条件固定为零；Wanxiangshu 独占 provider recovery 决策权；在活跃恢复期间 Wanxiangshu 自主发射零额外提示，并在所有恢复耗尽时仅产生一份 typed terminal presentation，避免双重终态噪声。
