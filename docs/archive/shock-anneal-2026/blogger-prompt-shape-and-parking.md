# 动议：Blogger 请求形状与挂起（Enforcer 生产接线细化）

状态：ADOPTED（2026-08-03）。提出人：用户（2026-08）。性质：SSOT 细化——用户明确授权直接修改 SSOT。

## 吸收位置

本动议八个冻结答案已写入规范，不得再据本文件改代码；以 SSOT 条款为准。

| 冻结答案 | 吸收条款 |
|---------|---------|
| cycle 提交必须推进 coverage；扩展 `BlogEntryCommitted`；删除独立 `EnforcementCycleCommitted` | ENFORCER-045、ENFORCER-150 |
| 删除正常 BloggerMain 路径上的 terminal 等待；完成边界 = `BlogEntryCommitted` | ENFORCER-047 |
| squash 与 normal 共用 provider-view builder；只投影前 k frames | CTX-012、COMPANION-005 |
| Prompt 要求 exactly once；Host 防御性多调用归并 | ENFORCER-030、ENFORCER-042 |
| system 合并 = 仓库权威 `blogger-system.md` 覆盖，不拼用户自定义 prompt | ENFORCER-030、COMPANION-004 |
| frames / delta 标题在消息层包装，不进 Blob / TOML | COMPANION-005、CTX-013 |
| canary matcher 改 instruction 稳定前缀，并验完整 message shape | VERIFY 层 canary（实现约束，非新条款） |
| 删除 `CompanionPrompt.System`；system 只由 managed-agent config 注入 | ENFORCER-030、COMPANION-004 |
| 单一 `BloggerRuntimeState` + typed `BloggerRequestContext` | ENFORCER-047、ENFORCER-050、ENFORCER-051 |
| resume 重建 projection，禁止 append raw transcript | ENFORCER-051、COMPANION-005 |

## 原诉求摘要（已吸收，仅作历史）

1. provider-visible 形状：system + `# Working Record` frames + `# New Work To Record` delta + final exactly-once instruction。
2. blog 调用后 continuation transform 挂起，直到新 material offer 恢复。
3. Opening 不送 Y 压缩（OpeningPromptRaw 规则不变）。

## 原未决问题

八项已全部冻结并写入上表条款；本文件不再保留开放问题。
