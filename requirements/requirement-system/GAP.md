# requirement-system — GAP

| GAP | 命题 | 缺口 | 状态 | 承载 | 计划 | Owner |
|---|---|---|---|---|---|---|
| GAP-REQ-020 | REQUIREMENT-SYSTEM-020 migration ledger 永久退役 | 退役后不得复活：路径、wiring、规范面引用与 019 编号复用均须机械红 | CLOSED | `scripts/checks/ledger-retirement-gate.mjs`（退役路径/入口 wiring/全仓引用/019 复现四查）+ `requirements/requirement-system/tests/ledger-retirement.test.mjs` 三断言 | 已落地，gate 与测试对真实树绿 | requirement-system |
