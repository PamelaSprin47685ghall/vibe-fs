# Sphinx clean-break 文件处置表（WP-00 产物）

对应 `proposals/Sphinx.md` 第 22 章的 54 对 / 108 文件，逐条落到真实仓库路径。
`迁移` = 可移植的机制（数学实现、严格解码、canonical 序列化），由新契约重新解释；`删除` = 最终生产树不含，且无转发层。
每行的 `.fsi` 与 `.fs` 同处置。

| # | 真实路径（去掉 `.fs`） | 处置 | 新位置 | 说明 |
|---|---|---|---|---|
| 01 | `Sphinx/Core/Hash` | 迁移 | `Sphinx/V2/Core/Projection` + `Sphinx/V2/Runtime/Ports` + `Sphinx/V2/Wire/Encode` | 保留 canonical JSON 思路；区分 traceHash/stateHash/semanticHash |
| 02 | `Sphinx/Core/Model` | 拆分重写 | `Sphinx/V2/Core/{Ids,Envelope,Goal,Graph,Certificate,Work,Budget,Events,State}` | 保存根目标；revision/attempt 分离；证书带 scope |
| 03 | `Sphinx/Core/Reducer` | 原位重写 | `Sphinx/V2/Core/{Reducer,Commands}` + `Sphinx/V2/Runtime/Admission` | 修 guarantee 放行；拒绝无事件补洞 |
| 04 | `Sphinx/Plugins/AStar/Refiner` | 迁移数学，重写接口 | `Sphinx/V2/Plugins/AStar/Refiner` | 增量 step、reopen、全局 bound |
| 05 | `Sphinx/Plugins/Bayes/Exact` | 迁移数学，改输入契约 | `Sphinx/V2/Plugins/Bayes/Exact` | log-space；区分重复与相关因子 |
| 06 | `Sphinx/Plugins/Mcts/Refiner` | 重写 | `Sphinx/V2/Plugins/Mcts/Refiner` | 生成器端口；coverage 默认 empirical-only |
| 07 | `Sphinx/Plugins/Ordinal/Inference` | 拆分补全 | `Sphinx/V2/Plugins/Ordinal/{Model,Pairwise,Ranking,Fit,DesignCheck}` | 稳定 log-likelihood、完整协方差、tie/abstain |
| 08 | `Sphinx/Plugins/Questionnaire/Protocol` | 拆分重写 | `Sphinx/V2/Plugins/Questionnaire/{Model,Design,Prompts,Decode}` | host-private label、missingness |
| 09 | `Sphinx/Plugins/Stop/Certificate` | 整体替换 | `Sphinx/V2/Plugins/Inquiry/Stop` | answer.now 参加选择；缺证据不算通过 |
| 10 | `Sphinx/Plugins/Truthful/SelfPrediction` | 退出发布构建 | 后续 Experimental 插件 | 不占 default capability |
| 11 | `Sphinx/Runtime/Agenda` | 拆分重写 | `Sphinx/V2/Runtime/{Agenda,Decision,Refinement}` | 删除 ID 排序选优 |
| 12 | `Sphinx/Runtime/Certificate` | 拆分重写 | `Sphinx/V2/Core/Certificate` + `Runtime/{Admission,Refinement}` | slot CAS、credible/coverage 分型 |
| 13 | `Sphinx/Runtime/Plugin` | 替换 | `Sphinx/V2/Runtime/Contracts` | manifest 绑定可执行能力 |
| 14 | `Sphinx/Runtime/PluginRegistry` | 迁移增强 | `Sphinx/V2/Runtime/Registry` | executable binding、content hash |
| 15 | `Sphinx/Absorb` | 删除 | `Sphinx/V2/Plugins/Inquiry/Observe` | 去掉 rootGainForProposal/gainFromMethod |
| 16 | `Sphinx/Bayes` | 删除 | `Sphinx/V2/Plugins/Bayes/Exact` 唯一产能 | 移除旧 EpistemicState projection |
| 17 | `Sphinx/Closure` | 删除 | `Sphinx/V2/Runtime/Refinement` + `Plugins/Inquiry/Plan` | 去掉固定五阶段流水线 |
| 18 | `Sphinx/Codec` | 替换 | `Sphinx/V2/Wire/{Decode,Encode}` | 无旧 Request 转发 |
| 19 | `Sphinx/DecodePrimitives` | 迁移通用解析 | `Sphinx/V2/Wire/Decode` | 删除 QuestionForm/EvidenceKind 推断 |
| 20 | `Sphinx/EventVocabulary` | 替换 | `Sphinx/V2/Core/Events` + `Sphinx/V2/Persistence/Codec` | 新类型 `sphinx/v2-transition@1` |
| 21 | `Sphinx/GecDecode` | 重写拆分 | `Sphinx/V2/Wire/Decode` + `Sphinx/V2/Persistence/Codec` | 删除按位置补 parent |
| 22 | `Sphinx/GecElicit` | 删除 | Questionnaire + Inquiry/Stop + Wire/Surface | 不暴露 caller 填后验终止 |
| 23 | `Sphinx/GecHost` | 删除假执行 | `Sphinx/V2/Hosts/OpenCode/Adapter` + `Runtime/Recovery` | 真实 dispatch/abort/reconcile |
| 24 | `Sphinx/GecInquiry` | 删除第二 registry | `Sphinx/V2/Runtime/Driver` + canonical Current | results-only 表退出 |
| 25 | `Sphinx/GecLegacy` | 删除 | 无运行时替代 | 旧轨迹只读对照 |
| 26 | `Sphinx/GecRefine` | 删除 | `Sphinx/V2/Runtime/Registry` | 数学引擎 typed 调用 |
| 27 | `Sphinx/GecStore` | 删除 | `Sphinx/V2/Persistence/{Codec,Integrator,Export}` | 唯一权威 fold |
| 28 | `Sphinx/GecSurface` | 删除 | `Sphinx/V2/Wire/Surface` | 去 placeholder、去长度 hash |
| 29 | `Sphinx/GenericDurability` | 替换 | `Sphinx/V2/Persistence/Codec` | v2 batch 含完整运行状态来源 |
| 30 | `Sphinx/GenericIntegrator` | 替换 | `Sphinx/V2/Persistence/Integrator` | 调用唯一 Core/Reducer |
| 31 | `Sphinx/Inquiry` | 删除 | `Sphinx/V2/Core/{Commands,Reducer}` + `Persistence/Integrator` | 移除 Working EpistemicState |
| 32 | `Sphinx/InquiryRuntime` | 整体重写 | `Sphinx/V2/Runtime/{Driver,Admission,Recovery}` | 本地队列不冒充 CAS |
| 33 | `Sphinx/InquirySurface` | 替换 | `Sphinx/V2/Wire/Surface` | 删 runExpected turn-price |
| 34 | `Sphinx/IntegrationRules` | 重写 | `Sphinx/V2/Persistence/Integrator` + `Sphinx/V2/Composition/Bind` | v2 rule 独立 key |
| 35 | `Sphinx/LegacyDurability` | 删除 | 历史版本工具离线读取 | 不迁历史数据 |
| 36 | `Sphinx/LegacyIntegrator` | 删除 | 历史版本工具离线读取 | 旧事件只保留 |
| 37 | `Sphinx/Mcp` | 保留路径改内容 | `Sphinx/V2/Mcp` + `Sphinx/V2/Hosts/Mcp/Contract` | 工具清单与 v2 一致 |
| 38 | `Sphinx/McpContract` | 重写移动 | `Sphinx/V2/Hosts/Mcp/Contract` | 删 nextTool 阶段映射 |
| 39 | `Sphinx/McpServer` | 重写移动 | `Sphinx/V2/Hosts/Mcp/Server` | SDK 只在 adapter |
| 40 | `Sphinx/Methodology` | 删除 | `Sphinx/V2/Plugins/Probes/{Catalog,Prompts}` | 不移植权重 |
| 41 | `Sphinx/MonteCarlo` | 删除 | `Sphinx/V2/Plugins/Mcts/Refiner` | 按模型/horizon/scope 保存 |
| 42 | `Sphinx/ObservationCodec` | 删除 | `Sphinx/V2/Wire/Decode` + `Questionnaire/Decode` | 版本化 envelope |
| 43 | `Sphinx/Policy` | 删除 | `Sphinx/V2/Runtime/Decision` + `Plugins/Inquiry/{Stop,Render}` | 删固定阶段与标量 stop |
| 44 | `Sphinx/Representation` | 删除 | 必要偏序在对应插件实现 | 价值偏序与资源支配分离 |
| 45 | `Sphinx/RuntimeTypes` | 删除 | `Sphinx/V2/Core/{Work,Commands,State}` + `Plugins/Inquiry/Model` | 旧 Request 不穿 v2 边界 |
| 46 | `Sphinx/Search` | 删除 | `Sphinx/V2/Plugins/AStar/Refiner` | 单一 typed capability |
| 47 | `Sphinx/ServeEntry` | 保留路径重写 | `Sphinx/V2/ServeEntry` + `Composition/Bind` | 生产必须 durable store |
| 48 | `Sphinx/Session` | 删除 | `Sphinx/V2/Runtime/Driver` + canonical Current | cache 可丢弃 |
| 49 | `Sphinx/State` | 删除 | `Sphinx/V2/Core/{State,Goal,Budget}` | 不按 QuestionForm 生成 RootContract |
| 50 | `Sphinx/Surface` | 重写 | `Sphinx/V2/Wire/Surface` | 唯一 façade |
| 51 | `Sphinx/TurnBudget` | 删除 | `Sphinx/V2/Core/Budget` + Host/provider 成本端口 | 删 1.44/(expected-1)^2 |
| 52 | `Sphinx/Types` | 删除 | `Sphinx/V2/Core` + `Plugins/Inquiry/Model` | Finding/Evidence 不在 Core |
| 53 | `Sphinx/Value` | 删除 | `Sphinx/V2/Plugins/Inquiry/DecisionModel` | 删 0.72/gateway/发现数量估值 |
| 54 | `Sphinx/WireEncode` | 重写 | `Sphinx/V2/Wire/Encode` | 只含 plain JS/JSON |
| — | `OpenCode/Host/SphinxExecution` | 重写 | `Sphinx/V2/Hosts/OpenCode/Adapter` | 真实 receipt，不用固定 active |
| — | `OpenCode/Host/SphinxExecutionSurface` | 重写 | `Sphinx/V2/Hosts/OpenCode/AdapterSurface` | 只做 native 值转换 |
| — | `OpenCode/Tools/SphinxTool` | 替换 | `Sphinx/V2/Hosts/OpenCode/Tool` | 只暴露 v2 工具 |
| — | `OpenCode/Host/SphinxConfig` | 迁移审阅 | `Sphinx/V2/Hosts/OpenCode/Config` | 配置注入不改 |
| — | `OpenCode/Host/SphinxMcpConfig` | 保留 | `Sphinx/V2/Hosts/Mcp/Config` | `config.mcp.sphinx` 注入 |
| — | `OpenCode/Host/SphinxMcpConfigSurface` | 保留 | `Sphinx/V2/Hosts/Mcp/ConfigSurface` | native 值边界 |
| — | `OpenCode/Plugin/SphinxCommand` | 替换 | `Sphinx/V2/Hosts/PluginCommand` | 同 `RunQuestion` 程序入口 |
| — | `OpenCode/Plugin/SphinxCommandSurface` | 替换 | `Sphinx/V2/Hosts/PluginCommandSurface` | native 值边界 |

上表 54 对 = `Sphinx/` 下 108 个文件；另有 9 对 Host/Plugin 侧接入文件，共 63 对 / 126 文件需要结论。
