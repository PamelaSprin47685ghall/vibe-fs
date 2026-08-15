# degeneration-guard

> 尚未结束的 attempt 若已进入病态重复，在污染更多历史前主动止损，再交给正常 recovery。

## 一句话 WHY

LLM 流式输出偶发退化（单 token/短句循环）；继续跑只污染 transcript 并推迟有效恢复槽。本包在
attempt 内放一个 bounded、非权威的纯传感器，命中时只停止当前物理 attempt，然后桥接标准
`provider-attempt-recovery`。（详见 `WHY.md`）

## WHAT 概览

唯一 normative 合同在 `WHAT.md`（12 条命题，`DG-001`..`DG-012`）：
- 问题与非目标（DG-001）；传感器是 ARCH-002 定点例外（DG-002）
- 判定指标：o200k token + 指数衰减 weighted-distinct token count（DG-003）
- 仓库全文滴定固定参数、O(1) 更新有界内存、生命周期绑定单次 ProviderRun（DG-004/005/006）
- 命中只停止当前物理 attempt、LoopKillArmed 进程内局部（DG-007/008）
- 强杀桥接标准 recovery，不造第二状态机（DG-009）
- 作用域与豁免、continuation 独立叶子、detector 不是业务 truth（DG-010/011/012）

## HOW 概览

实现模型见 `HOW.md`：`Session/LoopDetector.fs`（纯检测器）、
`Infrastructure/OpenCode/Host/LoopSensor.fs`（边沿观测器 + LoopKillArmed）、
`Application/Recovery/ProviderRecoveryWorkflow.fs`（`continueAfterLoopKill` 桥接）。

## Proof 概览

`PROOF.md` 给出每条命题的测试落点：
- MOVE（2 文件，20 断言）：`tests/loop-detector.test.mjs`（9）、`tests/loop-sensor.test.mjs`（11）
- NEW：`tests/loop-calibration.test.mjs`（仓库全部可读文字滴定）+ `tests/loop-detector-memory.test.mjs`（有界内存 / 无迟滞 / 无连续命中 / attempt 独立）
- REUSE：`tests/unit/verify/p0-recovery-join-gate.test.mjs`（桥接静态形状）

## 阅读顺序

1. `WHY.md`（为什么存在、何时 RED、与 provider-attempt-recovery / crash-reconciliation 的边界）
2. `WHAT.md`（12 条 normative 命题）
3. `HOW.md`（算法与桥接怎么满足）
4. `PROOF.md`（怎么验证、红了说明什么）

## 依赖

DEPENDS ON：`provider-attempt-recovery`（桥接目标：命中后由标准 recovery 决定 cursor/budget）、
`host-boundary`（AbortSession 是 Host 物理能力、snapshot 观察由 Host 提供）。理由：DG-009 消费
前者的唯一写入口，DG-002/007 消费后者的 transport 观察边界。
