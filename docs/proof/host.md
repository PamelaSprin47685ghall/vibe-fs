# Host — 证明

行为：`what/host.md`。边界：`shape/host.md`。程序：`how/host.md`。

## 事件

| 证明 | 条款 |
|------|------|
| 业务层无碎片事件处理 | HOST-001、HOST-002、ARCH-002 |
| Domain 仅 typed HostSignal | HOST-003 |
| chat.message 不进普通业务 | HOST-002 |
| provider `TurnAborted` 保留到消费边界；无 Armed 不产生 Agent completion | HOST-004、LOOP-006、EXEC-020 |

## Compaction

| 证明 | 期望 | 条款 |
|------|------|------|
| 预防四项关闭 + 首轮探测 | 失败 → HostContractUnsupported | HOST-006 |
| 任意 pseudo-run | ContextReanchored；PrefixCoverage 归零；RecordCoverage 保留 | HOST-006、PERSIST-010 |

## 绑定与身份

| 证明 | 条款 |
|------|------|
| Transform 绑定 0/≥2 → 不写 seal | HOST-010 |
| journal 代理等式 canary | HOST-010 |
| Tool 身份仅 ToolContext 双半边 | HOST-011 |
| 跨实例共享表 vs 每实例 Journal；共享表操作不跨 await | HOST-012 |

## Session 关联

Work↔Companion 深度 1；关联非 Role（HOST-008、COMPANION-001/002）。

代表：`tests/unit/plugin/host-hooks.test.mjs`、host-compaction unit、e2e compaction/reanchor 路径。

## Satellite 与 Student/Teacher

| 证明 | 期望 | 条款 |
|------|------|------|
| kind 投影 | Work/Companion/Teacher 双向 O(1)；Satellite 无子 Satellite | HOST-008 |
| Host children 恢复 | 1 个复用、0 个 Replacement、多于 1 个/查询失败 fail closed | HOST-008、HOST-014 |
| Teacher 三轮调用 | 同一 Teacher SessionId；普通正文不完成父工具 | HOST-014、AGENT-020 |
| Teacher return | 文本只成为父 `teacher` 结果；固定 terminal 正常完成，无 abort/interrupted，Session 可继续 | HOST-014 |
| Student final return | QA 删除先于最终 Assistant completion；message 成为最终回复 | HOST-014、EXEC-027 |
| 非 Student 回归 | provider schema、hooks、Companion 行为字节/语义不变 | HOST-014 |
