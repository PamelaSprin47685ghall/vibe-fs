# 恢复施工与流程验收收尾

按用户“流程能跑通、小问题先记录”的范围收尾。生产源码保持`764e6778e99d485ed2dafa97072e1f6711b74161`，独立库保持`45849f51778f6a12dcafbc12ce1c8c7f7f65502b`（0.11.0，22入口）。[施工计划与问题优先级](../../../proposals/extraction-kanban/MODULE-CONSTRUCTION-2026-10-21.md#resume-20261021)保留未完成范围；本目录的隔离原型不接入生产。

| 范围 | 实际结果 | 证据 |
|---|---|---|
| 完整本地发行 | release08/gen107通过；unit4537/0、integration725/0，skip和旧TODO单列 | [收据](release08-receipt.json)、[原始日志](release08-full.log) |
| 归档输入验收 | gen108的783项主仓编译输出原字节不变；正式定向336/0、10/10排空回收，包验证通过 | [收据](final-acceptance.json) |
| 精确发布与完整Linux CI | a6e3917ca已正常快进自有master及PR分支；unit4440/0、integration709/0，格式/静态/Fable/E2E/包通过 | [收据](publication-and-ci-a6.json)、[远端运行](https://github.com/dyx13/vibe-fs/actions/runs/39539917238) |
| Process/Supervision长测 | Node22、Bun各2h+2h真实通过，keeper各4h；13个记录PID及其FD、原生scope/pipe目录均不存在 | [最终收据](process-four-hour/final-receipt.json)、[原harness](process-four-hour/consumer/lane.mjs)、[回收](process-four-hour/cleanup-receipt.json) |
| PTY长测 | 2h交互及EOF成立，但暂停custodian后的超时recover连接占位，整体exit1，记P2不修 | [原始失败](pty-2h/final-receipt.json)、[唯一短例](pty-detach-diagnostic/final-receipt.json)、[只读定位](vibe-fs-pty-close-readonly-review-20261021.md) |
| 首次PTY启动 | cwd未准备，在创建owner前失败，不计长测时间 | [保留记录](pty-setup-failed/) |
| 隔离guest A/B/C | 有限机制正反例与独立验证已保存；生产权限、ledger、完整sanity和资源硬限额尚未接线 | [源码与证据入口](mf01/README.md) |

Process/Supervision的实际流hash比较在原harness写出`phase-completed`前已断言通过；最终收据中的摘要数值是事后按保留frames重算的期望值，来源分列，没有另一次实际输出采集。四小时只计新实验，旧取消时长不累计。PTY整体失败与后续短例均保留原exit1，不用正常交互时长替代恢复验收。

[实验原始清单](endurance-archive-list.json)列125项固定文件；[归档审计](endurance-archive-audit.json)记录它们在本目录的实际位置、大小与SHA256。先前已归档的PTY文件复用原位置，未覆盖不同证据。包、node_modules及生成dist不进Git。[最终归档输入验收收据](closing-acceptance.json)另记本次正常构建及必要检查；最终提交的远端CI以现有[Draft PR #62](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/62)及对应运行的精确head为准。

原运行目录仍在`8db33b3429a70d5cdbea30e393363666b3649573`，三个stash保留；新出现的用户未跟踪文件`万象术独特功能导论.md`原样保留。未切换运行版本，未部署。
