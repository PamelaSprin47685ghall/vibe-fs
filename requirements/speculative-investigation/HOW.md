# speculative-investigation — HOW

## 架构模型与执行流

`speculative-investigation` 在主模型调用管线中以非侵入方式运行：

```text
主模型请求机会 (Opportunity)
  ↓
决策评估 (Eligibility 校验 → 确定性 Control 分流 → 价值方程与预算判定)
  ↓
[K0 / Shadow / 熔断]: 不启动投机，直接执行主请求
[DryRun]: 异步启动真实 Replica 子会话，记录审计日志，主路径立即无等待继续
[Treatment (K1/K2)]:
  启动短生命周期 Replica (fast peer + 只读工具约束)
  ↓
  批量收割只读 call/result → 校验完整配对与字节上限
  ↓
  EventStore.append(StrengthCandidatePrepared) (持久化候选帧引用)
  ↓
  向主模型目标运行注入 Candidate Frames
  ↓
  主模型产出真实输出 → EventStore.append(StrengthCandidatePromoted)
  ↓
  下一轮变换中确定性重建 Promoted Frames 并纳入 XTrace 时间线
```

## 核心机制

### 1. 机会评估与预算分配 (Opportunity & Budgeting)

- **资格准入**：严格校验会话类、主请求类型、角色、未取消状态与模型健康度；任一条件缺失直接降级为 K0。
- **价值方程**：基于预测准确率、主/从模型边际成本、响应延迟与风险项计算期望净收益。仅当收益显著为正且样本量达标时方可批准 K1/K2 预算。
- **确定性对照组**：通过确定性哈希将固定比例的请求分配给 control holdout，保证评估不受运行时随机扰动影响。

### 2. 副本生命周期与权限控制 (Replica Lifecycle & Gate)

- **轻量会话**：创建为附属于 Owner 的内部叶节点子会话，继承 Owner 的 Persona 与 Language，仅将执行绑定重定向至快速模型。
- **严格只读**：工具清单与底层门禁严格限制为 `read`、`glob`、`grep`，任何写操作或越权调用直接被拦截并终止副本。
- **请求预算拦截**：按 provider request 计数，达到预算 K 后立即切断后续外发，防止成本失控。

### 3. 候选物化、持久化与晋升 (Frame Canonicalization & Promotion)

- **规范化编码**：提取纯粹的工具调用与结果，剔除模型中间推理文本；Owner 侧工具调用 ID 采用确定性哈希派生。
- **两阶段提交**：
  - 候选帧就绪后，先写入 `StrengthCandidatePrepared` 并将大对象存入 payload_refs；
  - 仅当主模型在目标运行中产生可用输出后，触发 `StrengthCandidatePromoted`；
  - 若目标运行失败、被取消或未产生输出，候选帧自然废弃，不污染语义历史。

### 4. 影子与 DryRun 机制 (Shadow & DryRun)

- **Shadow 模式**：仅执行预测计算并记录特征日志，不启动物理副本，用于收集真实基准数据。
- **DryRun 模式**：启动真实物理子会话执行只读请求以供宿主观测，但完全解耦主路径等待与因果提交逻辑。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| SPEC-INV-001 | `requirements/speculative-investigation/tests/host-canary-k0.test.mjs` |
| SPEC-INV-002 | `requirements/speculative-investigation/tests/authority-policy.test.mjs` |
| SPEC-INV-003 | `requirements/speculative-investigation/tests/batch-collector.test.mjs` |
| SPEC-INV-004 | `requirements/speculative-investigation/tests/authority-policy.test.mjs` |
| SPEC-INV-005 | `requirements/speculative-investigation/tests/frame-projection.test.mjs` |
| SPEC-INV-006 | `requirements/speculative-investigation/tests/commit-promotion.test.mjs` |
| SPEC-INV-007 | `requirements/speculative-investigation/tests/turn-evidence.test.mjs` |
| SPEC-INV-008 | `requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs` |
| SPEC-INV-009 | `requirements/speculative-investigation/tests/projection-algebra.test.mjs` |
| SPEC-INV-010 | `requirements/speculative-investigation/tests/authority-policy.test.mjs` |
| SPEC-INV-011 | `requirements/speculative-investigation/tests/host-policy.test.mjs` |
| SPEC-INV-012 | `requirements/speculative-investigation/tests/invisibility.test.mjs` |
| SPEC-INV-013 | `requirements/speculative-investigation/tests/dry-run-shadow.test.mjs` |
