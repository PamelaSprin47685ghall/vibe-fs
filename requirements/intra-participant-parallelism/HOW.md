# intra-participant-parallelism — HOW

## 架构机制

### 准入检查与会话替换

1. **Origin 门禁**：执行侧首先验证调用方是否具有物理父会话（`parentID` 存在）。根会话（root session）在解析 prompt 前即被拦截并拒绝，并在工具投影中显式关闭裂变可见性。
2. **参数校验与原子准入**：校验 `prompts` 数组（N≥2 且非空），预留并发槽位后，为每条 lane 创建与原 caller 具有相同父级的 fresh sibling 会话。
3. **首载荷注入与静默交接**：各 lane 继承调用方的角色配置与语言设置，注入原 caller 的 canonical LWR 与对应 lane 输入。全量 lanes 建立成功后，向原 caller 发起 Fission 专属的静默中断，无缝移交执行流。

### 债权分配与收敛网络

- **广播与亲和分配**：裂变前的未完成子任务（subagents / PTY）注册为广播源，其完成事实向每条 lane 投递一次；裂变后新创建的子任务自动附加发起 lane 的亲和标记，仅由发起 lane 消费。
- **Keyed 收敛**：各 lane 完成后将自身的 WorkRecord 以 lane 索引为 key 登记至 bundle 中。环形或聚合节点按稳定索引排序合并工作记录，并在所有债权结算后由最终 lane 交付 logical owner 的 terminal completion。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| INTRA-PARTICIPANT-PARALLELISM-001 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-002 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-003 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-004 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-005 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-006 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-007 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-008 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-009 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-010 | `requirements/intra-participant-parallelism/tests/fission-source-ratchet.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-011 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-012 | `requirements/intra-participant-parallelism/tests/fission-source-ratchet.test.mjs` |
| INTRA-PARTICIPANT-PARALLELISM-013 | `requirements/intra-participant-parallelism/tests/fission-tool-origin.test.mjs` |
