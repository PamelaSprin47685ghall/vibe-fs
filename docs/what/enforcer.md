# Blogger / Enforcer — 可观察行为

条款前缀：`ENFORCER-`。  
Cycle 写入口与恢复证据边界见 `shape/enforcer.md`。
归并、nudge、continuation、compaction 接线见 `how/enforcer.md`。  
规则实例：`resources/enforcer/catalog.json`。

## ENFORCER-001：目标

Blogger 以 `blog` 工具提交稠密工作日志；tip 绑定 catalog 字段；cycle 原子提交 coverage。

## ENFORCER-002：非目标

不把 Blogger 做成通用评分引擎；不恢复 score-vector 控制流；不在 transform 里预测压缩。

## ENFORCER-003：Blogger Cycle

一个 Blogger Cycle = 一次 provider run 上对 `blog` 的有效归并提交。

## ENFORCER-004：Blogger Cycle 结果

结果携带 canonical text、tip→RuleId、可选 evidence；无效 cycle 不进 frames。

## ENFORCER-010：Blogger 工具权限

Blogger 工具权限仅 `blog`。

## ENFORCER-011：工具名称

工具名稳定为 `blog`。

## ENFORCER-020：逻辑 schema

必填：`text`、`tip`（catalog field 枚举）。可选：`evidence`。

## ENFORCER-021：tip 枚举身份

`tip` 的合法值 = catalog 的 `field` 枚举；映射到 RuleId。

## ENFORCER-022：Required / Optional 语义

必填缺失失败；可选缺省不发明值。

## ENFORCER-023：缺 tip / 未知 tip 失败

缺 tip 或 tip 不在 catalog → 该调用失败，不得默认 tip。

## ENFORCER-024：字段识别

只认合同字段名；未知字段不得静默充当 tip/text。

## ENFORCER-025：多调用时 tip 选择

多 `blog` 归并时 tip 选择规则确定（实现见 how 归并）；不得随机取。

## ENFORCER-026：Transport 与 Semantic Schema 分离

不得用 wire 形态当领域身份；Semantic schema 才进 cycle。

## ENFORCER-030：统一 System Prompt

fast/deep blogger 共用 authoritative system（见资源文件）；工具合同在 system 中固定「恰好调用一次 blog」。

## ENFORCER-040：工具立即返回

`blog.execute` 立即返回，不在工具内等待后续模型轮次。

## ENFORCER-060：缺少工具调用 — 总则

无有效 blog → 进入 InteractionRepair / nudge 路径或 Fallback（见 how）。

## ENFORCER-061：无有效 text

无有效 text → 不提交。

## ENFORCER-062：Fallback 切换

失败与 Fallback 切换规则不另造预算；走统一 FallbackController。abort 清理残留不算失败（FALLBACK-013）。

## ENFORCER-063：成功关闭恢复窗口

成功提交把当前逻辑请求的 `BloggerToolRecovery` 恢复为 `NoRecovery`；不得保留会污染下一请求的 Nudge/AABB 证据，也不得另造 repair 计数器。

## ENFORCER-072：ScoreVector 删除与版本化 clean break

ScoreVector 删除与版本化 clean break。

## ENFORCER-073：旧评分条款废止

旧评分条款废止，不得作为运行时分支。

## ENFORCER-071：work record 呈现 previous_enforcer_tip

work record 以低信任 `previous_enforcer_tip` 块呈现；不得伪装 parent instruction。

## ENFORCER-170：规则 catalog

规则 catalog：schemaVersion + rules；`id`/`field` 发布后稳定；校验非空、唯一、ordinal 连续 1..N。
