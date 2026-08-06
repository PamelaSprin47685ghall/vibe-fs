# Companion — 理由

每个 Work Session 配叶子 Y，是为了把「可压缩的工作日志」从主会话原始历史中分离，而不把 Companion 做成角色特权。

LWR 自包含跨 Session hand-off；父 LWR 不作 child Seed，防止多代 fork 指数嵌套。

RecordCoverage 与 PrefixCoverage 分型，避免「Y 还没覆盖完就声称可替换 X 前缀」。同 epoch 前缀字节稳定，是 KV-cache 与 ReviewSeal 的共同前提；epoch 切换必须由已提交事实驱动，不能由 token 估算驱动。
