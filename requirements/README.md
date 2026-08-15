# Requirement Packages

> 万象术的**正式 normative 语义树**。每个 `<package>/` 是一个语义 owner，持有不可替代的 WHY、
> 唯一拥有的 WHAT 命题、显式 hard dependencies，以及 package-local 的可执行 proof。
> 全部 packages 同时为真；dependency 只表示 guarantee consumption，不表示优先级。

本树由旧 docs/、旧 changes/、`src/` 综合迁移而来（2026-08-14 cutover，非机械改名）；
旧 `tests/` 已全部分包（2026-08-14 Wave 2a/2b cutover）：测试全部包自有
`<package>/tests/`，共享 harness（support adapters、unit runner、integration
orchestrator、Long Stroke e2e）归 `verification-system/tests/`。每个包目录：

```text
README.md   包入口（阅读顺序 + 概览）
WHY.md      不可替代的存在理由（保姆级）
WHAT.md     唯一 normative 合同（编号命题，每条有测试落点）
HOW.md      实现模型与约束（非 normative；含「历史与弃权」）
PROOF.md    测试落点表（每条 WHAT 命题 → 测试）
tests/      本包拥有的可执行 proof（*.test.mjs）
```

已知 proof gap 聚合台账见 [GAP.md](GAP.md)；包清单与依赖骨架见 [INDEX.md](INDEX.md)。

## 47 包索引

### 1. Requirement system
| Package | 一句话 WHY |
|---|---|
| [requirement-system](requirement-system/README.md) | 当前接受的产品真理必须有唯一 package owner、显式依赖与唯一 proof ownership。 |
| [verification-system](verification-system/README.md) | requirement acceptance 必须由分层、可失败、可重放的证据体系定义。 |

### 2. Programming / causality
| Package | 一句话 WHY |
|---|---|
| [structured-workflow](structured-workflow/README.md) | 业务流程应由宿主语言结构直接表达，不能在领域层再造第二程序计数器/runtime。 |
| [time-capability](time-capability/README.md) | 时间与等待的物理能力必须显式进入系统，不能由 ambient clock/timer 偷渡业务判断。 |
| [causal-wait](causal-wait/README.md) | 等待必须可诊断、可观测，但诊断观测不能升级为业务 authority。 |

### 3. Session / Host substrate
| Package | 一句话 WHY |
|---|---|
| [session-ontology](session-ontology/README.md) | execution class、ownership、attachment 与 personhood 必须正交。 |
| [managed-session-lifecycle](managed-session-lifecycle/README.md) | managed session 的创建、复用、取消、retire、replacement 与 owner closure 必须有单一生命周期合同。 |
| [host-boundary](host-boundary/README.md) | 外部 Host 只提供最小可验证物理能力与稳定观察边界。 |

### 4. Participant / provider world
| Package | 一句话 WHY |
|---|---|
| [participant-identity](participant-identity/README.md) | Role、Persona、ExecutionBinding 分离，换执行者不等于换人。 |
| [execution-model-routing](execution-model-routing/README.md) | managed EffectiveAgent 经唯一 TOML 的七个容量 lane 解析物理模型；`opencode.json` 不拥有 model authority。 |
| [office-capability](office-capability/README.md) | office 由有资格产生的后果定义，不由 persona 名或工具白名单定义。 |
| [capability-enforcement](capability-enforcement/README.md) | provider 看见的与 runtime 真能执行的 capability 同源且不扩大 office entitlement。 |
| [participant-horizon](participant-horizon/README.md) | 只有会改变合法行动的最小事实应穿过 horizon。 |
| [cognitive-environment](cognitive-environment/README.md) | 长期认知层与瞬时 runtime/mission 分开；knowledge 不创造 authority。 |
| [action-affordance](action-affordance/README.md) | 决策点必须知道 act 的正负边界、成功后果与参数意义。 |
| [provider-language](provider-language/README.md) | 一个 life 一个稳定 natural-language world；protocol identifiers 不翻译。 |
| [provider-projection](provider-projection/README.md) | typed semantic intent 经唯一确定性投影成为 provider representation，表示不反向创造 authority。 |
| [external-investigation](external-investigation/README.md) | 外部 facts 以 provenance、source quality、disagreement-aware observation 建立。 |

### 5. Interaction / effect / durability
| Package | 一句话 WHY |
|---|---|
| [interaction-authority](interaction-authority/README.md) | PhysicalUserMessage ≠ AuthorityTurn；typed provenance 才能创建/继续 logical interaction。 |
| [dispatch-protocol](dispatch-protocol/README.md) | 已获授权 interaction 穿过 unreliable Host 时避免 unknown outcome 复制逻辑效果。 |
| [durable-events](durable-events/README.md) | immutable facts + atomic commit + deterministic fold = 单一可重放 substrate。 |
| [effect-accounting](effect-accounting/README.md) | 外部副作用 Requested/Unknown/Accepted 分型；unknown 不能伪装未发生或成功。 |
| [durable-convergence](durable-convergence/README.md) | replicas 按对象语义收敛，不靠 wall-clock/LWW 猜赢家。 |

### 6. Work / execution
| Package | 一句话 WHY |
|---|---|
| [delegation](delegation/README.md) | 语义工作转交时 authority、charge、owner 与返回后果明确。 |
| [intra-participant-parallelism](intra-participant-parallelism/README.md) | 同一 participant 可展开多个 coequal execution presents，而 identity/authority/responsibility 与最终 completion 仍保持一个。 |
| [process-execution](process-execution/README.md) | 真实进程/PTY 有 bounded、可终止、物理完成可信的 execution semantics。 |
| [output-distillation](output-distillation/README.md) | 大输出有损但诚实地压缩；fragment 不能冒充整体成功或发明因果。 |
| [change-integration](change-integration/README.md) | 独立 Git road 只在短原子门内发布，长 review/repair 不全局串行化。 |

### 7. Context continuity
| Package | 一句话 WHY |
|---|---|
| [semantic-trace](semantic-trace/README.md) | participant life 的原始 semantic history append-only、可定位。 |
| [work-record](work-record/README.md) | bounded work 跨 participant/review/finality 传递有 canonical statement。 |
| [context-compression](context-compression/README.md) | 历史过长时只在证据边界用 semantic memory 替换可压缩区。 |
| [prefix-stability](prefix-stability/README.md) | 同一 semantic epoch 已呈现前缀 byte-stable；冷边界由事实驱动。 |

### 8. Failure / recovery
| Package | 一句话 WHY |
|---|---|
| [provider-attempt-recovery](provider-attempt-recovery/README.md) | attempt 失败后可 bounded 换 execution binding，不改变 authority/personhood。 |
| [crash-reconciliation](crash-reconciliation/README.md) | 中断后只从 durable facts + 可信物理观察重入普通程序。 |
| [degeneration-guard](degeneration-guard/README.md) | 未结束 attempt 病态重复时主动止损再交正常 recovery。 |

### 9. Mission / judgement / finality
| Package | 一句话 WHY |
|---|---|
| [obligation-ledger](obligation-ledger/README.md) | mission 持续维护「仍欠世界什么」，不用 phase/status 伪装进度。 |
| [review-judgement](review-judgement/README.md) | PERFECT/REVISE 是 discrimination + proportionate evidence judgement。 |
| [review-assurance](review-assurance/README.md) | judgement 何时可消费由 bounded evidence、fresh witness、causal confirmation 建立。 |
| [finality](finality/README.md) | 不可逆 mission end 基于 obligations + current tree + qualified review evidence。 |

### 10. Feedback
| Package | 一句话 WHY |
|---|---|
| [behavior-diagnosis](behavior-diagnosis/README.md) | pathology 只有满足 trigger/negative/distinction 的 evidence 才成立。 |
| [guidance-delivery](guidance-delivery/README.md) | diagnosis 与何时/如何再次告知分离。 |

### 11. Repository knowledge / programming
| Package | 一句话 WHY |
|---|---|
| [repository-investigation](repository-investigation/README.md) | repository claim 由可定位可追溯真实 observation 建立。 |
| [knowledge-reuse](knowledge-reuse/README.md) | 历史 repository knowledge 是 best-effort cache/hint，不冒充当前证明。 |
| [repository-programming](repository-programming/README.md) | repository mutation 用 capability-projected、sandboxed、all-or-nothing surface。 |

### 12. Optimization / epistemics
| Package | 一句话 WHY |
|---|---|
| [speculative-investigation](speculative-investigation/README.md) | disposable speculation 只在 authoritative world 零影响时才可换成本收益。 |
| [epistemic-reasoning](epistemic-reasoning/README.md) | 认识状态区分 proposal/evidence 与不确定性，由 controller 治理信息动作与停止。 |

### 13. Delivery
| Package | 一句话 WHY |
|---|---|
| [distribution](distribution/README.md) | shipped artifact 携带 runtime code/resource closure；consumer 不依赖源码树/cwd。 |

## 依赖骨架

权威依赖清单见各包 HOW.md / README.md 的 DEPENDS ON 节，与 `requirements/INDEX.md` 的 102-edge 骨架一致（0 cycle）。

## 运行与验证

```text
node requirements/verification-system/tests/run.mjs          # 单元套件（自动发现 requirements/<package>/tests/**/*.test.mjs）
node --test requirements/<pkg>/tests/<file>.test.mjs   # 单包单文件
node scripts/check.mjs           # 全 static gates
```

- 每条 WHAT 命题的测试落点见该包 PROOF.md。
- 迁移状态：旧 `docs/`、`changes/` 已于 2026-08-14 cutover 归档删除（git 可回溯）；`tests/` 已全部分包（见各包 PROOF.md）。
