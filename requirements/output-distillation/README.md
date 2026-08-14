# output-distillation

## 一句话 WHY

真实执行输出可能大到不能原样进入 participant horizon；压缩必须保留会改变后续判断的事实，
同时承认 fragment 的视野边界——fragment 不能冒充整体成功、不能发明因果。

## WHAT 概览

- 大输出 → bounded observation 的语义压缩；不静默截断成成功空结果（DISTILL-001/002）。
- fragment 谦逊：沉默的 fragment ≠ 整体成功；边界保持可见（DISTILL-003）。
- 合并不发明因果/成功率；conflict 保留、不被沉默否决（DISTILL-004/005）。
- 失败路径：partial account + 最后 chunk 原始 tail（DISTILL-006）；成功路径 chunked map + online reduce（DISTILL-007）。
- 每 chunk 定向 await 一次；FamilyWaiting 等 readiness；NotFound 是 hard fail（DISTILL-008）。
- Distiller 是私有 runtime，不进公开 fork/horizon（DISTILL-009）；不执行、不裁决（DISTILL-010）。
- Large Gate 输出预算合同；禁无界缓冲（DISTILL-011）；自定义 tool 文本确定性留尾截断（DISTILL-012）。
- 不做 chunk 统计仪表盘、不报告 success ratio（DISTILL-013）。

## HOW 概览

实现模型：`Infrastructure/OpenCode/Tools/{Distillation,DistillationRuntime}.fs`（map/reduce、失败降级）、
`Process/{LargeGate,Spool}.fs`（预算合同与输入）、`Domain/ToolResultBound.fs`（ARCH-012 截断）、
`resources/provider/role/distiller/`（Role Law）。详见 HOW.md。

## PROOF 概览

- 包内（MOVE）：`tests/executor-summarize.test.mjs`（9 断言）；`tests/distiller-fragment-humility.test.mjs`（NEW，Oracle 2，1 断言）。
- REUSE（SPLIT@cutover）：`tests/unit/process/large-gate.test.mjs`、`tests/unit/process/process-runner.test.mjs`
  （gate 断言）、`tests/unit/session/distiller-ownership.test.mjs`、`tests/unit/plugin/tool-host-codec-full.test.mjs`
  （ToolResultBound 面）。
- Semantic anchors（`scripts/checks/semantic-anchors.mjs`）拥有 distiller 组全部 5 个：`distinguishing` /
  `fragment-humility` / `merge-conflicts` / `locatable-to-unseen-reader` / `no-invented-causality`。

## 阅读顺序

1. `WHY.md` — 为什么必须独立存在、RED 是什么、与 process-execution 的边界。
2. `WHAT.md` — normative 合同。
3. `HOW.md` — 实现模型（非 normative；含「历史与弃权」）。
4. `PROOF.md` — 命题落点表 + SPLIT@cutover。

## 边界（DOES NOT OWN）

process 控制/completion（`process-execution`）；Reviewer judgement（`review-judgement`）；
generic context compression / 历史记忆（`context-compression`）；Distiller 当前 Persona 名 /
hidden-session mechanics（`managed-session-lifecycle`）。
