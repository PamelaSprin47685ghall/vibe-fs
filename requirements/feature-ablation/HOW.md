# feature-ablation — HOW

## 架构

| 组件 | 路径 |
|---|---|
| 内核类型 | [`src/Wanxiangshu/Ablation/Model.fs`](../../src/Wanxiangshu/Ablation/Model.fs) |
| Manifest 加载 / DAG 校验 | [`src/Wanxiangshu/Ablation/Manifest.fs`](../../src/Wanxiangshu/Ablation/Manifest.fs) |
| Env + Registry | [`src/Wanxiangshu/Ablation/Settings.fs`](../../src/Wanxiangshu/Ablation/Settings.fs) |
| 工具映射 | [`src/Wanxiangshu/Ablation/ToolMap.fs`](../../src/Wanxiangshu/Ablation/ToolMap.fs) |
| Execute/schema gate | [`src/Wanxiangshu/Ablation/Gate.fs`](../../src/Wanxiangshu/Ablation/Gate.fs) |
| JS semantic surface | [`src/Wanxiangshu/Ablation/Surface.fs`](../../src/Wanxiangshu/Ablation/Surface.fs) |
| 节点 / 边 / profile | [`resources/ablation/nodes.json`](../../resources/ablation/nodes.json), [`profiles.json`](../../resources/ablation/profiles.json) |
| 工具→节点 | [`resources/ablation/tool-map.json`](../../resources/ablation/tool-map.json) |
| 事实→节点 | [`resources/ablation/fact-map.json`](../../resources/ablation/fact-map.json) |

配置优先级：`WANXIANGSHU_ABLATION_<node>` 显式 env > `WANXIANGSHU_ABLATION_PROFILE` > production 全 active。

Profile 按巡检段落展开：`station-05`…`station-56` 使用 segment 解锁（A≤4、B≤14、C≤21、D≤30、E≤41、F≤49、G≤54、H≤56），F 段前 `delegation` 为 borrowed、`delegation.async-fork` ablated。

## Enforcement 接线

| 优先级 | 文件 | 节点 / 行为 |
|---|---|---|
| P0 | [`ToolRegistry.fs`](../../src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs) | `Path.DeniedAblation` execute gate |
| P0 | [`StaticTools.fs`](../../src/Wanxiangshu/OpenCode/Tools/StaticTools.fs) | `permissionObj` / `requestToolMap` schema 过滤 |
| P1 | [`Strength/OpenCode/Settings.fs`](../../src/Wanxiangshu/Strength/OpenCode/Settings.fs) | `speculative-investigation` ablated ⇒ Off |
| P1 | [`ManagedAgentConfig.fs`](../../src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs) | manager/orchestrator/browser/inquiry hidden |
| P1 | [`FissionHostSurface.fs`](../../src/Wanxiangshu/OpenCode/Host/FissionHostSurface.fs) | `intra-participant-parallelism` ablated ⇒ 隐藏 fission |
| P2 | [`SphinxMcpConfig.fs`](../../src/Wanxiangshu/OpenCode/Host/SphinxMcpConfig.fs) | `epistemic-reasoning` ablated ⇒ Disabled |
| P3 | [`AgentJournal.fs`](../../src/Wanxiangshu/Persistence/Journal/AgentJournal.fs) | `fact-map` ablated ⇒ `journal/denied-ablation` |

## 54 包 hook census（living）

| 包 | 主审站 | Enforcement / Borrowed 面 | 零影响断言 |
|---|---|---|---|
| requirement-system | 01 | 无 runtime hook | 规范-only |
| verification-system | 02 | 无 runtime hook | 规范-only |
| js-semantic-surface | 03 | surface manifest | 测试入口 |
| structured-workflow | 04 | compile shard gate | 结构-only |
| host-boundary | 05 | plugin / Host hooks | B 段 substrate |
| session-ontology | 06 | 装配事实 | B 段 active≤14 |
| participant-identity | 07 | identity 链 | B 段 active≤14 |
| provider-language | 08 | language binding | B 段 active≤14 |
| office-capability | 09 | StaticTools 矩阵 | B 段 active≤14 |
| capability-enforcement | 10 | ToolRegistry gate | B 段 active≤14 |
| cognitive-environment | 11 | prompt / assume | B 段 active≤14 |
| action-affordance | 12 | read/glob/grep schema | B 段 active≤14 |
| participant-horizon | 13 | horizon tool | B 段 active≤14 |
| provider-projection | 14 | transforms | B 段 active≤14 |
| repository-investigation | 15 | inspect | C 段；#15 borrow 无 inspect 拒绝 |
| requirement-grounding | 16 | grounding hooks | C 段 active≤21 |
| repository-programming | 17 | js-* tools | C 段 active≤21 |
| time-capability | 18 | IClockPort 注入 | C 段 active≤21 |
| process-execution | 19 | run/pty tools | C 段 active≤21 |
| causal-wait | 21 | wait 诊断 | C 段 active≤21 |
| durable-events | 22 | journal | D 段 active≤30 |
| effect-accounting | 23 | effect 链 | D 段 active≤30 |
| semantic-trace | 24 | trace capture | D 段 active≤30 |
| context-compression | 25 | Blogger | D 段 active≤30 |
| prefix-stability | 26 | prefix transforms | D 段 active≤30 |
| work-record | 27 | WorkRecord 投影 | D 段 active≤30 |
| behavior-diagnosis | 28 | enforcer | D 段 active≤30 |
| guidance-delivery | 29 | guidance | D 段 active≤30 |
| attention-regulation | 30 | enough/abandon/defer | D 段 active≤30 |
| execution-failure-policy | 31 | failure policy | E 段 active≤41 |
| interaction-authority | 32 | authority | E 段 active≤41 |
| managed-chat-execution | 33 | chat execution | E 段 active≤41 |
| execution-model-routing | 34 | model routing | E 段 active≤41 |
| managed-session-lifecycle | 35 | session lifecycle | E 段 active≤41；sync borrow 前提 |
| dispatch-protocol | 36 | dispatch | E 段 active≤41 |
| provider-attempt-recovery | 37 | recovery | E 段 active≤41 |
| host-provider-failure-ownership | 38 | failure presentation | E 段 active≤41 |
| degeneration-guard | 39 | loop detector | E 段 active≤41 |
| crash-reconciliation | 40 | restart | E 段 active≤41 |
| durable-convergence | 41 | writer merge | E 段 active≤41 |
| delegation | 42 | fork/resume/join | F 段；15–41 borrowed sync |
| concern-routing | 43 | subscribe/publish | F 段 active≤49 |
| intra-participant-parallelism | 44 | fission tool/schema | F 段 active≤49 |
| knowledge-reuse | 45 | chronicle/fetch | F 段 active≤49 |
| epistemic-reasoning | 47 | sphinx MCP/agent | F 段 active≤49 |
| speculative-investigation | 48 | Strength pipeline | F 段 active≤49；profile≤14 强制 Off |
| institutional-learning | 49 | celebrate/regret | F 段 active≤49 |
| obligation-ledger | 50 | todowrite | G 段 active≤54 |
| relay-incumbency | 51 | manager agent | G 段 active≤54 |
| relay-assessment | 52 | review tool | G 段 active≤54 |
| relay-context-projection | 53 | narrative cut | G 段 active≤54 |
| relay-retirement | 54 | suicide tool | G 段 active≤54 |
| change-integration | 55 | commission / orchestrator | H 段 active≤56 |
| distribution | 56 | 交付验收 | H 段；无额外 ablation hook |
| feature-ablation | — | 本包 kernel + manifest | 自检 |

子节点：`delegation.sync-delegate`、`delegation.async-fork`、`change-integration.orchestrator-tools` 见 [`nodes.json`](../../resources/ablation/nodes.json)。


## GAP

见 [`requirements/GAP.md`](../GAP.md) GAP-036。
