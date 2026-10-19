# 拆分方法补充：原提示词位置与融入说明

更新：2026-10-19。

执行入口统一为原有 [README 施工流程](README.md)与 [TEMPLATES 可复制施工单](TEMPLATES.md)。本文件保留查找结果、融入位置和历史依据，供同事审查；不再另列一套独立执行 prompt。

## 找到的原入口

本轮只读核对上游 `eee7936a212a6da69feb68808e4ea81a716ebdb4` 的 `proposals/extraction-kanban`。该版本没有单独的 prompt 文件；目录入口说明由 README 管操作流程，TEMPLATES 提供可复制的认领、契约、证据、PR 和交接文本，CARDS 给具体职责，BOARD 管状态。仓内入口能确认，不能据此声称已经取得某个 agent 的实际会话提示词。

- [上游 README](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/eee7936a212a6da69feb68808e4ea81a716ebdb4/proposals/extraction-kanban/README.md)：第 3 节设计准绳、第 6 节 A—H、第 7 节契约协作、第 9 节验收。
- [上游 TEMPLATES](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/eee7936a212a6da69feb68808e4ea81a716ebdb4/proposals/extraction-kanban/TEMPLATES.md)：第 2 节最小完整契约是主要任务文本，第 1、4—7 节覆盖认领、验证、PR、完成与交接。

两份上游文件的 blob 分别为 `10b0208364ab57f961f5e2580da861649bc78538`、`1c51cd5cb55eeab185dfef68081821b24035159f`，与此前读取的 `705e1ea3b` 相同。本地文件已有报告入口等改动，本次只在现有结构内补充，未导入或合并上游。

## 已有要求与实际补充

| 内容 | 原文已经覆盖 | 融入方式 |
|---|---|---|
| 从真实消费者确定职责 | README A/B 与 TEMPLATES 的目标、依据、A/B/C 和公开消息 | 在[原契约模板](TEMPLATES.md#contract)补“消费者结果”及“库 / 主仓或组合层 / 承接包”，让两侧保证可直接对照 |
| 反例、效果完成与旧职责退出 | README 第 3 节及 C/F/G、TEMPLATES 的公开消息、删除清单和验证 | 继续使用原文，不另造通用检查；按本模块契约选择反例 |
| 局部问题可以后移 | 原流程允许库就绪先交付、子卡接续，但未展开带局部缺陷候选的交接分工 | 在 A—H 后加入[阶段交接](README.md#staged-handoff)，区分边界前置与局部修复，允许拆分者交接后继续下一项 |
| 阶段交付与完整迁移分别判断 | 原文已有 LIB_READY、父子卡及未证不算通过 | 在认领、契约、PR 和[完成检查](TEMPLATES.md#completion-scope)中明确本轮结论与范围，保持原看板列含义 |
| 并行修复 | README 第 4/7 节已有公共编辑面、稳定协议并行，第 8 节已有串行集成 | 在[原协作段](README.md#parallel-repairs)说明同一机制用于后续修复，并在[原交接单](TEMPLATES.md#handoff)补后续包、前置和集成责任 |

原来的自治、显式组合、可拆增强、独立产物及完整迁移验收继续有效。已知库契约缺陷不会因交接自动成为 LIB_READY，完整迁移的未完成承诺继续留在父卡。专项分工无需加入根 AGENTS.md，也不新增看板列、重复表格或审批轮次。

这些方法面向后续每个模块；历史案例只说明补充理由，不要求重开已完成模块，不照搬旧模块的物理故障测试。并行修复尚未在本轮试行，不宣称提速已经验证。

## 方法的历史依据

| 已发生的情况 | 提炼出的后续方法 |
|---|---|
| Process 留尾库契约成立，主仓却丢失完整输出 | 消费者承诺与基础库契约分别确认，其间差额明确归属 |
| Trace 主要承接描述符，原件仍在旧链；Opening/cut 出现回退 | 按职责实际承接判断迁移，用相关反例验证消费者保证 |
| Ownership 无退出见证就清账、同步完成丢失，受控端口探针复现 | 相关模块明确效果完成和结算的观察，不把请求当结果 |
| 真实 PTY 终止仍是 TODO，历史记录却称通过 | 完成结论严格对应证据的范围和结果 |
| Journal 有局部资源寿命缺陷，职责划分仍成立 | 保持已经成立的边界，将局部缺陷交给后续修复 |

详细事实与归因见[首次源码复核](SOURCE-AUDIT-2026-10-15.md)、[双版回归复审](REGRESSION-REVIEW-2026-10-16.md)。以上案例不证明所有模块都有同类缺陷；后续修复回退不归责于原拆分。

原要求及证据分工见[原要求对照](LOCAL-FIRST-FOUR-MODULES-REPORT-2026-10-18.md#original-requirements-alignment)和[交付清单](UPSTREAM-DELIVERY-CHECKLIST.md#requirements-and-checklist)。接手时使用实际适用的规则版本；历史来源仅用于说明方法依据，不覆盖当前已批准的契约。
