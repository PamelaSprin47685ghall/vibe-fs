# Post-Freeze 证据 — 封炉工装自身的输出

本目录保存封炉期结束时第 0 层静态检查器的实测输出，以及删除行数门禁后的
单元测试结果。

采集时间：2026-07-29T22:14+08:00
基线 commit：`274a30aa`

## 与 pre-shock 的区别

`pre-shock/` 是旧世界最后一次完整机器反馈，用于日后判断哪些失败是迁移引入的。

`post-freeze/` 是封炉工装的自证：静态检查器本身能跑、能测出预期的残留、
能在正确的地方报警。休克期这三份输出是唯一允许刷新的机器反馈。

## 文件

| 文件 | 内容 |
|------|------|
| `ssot-lint.txt` | 规范内部一致性检查 |
| `shock-audit.txt` | 旧符号灭绝表残留 + 单一写入口 + SHOCK 标记 |
| `test-next-after-gate-removal.txt` | 删除行数门禁后的 `test:compile` + `test:next` 完整输出 |

## ssot-lint

```text
ssot-lint: OK — 118 条款，242 处引用，13 个文件
```

118 条款、242 处引用全部有定义，无重复 ID，无前缀归属错误，规范中无实现状态词。

封炉期新增 4 条：`FALLBACK-010`（Host Attempt ≠ ConsecutiveFailureCount）、
`HOST-010`（transform → ProviderRunIdentity 绑定）、`HOST-011`（tool 身份两个半边）、
`VERIFY-008`（测试语言边界）。

## shock-audit

灭绝表 40 行，测出的关键残留：

```text
prompt_async                     next 5   （目标 1，唯一 Host adapter）
PluginPromptAccepted             next 7   tests 5
ConfirmationPhysicalMessageId    next 11
AcceptedContinuationIds          next 9   tests 6
AcceptedContinuationRoots        next 8
AgentLinked/Forked/Unlinked      next 12  tests 26
剧本森林 15 项                    testkit 6~18 各项
"turn" 剧本字段                   scripts 254
```

单一写入口检测的实测结果是本次采集最重要的一条：

```text
FallbackCursorAdvanced   FALLBACK-003   3 writers
  next/OpenCode/FallbackDetect.fs                          （helper 定义 + append）
  next/OpenCode/ProviderFailureWakeup.fs:50  via recordFallbackFailure
  next/OpenCode/RetrySignalHandler.fs:84     via recordFallbackFailure

FallbackExhausted        FALLBACK-005   absent (fact not defined yet)
  next/Kernel/Outcome.fs 有同名但无关的 terminal-outcome case
```

`shock-audit` 不只数事实名，还追 append helper 的调用方。只数名字会得到
「1 个 writer」的错误结论——一个 helper 把两个 writer 藏在一次调用后面，
这正是 FALLBACK-003 当前的违规形态。

包 C 完成时这两行必须变成 `ok (1)`。

## 行数门禁删除的影响

删除项：`ArchitectureGates.Next_source_files_do_not_exceed_300_lines`、
`ArchitectureGates17` 的 §17.7 行数带（300 硬失败 / 280 阻断 / 260 告警）、
`ArchitectureGateSupport` 的两张 280 行 allowlist。

```text
删除前：290 passed / 3 failed / 293 total
删除后：291 passed / 1 failed / 292 total
```

变化完全可解释：

| 项 | 解释 |
|----|------|
| 293 → 292 | 删掉 1 个测试（`Next_source_files_do_not_exceed_300_lines`） |
| 3 failed → 1 failed | 该测试本身失败（13 个文件超 260）；`ArchitectureGates17` 也因 `TurnCompletionProgram.fs` 331 行硬失败，删除行数带后转绿 |
| 290 → 291 passed | `ArchitectureGates17` 的其余语义门禁（机械后缀、Host 边界、禁止词、重复算法归属）全部通过 |

剩余 1 个失败：

```text
✗ ReviewRequirementBoundaryTests > Confirmed reviewer terminal resets
  only previously reviewed human requirements: Assert.equal failed.
```

属 Review 语义，与行数门禁无关，休克前即存在（见 `pre-shock/unit-baseline.txt`），
由工作包 D 覆盖。

理由见 `SSOT/10.md` VERIFY-005：行数是症状不是病因，用它代理会产生反向激励——
为过门禁而做机械拆分，把内聚语义边界切成互相调用的碎片。真正防止拆分逃逸的是
机械后缀 allowlist，那一项保留。

## 本目录不采集什么

不采集 canary / E2E / `test:release`。它们的 fixture 即将被包 K 整体重写为 TOML，
采集旧结果不产生可用于新世界的判据。`gate-testkit` 在 `pre-shock/` 已采集一次
（29 passed），它验证的是 mock 森林与隔离机制本身，在退火三仍是第一层门禁。
