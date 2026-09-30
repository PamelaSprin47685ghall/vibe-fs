# requirements/ 语义合并审查报告（2026-09-30）

审查范围：我们 PR 分支的完整 requirements 版本（`f7d13d919`，PR#42+#43 链）与 upstream/master（`5321fba04`）的 227 个差异文件，参照合并前历史版本判定语义合并正确性。重点：upstream replay（Kunwei Zhang 以 `a319fe1ea`/`904bbd554` 等重写）是否丢失或削弱了我们的规范与测试。

方法：三态分类（PRESENT/ABSENT/DIVERGED），聚焦 ABSENT（上游完全未采纳）与语义反向的 DIVERGED；WHAT.md 约束词强度对比；测试用例增删核对。

---

## 一、核心发现：5 个 WHAT.md 的语义收紧被 upstream 完全丢弃（ABSENT）

以下规范文档在 `25ff1cd6d`（restore requirement boundaries）中被我们收紧，但 upstream replay **完全没有采纳**，仍保留旧版（更弱）：

| 文件 | 我们收紧的内容 | upstream 现状 | 风险 |
|---|---|---|---|
| `relay-incumbency/WHAT.md` [002] | “Relay 只表达**不可变**领域事实” | 无“不可变” | 允许可变事实，削弱事件溯源保证 |
| `relay-incumbency/WHAT.md` [006] | 退休提交须含“**退休身份**” | 无 | 缺身份字段，追踪不完整 |
| `relay-incumbency/WHAT.md` [010] | 旧任失去“新派工**与调用权**” | 仅“新派工权” | 旧任仍可调用，权限泄漏 |
| `relay-assessment/WHAT.md` | 快照改变后旧证书“**立即失效，不得作为新改动的验证**” | “不能证明新快照” | 措辞弱化，允许旧证书间接佐证 |
| `relay-context-projection/WHAT.md` | Projection “**不得包含** hidden reasoning” | “不另行注入” | 由“禁止”降为“不主动注入”，语义减弱 |
| `managed-session-lifecycle/WHAT.md` | Dedicated 键“**同一 scope 内每个 Role 至多一个活动会话**” | “每个键至多一个” | 键定义模糊，允许同 Role 多会话 |
| `managed-session-lifecycle/WHY.md` | Reusable “**按作用域与角色复用，让同一职责沿用会话、不同职责各自隔离**” | “服务一个作用域” | 丢失职责隔离的语义 |

**判断**：这些是实质的规范削弱。upstream replay 用旧版覆盖了我们的收紧版。

## 二、语义方向相反的 DIVERGED（最严重）

`intra-participant-parallelism/WHAT.md`：

- **我们版本**：拒绝“Sphinx 内部**只读** Engineer”（禁止任何只读 Engineer 参与并行）
- **upstream 版本**：拒绝列表**删除**“Sphinx 内部只读 Engineer”，改为“Sphinx 内部**标准** Engineer 遵守相同准入，**不另设只读身份**”

**判断**：方向完全相反。我们禁止只读 Engineer，upstream 取消只读身份概念、允许标准 Engineer。这不是丢失，是**语义反转**。需裁决：Sphinx 内部 Engineer 是否应有特殊只读豁免。

## 三、丢失的测试用例（upstream 完全未采纳，ABSENT）

| 文件 | 性质 | 内容 |
|---|---|---|
| `verification-system/tests/integration/harness/cases.mjs` | **+73/-0 纯新增** | `strict-mock-server.js` 关闭路径的失败注入测试：`closeAllConnections` 抛错、close 回调失败、close 抛异常、重复 stop 幂等。真实测试，upstream 完全没有 |
| `host-boundary/tests/023.test.mjs` | +72/-9 | 真实 Host chat-admission canary 强化（版本证据、drift 检测） |
| `host-boundary/tests/support/run-opencode-chat-admission-canary.mjs` | +174/-140 | 配套 canary 支撑重写 |
| `sphinx-v2/tests/036.test.mjs` | +32/-0 | MCP stdio 真实启动 canary（已在本地 master 重做，见 `2a6cbda6e`） |
| `verification-system/tests/e2e/support/{process-host-utils,process-lifecycle,strict-mock-server}.js` | 各 +10~18 | e2e 支撑加固 |
| `verification-system/tests/support/verdict-feed.mjs` | +2/-1 | verdict 归属修复（已在本地 master 重做） |

**判断**：`cases.mjs` 的 73 行关闭路径失败注入测试是**明确丢失的测试覆盖**，且其依赖的 `strict-mock-server.js` 加固也一并丢失。这是本次审查发现的最具体的测试缺口。

## 四、测试覆盖减少（DIVERGED 中的削弱）

- `crash-reconciliation/tests/020.test.mjs`：7 个测试减为 5 个，删除 2 个取消桥测试（`the actual cancellation bridge closes child authority`、`the cancellation bridge leaves Completed to its own completion path`），并移除 `child-settlement.mjs` 支撑。upstream 在 PROMPT-006 重构后重写，取消路径的折叠证据覆盖减弱。

## 五、被删除的测试文件（8 个，upstream 无对应）

| 文件 | 删除方 | 判断 |
|---|---|---|
| `behavior-diagnosis/tests/020.test.mjs` | PR#46 squash（c07b5ca79，**我们自己**） | 我们主动删的 |
| `participant-identity/tests/011.test.mjs` | 同上 | 同上 |
| `crash-reconciliation/tests/support/child-settlement.mjs` | upstream replay | 随 020 重写失效 |
| `host-boundary/tests/support/manager-review-contract.mjs` | upstream replay | 支撑重构 |
| `structured-workflow/tests/fixtures/{dormant,fake}-plugin-transform-order.fs` | upstream replay | 夹具重构 |

## 六、核对为“正确/增强”的项

- `dispatch-protocol/WHAT.md`：upstream replay 把所有条款**改写得更详尽**（[002][004][007][008] 均补充细节），语义增强而非丢失。
- `GAP.md`：upstream **诚实下调**验证状态（GAP-064/069/134/135/148/154 从“完成”改为 PARTIAL/OPEN），并更新 GAP-026/109 反映 native todowrite/assume 新语义。负责任的合并。
- `speculative-investigation/tests/`：17 个文件 6636 行插入 vs 1256 删除，新增 015/016/020 协议测试，大幅增强。
- `verification-system/tests/003,008.test.mjs`：upstream 选择**保留并演进**（PR#42 想删，upstream 未删）——上游决定，非缺陷。

---

## 处置建议

1. **P0 裁决**：`intra-participant-parallelism` 的 Sphinx 只读 Engineer 语义方向相反，需与 upstream 对齐或提交 PR 恢复我们的禁止语义。
2. **P1 恢复规范收紧**：5 个 WHAT.md 的 ABSENT 收紧条款，建议重新提交（upstream replay 用旧版覆盖）。
3. **P1 恢复丢失测试**：`cases.mjs` 的 73 行 strict-mock-server 关闭失败注入测试，建议重做（其依赖的 `strict-mock-server.js` 加固也缺失）。
4. **P2 评估**：`crash-reconciliation/020` 的取消桥测试删除是否可接受（PROMPT-006 重构后取消路径是否仍受覆盖）。

本次审查未修改任何 requirements/ 文件。
