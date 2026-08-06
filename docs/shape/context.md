# 上下文恢复 — 边界

## 谁有权决策

- transform 内**不做**恢复决策（看不到 attempt 结局）。  
- 恢复材料选择与提交只发生在 attempt 结局 reconile 之后。  
- Fallback 提供 armed/primed（FALLBACK-012）；本域提供 hasMaterial 与动作选择。

## CTX-007：按 RequestKind 分派结局

每个 attempt 三种结局来自 Outcome + isValidTerminal，不解析错误文本。  
动作按 `ProviderRequestKind`（PROMPT-008）区分：WorkMain / BloggerMain / BloggerSquash / InteractionRepair 各有固定后继（推进 cursor、写事实、发 continuation 等）——实现表见生产 `AttemptPlanner`，规范要求：**同种 RequestKind 同种结局必须同一分派**，禁止按错误字符串分叉。

## CTX-008：恢复槽失败计数

恢复槽内失败仍走 Fallback 连续失败计数；维护子请求成功不得单独清零 count（FALLBACK-011）。  
不得为「压缩失败」另造第二套预算。
