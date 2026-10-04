# Host、Provider、验证与发布：剩余 TODO 施工分册

返回[总计划](../TODO施工总计划-2026-10-03.md)；[逐项原始清单](05-逐项清单.md)。本分册的 P0/P1/P2 表示风险优先级；执行波次与跨包前提以总计划为准。

S03 最新接手入口是[运行器因果输送与回收](../archive/2026-10-04/S03运行器因果输送与回收-2026-10-04.md)。第一原子批收录有限 Fable 编译、coverage 异步等待/真实signal/stdin EOF、supervisor 原进程组与HOME/异常收口、工具角色 typed 提前拒绝；定向红绿与统一18文件验收分开，后者待附件。下一独立原子批处理已正式击红的 detached tool 及后代监护；不能由原 PGID/HOME 回收推导完整 descendant 收口。SDK packs/tool FSharp.Core 不在四包图证明范围；VS-016的同候选实际验证、runtime readonly、ABA、T418/T419 与 GAP-055 PARTIAL 保留。

fileWaits 的 active/queued/drained+lastVerdict 诊断已有正式红绿；历史72e83 CI仍仅817/818、无authoritative summary/活动身份，gen103本地仍是静默失败，不能用gen102的398/0替代。新 fe9 CI `37195699694` 已818/818排空、4188/0/102skip/404TODO、exit1 solely pending proof，是另一输入证书，不定位旧物理原因。本批统一18文件结果另由附件绑定；重复fixture准备只按完整行为/断言等价评估，不调workers、不扩大300000/5000、不删测试。

本分册创建时只做只读规划，合计 **64 个编号文件、71 项实际 TODO**，该数是历史截面。仓库根目录为 `/Users/yuanxi/Workwork/vibe-fs`。随后Host就绪与Guard替代批次已完成总计划R01—R04，GAP-223关闭，最新证据见[交付记录](../archive/2026-10-03/Host就绪与Guard替代修复-2026-10-03.md)。VS-016的T418/T419与GAP-055仍PARTIAL，源码/依赖/工具准备不等于实际同候选verify或只读执行；DIST-001/005/007不能假定同候选快照已可用。最新上游取舍见[590同步记录](../archive/2026-10-04/Upstream增量-590a3f69e-2026-10-04.md)。本批不删除安装版executor throw的T180。测试夹具字符串中的`todo`不计入债务，历史数字不是新的全量统计。

本地focused编译新证据：Controller使用SessionHostPort的ChatExecutionKey/ManagedChatAcceptanceWitness时，`host-session-contract`缺`execution-session-chatexecution-facts`真实边；SW012 production-plan正式红0通过/1失败，单边修正后定向2项通过。独立Controller闭包112→132个fs/fsi，fs计数由56→66（此前48→56另有8个source增长），facts shard分组带入10个typed ChatExecution合同及既有支持模块fs，不含具体Host/文件/网络适配器，单边不新授capability；028 recovery反增长ratchet据实66，其余预算未超，WHAT185及禁止实现source断言不变。统一正式构建/受影响套件仍待验收；SDK只调查未实施，S03其余边界保持PARTIAL，详情见590同步记录。

## 使用这份分册

- **P0**：已知可能改变授权、持久事实、容量或验收真假的缺陷；先明确规范，再取得真实反例并闭合。
- **P1**：核心跨包/Host/重启链未证明，已有局部绿色不能替代。
- **P2**：内容、接口、结构或覆盖审计，仍是必要工作，但不宜抢在所有权和证据基础设施之前。
- **规范裁决**是独立状态，不代表可以实现自己偏好的语义。存在冲突时只推进不依赖该冲突的工作。
- 每张卡的“先红”是应补的反例或受控破坏方案，除明确写“已有可执行 TODO”外，不声称当前实现必然失败。若真实入口已经满足合同，只补正式行为证据，不为了产生代码 diff 改生产。
- 每张卡落到该包既有 `NNN.test.mjs`，必要的支持夹具也必须由该正式测试调用。优先复用真实 owner 的窄能力端口；不让 Surface 自己重写一套决策来通过测试。
- 验收同时核对返回值、完整持久事实、物理发送/释放等副作用及无关对象不变。编译负例必须确实调用 Fable，并且由于目标类型边界失败；语法错误、缺依赖、零匹配和 TODO 零退出均不算通过。
- 本分册的路径以仓库根目录为基准；每包链接给出实际目录。共享生产文件必须指定唯一编辑者；其它工作卡先写独立测试或只读审查。运行命令、统一波次和总体验收由主计划统一规定。

## 开工前先纠正的依据

1. `execution-model-routing-011` 的 TODO 标题和 GAP-131 仍沿用“先解析目标再 Accepted”。现行 WHAT 明确 **accept → acquire → project**。禁止照旧标题把生产改回去；先重写该卡的反例与证据说明。
2. `execution-model-routing-004` 的普通 demand 禁止发送/排队时抢容量，与 WHAT-011 明示的 Strength 非等待 reservation 是不同范围。GAP-130/34-D2 的旧结论必须结合用途重判，不能统一删除所有 reservation，也不能让例外覆盖普通请求。
3. `provider-language-008` 的 TODO 仍说 bound session；现行 WHAT-002/008/013 明确无会话语言绑定，下一次渲染读取当前全局语言。`provider-attempt-recovery-013` 仍要求保留 `SessionProviderLanguage`，是跨包文字冲突，须裁决后同步测试标题/规范。保持 Persona/权限不变不等于冻结语言。
4. `provider-attempt-recovery-007` 的“耗尽后新失败”与“持久事实未携带可验证 limit”是两个问题。前者可独立修；后者需要预算事实合同决定，不能在回放时偷读当前配置充当历史预算。
5. `js-semantic-surface-001/006` 分别有 03-D1/03-D2 边界裁决；`distribution-007` 有 GAP-211 归档允许成员文字冲突。保留现有真实物理证据，不通过豁免白名单或删测试消除冲突。
6. `structured-workflow` README/GAP 中部分历史编号说法已过时：当前显式 TODO 只有 003/006/018。退役的恒真/词形测试不应为了“补齐编号”复活。
7. 已闭合的 GAP-081（TOML 原值、换行与 UTF-8 计数）不再安排重做；现行 LF 是布局规则，不是允许给字符串值追加 LF。Host-032 已有大量真实 hook 回归，也不应全部回退成待施工。
8. README 历史运行数字只说明那次范围。Host-032 隔离 canary 曾通过，但完整 integration 曾在取消分支超时；应检查本批最终诊断记录与关联身份，不能用旧成功或再次偶然成功关闭缺口。

## 1. host-boundary：3 文件 / 3 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/host-boundary/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/host-boundary/tests/README.md)。测试目录：[tests](/Users/yuanxi/Workwork/vibe-fs/requirements/host-boundary/tests)。GAP-063/064/217。

生产入口：`src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs`、`PluginTransforms.fs`、`PluginBoot.fs`；Host 层 `PluginTransformSurface.fs`、`HostSignalBootstrap.fs`、`ManagerReviewContract.fs`、`ReadonlyDelegationContract.fs`、`ProtocolArgumentVault.fs`；真实 canary 位于 `requirements/host-boundary/tests/support/run-manager-review-tools-canary.mjs` 及 `manager-review-tools-canary-plugin.mjs`。029 的物理终止能力在 `src/Wanxiangshu/Foundation/FatalProcess.fs`。

### HB-019 · P1 · 全部必需 Host 能力的真实 canary

- **已有/欠缺**：现有生产 transform 接缝覆盖条件分支；023、032 有真实 Host 局部场景。仍未逐项证明 WHAT-019 要求的实际能力，Replica 物理路线不能靠预填 Map。
- **先决项**：从 WHAT 提取必需能力列表并逐项标当前真实 canary；固定受支持 Host 版本、安装路径、隔离配置与本机回环服务。和 HPFO-007 共用兼容基线，和 HB-032 共用事件关联诊断。
- **先红/独立观察**：对每个尚缺的能力，从公开 Hook/SDK 触发最短真实调用；删除/拒绝该能力时必须在准确阶段失败。区分请求未提交、Hook 未收、Hook 拒绝、provider 未收、终态未见；使用真实 session/message/call ID，不用全局步数把 Chronicle 等辅助请求当目标请求。
- **施工**：先修夹具因果关联，再逐项增加 canary；确有 Host 缺能力时单独提兼容决策，不在插件模拟“已经调用”。保持原断言与预算，补诊断不改变产品结果。
- **验收/停止**：能力列表每行连接正式测试、实际观测和失败对照，生命周期正常回收。缺支持版本/端口权限列环境阻塞；存在未解释串线或超时即停，不重跑凑绿、不延长 timeout。

### HB-029 · P1 · 正常拒绝、旧回调和耗尽不得误杀进程

- **已有/欠缺**：真实子进程已证明 fatal 报告、报告器异常、输出关闭及同子进程写入后重开日志。旧三种“正常路径”只打印成功，已删除；本卡专补真实非 fatal owner。
- **先决项**：先解决 JS-006 对物理探针入口的边界，或沿用当前已保留的物理 fixture 而不扩大绕过范围；列出各场景实际 owner，和 EFP-010 的 fatal 清单对应。
- **先红/独立观察**：同一子进程调用真实协议拒绝、过时回调和 provider budget exhaustion；父进程核对返回/typed terminal/资源释放及退出状态。把 fatal 能力注入为记录后真实终止的能力，误走 fatal 必须失败；正常完成后仍需确认无悬挂句柄。
- **施工**：一场景一条真实入口链，不拼接父进程预写事实或独立 traces；如果发现在正常拒绝路径调用 fuse，仅修拥有该决定的模块。
- **验收/停止**：三条路径实际达到其业务终态、未触发 kill、正常退出且事实准确。仅 exit=0、控制台成功文本或未捕获异常导致的非零都不构成证明。

### HB-032 · P1 · 安装版执行器抛错后的自动参数恢复

- **已有/欠缺**：C44–55 已证明参数描述符/键序/对象身份、部分隐藏失败回滚、精确调用 owner、重复 before/after、并发共用对象和业务 getter 不读取；16 个新增真实 hook 回归已有绿色。C11/C12 是显式 after，不能证明 Host 执行器异常必然调用 after。
- **先决项**：先读最新 canary 失败诊断；以真实目标 call 的 chat.message 接收/完成/拒绝、SDK parent/status、provider route 阶段区分丢协议与夹具关联错误。此调查是稳定证据前提，不是扩展产品功能。
- **S02及后续增量**：collector旧idle误关联与请求计数串阶段已修；032正常同物理turn自然followup、业务错误、取消分别核对before的exact身份，原断言在最终完整integration通过。032本身不证明executor throw后的自动after；独立033与R01—R04随后关闭GAP-223，不是在读取hook补租约。T180保持待证。
- **先红/独立观察**：安装版 Host 真正执行一个受控抛 sentinel Error 的参与工具；观察器仅记录，不手调 after。before 已隐藏协议字段，执行器看不到它们；异常后 SDK 保存参数/after 观察应恢复原完整值与顺序。并列正常、业务 Error 返回和真实 throw，不能互换。
- **施工**：先证明 Host 的异常 callback 合同；若 Host 不调用 after，找真实可用 finally/恢复归属并补最小协议实现，或提交 Host 兼容裁决。不得以全局异常 handler 偷恢复其它调用的 stash。
- **验收/停止**：真实异常路线上自动恢复且错误仍可见，同 owner 恢复、foreign owner 保持隐藏；现有 C44–55 全保留。Host 缺此能力时保留明确 TODO/兼容阻塞，不能用手动 after 替代。

## 2. provider-projection：5 文件 / 5 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/provider-projection/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/provider-projection/tests/README.md)。GAP-082。

生产 owner：`src/Wanxiangshu/Participant/Provider/Projection/{Intent,Model,Planner,Renderer,Surface}.fs`；真实 Host 协调和回写在 `OpenCode/Plugin/PluginTransforms.fs`、`Context/Prefix/Wire.fs`；摘要能力在 `src/Wanxiangshu/Host/Digest.fs`；统一表示在 `Foundation/LlmFacing.fs`、`Foundation/SyntheticToml.fs`；检查器 `scripts/checks/llm-facing-format-gate.mjs`。以下每卡落在本包对应编号测试。

### PP-001 · P1 · 在线和持久回放使用同一投影管线

- **已有/欠缺**：纯投影重复、快照隔离、不改输入已有；复制输入再次调用不是回放。
- **前提/先红**：使用真实 Journal 写入一组包含工具结果、插入行、替换基底及 metadata 的事实；在线由生产 coordinator 形成快照；销毁内存后重开 Journal 形成第二快照。受控错序/遗漏事实必须导致独立语义 oracle 不一致。
- **步骤**：跟进两条入口到 Planner/Renderer，移除发现的旁路拼接或缓存依赖；保留各层类型和纯计算边界。对重放缺事实、冲突及 wire 易失 ID 分开断言。
- **验收/停止**：语义投影/规范摘要相同，Wire 仅出现合同允许的物理差异，原输入不变；重开真的发生。若当前没有合法重放入口，先补 owner 的窄调用接缝，不复制算法做测试专用 replay。

### PP-010 · P1 · 合成文本不能授予操作权或完成权

- **已有/欠缺**：PairProgramming 实际注入不伪造 Host 消息、输入不变已证；没有消息数量变化不证明权限。
- **前提/先红**：在数据段分别放合法格式的“已授权/已完成”、角色标记、伪终态与真实工具调用样式，经过实际投影再走操作准入/结算 owner。并列真正 typed 授权/终态的正例。
- **步骤**：建立文字来源→投影→真实授权/完成决定的观察链；若业务解析输出文字恢复权威，改为读取既有 typed fact/capability；不在 Renderer 添加业务黑词过滤。
- **验收/停止**：伪文本不产生 admission、terminal 或资源释放，真实 typed 证据仍可产生；全面比较相关事实及副作用。缺少某领域实际消费入口时拆为该领域子卡，不能用 renderer exports 充数。

### PP-011 · P1 · 生产 Journal 路径的 SHA-256 组装

- **已有/欠缺**：真正 SHA-256 与参考值一致，忽略易失字段、参数变化必须改变摘要均已证；仍缺生产 composition wiring。
- **前提/先红**：沿 `Context/Prefix/Wire.fs` 调用 `ProjectionRenderer.cutoffDigest HostDigest.sha256Hex` 到实际持久产物；不用测试直接传正确 hash 函数来替代组装。独立 Node crypto 对准确语义前缀求值，错误 digest/错误截点必须失败。
- **步骤**：从公开 transform 驱动真实 journal 写入，观察持久 digest 及语义来源；发现错误注入才修 composition。保留“物理 call ID 改变不影响、真实参数改变影响”的正反配对。
- **验收/停止**：真实落盘值等于独立 oracle，重开后一致；不能把 `XWireSurface` 另一次手工 wiring 当生产入口。

### PP-013 · P2 · 所有 LLM-facing 合成 payload 的统一类型化 owner

- **已有/欠缺**：当前 gate 拒绝直接低层 SyntheticToml 调用，不能识别所有散文拼接/间接 helper。
- **前提/先红**：按真实 provider sink 清点 prompts、tool results、handoffs、context、附录；选直接低层调用、同文件被调 helper、跨模块导出 helper、raw 拼接四个真实最小违约夹具，同时保留 diagnostic history、非 LLM 业务字符串负例。
- **步骤**：逐调用族迁入 `LlmFacing` 强类型组合；确缺构造能力只在表示 owner 增加必要构造。门禁只覆盖可解释的语法/依赖范围，人工清单补它不能判定的语义，不搞全仓字符串禁令。
- **验收/停止**：每个 sink 有 owner 路径和真实输出测试；违约夹具被实际门禁拒绝、无关字符串不误伤。动态路径无法可靠判断时记录审阅责任，禁止声称扫描器完备。

### PP-014 · P1 · 每个物理 payload 先组合再 render 一次

- **已有/欠缺**：Join 和 warm-start 已验证后加指令仍在全部数据之前，不能推出其它调用者。
- **前提/先红**：复用 PP-013 的 sink 清单；各族选择“主体先有 Data、附录后来有 Instruction”的例子，含批量工具结果与交接。独立解析完整输出并定位首数据之前全部指令；受控 render(A)+render(B) 必须失败。
- **步骤**：把中间传递的字符串改成该 owner 已有的组合值，到最后物理交付边界才渲染；先处理一个族再扩展，避免泛化全能 AST。
- **验收/停止**：逐 sink 完整字节/解析对象和指令次序正确，没有拼接已经渲染的片段。若只知道最终正确却未核实全部 caller，不关闭全链 TODO。

## 3. provider-language：5 文件 / 5 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/provider-language/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/provider-language/tests/README.md)。GAP-070/071。

真实入口：`src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs`、`ProviderSystemTransform.fs`；`Participant/Provider/{SessionLanguage,Language,ProviderResources,Prose}.fs`；`Resources/ProviderResourceBytes.fs`；工具注册和 schema 经 `OpenCode/Plugin/PluginHooks.fs`。资源策略检查器 `scripts/checks/language-parity-gate.mjs`。不编辑用户私人配置。

### PL-002 · P1 · 真实存活会话下一次请求使用当前全局语言

- **已有/欠缺**：同会话、多派生会话读取全局偏好的 Surface 与 transform 已证；缺实际 Host 发出的请求。
- **前提/先红**：隔离 Host 配置/环境，在同会话首次 English 请求被服务端接收后切 SimplifiedChinese，再触发真实下一次请求；并列 owner/child/internal execution。使用 provider 接收的完整 payload，不能用 registry 值。
- **步骤**：沿配置刷新→system/tool 描述 render→Host wire 查语言缓存；只修实际不再读取当前全局值的 owner。技术字面量和外部材料作为不可变对照。
- **验收/停止**：下一次完整 Class A 是中文，首次证据仍英文，Class B/外部原文不变；没有新增 session language fact。若运行中环境无法合法刷新，先明确真实配置更新入口，不从测试直接改私有状态。

### PL-008 · P1 · 工具描述和调用契约真实交付

- **已有/欠缺**：资源成对和扫描器有证据，不能证明最终 schema 描述；TODO 的 bound session 措辞须改为当前全局。
- **前提/先红**：实际插件注册工具后捕获 provider 收到的 tool schema 和 system；en→zh 同会话切换，覆盖字段说明、调用后果、错误/完成文案与工具名/枚举不翻译。故意留一项旧语言资源必须被完整 oracle 发现。
- **步骤**：清点当前合法工具的 Class A 字段到资源 owner 的调用链；逐项修硬编码或注册时冻结语言，避免把业务参数翻译。
- **验收/停止**：逐工具完整描述与选定资源一致，混用语言被拒绝；只有汉字出现/资源文件存在不算通过。依赖 PL-002 的真实边界夹具。

### PL-009 · P2 · 全部 Class A 统一加载

- **已有/欠缺**：PromptSurface 和语言策略 gate 有局部证据，未穷尽生产调用路径。
- **前提/先红**：复用 PP-013 的最终 sink 清单，标出语义 owner、resource key、参数和值的类属。用缺 en/缺 zh、漏参数、业务中直接语言分支 prose 的正式反例；Class C 诊断允许独立文本。
- **步骤**：从 sink 反向核对加载，逐族迁移统一资源；不把布局、语言决定与内容合并在单一万能模块。检查器记录自己可判定的边界。
- **验收/停止**：每个 Class A sink 有实际加载与完整输出证据、缺失失败而非英文 fallback；未审路径保持待证，不能以几种源码模式全绿作总体结论。

### PL-010 · P2 / 规范裁决 · Semantic Anchor ID 的承载形式

- **已有/欠缺**：尚无已达成一致的锚点表示；关键词相同不表示同一个锚点。
- **前提**：内容 owner 决定 ID 的唯一归属、稳定性/删除规则、资源内承载格式、一个 ID 对应的职责范围；不得从行号、中文翻译或当前段落位置生成临时身份。
- **先红/步骤**：形式明确后给 paired/missing/duplicate/mismatched ID、顺序变化但对应未变的正反样本；让同一资源加载/发布检查读取真实锚点；只迁移正式 Role Law，不修改技术字面量。
- **验收/停止**：全部现存正式锚点一对一，增加/删除有可审记录；此项只证明对应关系。语义是否忠实仍由 PL-012 审阅，裁决前不设计自动翻译评分冒充语义验收。

### PL-012 · P2 · 当前职责的双语语义审阅

- **已有/欠缺**：肯定/否定短语反例只能证明现有检查器；Sphinx 内部 Engineer 按当前职责，不能沿用旧角色特例。
- **前提/先红**：以 office-capability、delegation 当前 WHAT 建“职责命题→en片段→zh片段→示例/工具说明”矩阵；植入一次权限扩大、一次义务减弱、一次错误委托对象，要求审阅明确指出差异。
- **步骤**：逐角色逐资源人工对读，记录具体命题与判断，不统计关键词；已发现的精确矛盾可补自动回归，但不宣称机器已证明任意双语等价。
- **验收/停止**：所有矩阵行有可复核结论、修改后两语言与例子同改，技术标识保持一致；存在职责规范冲突先交相应 owner 裁决。自动测试和人工语义证据分开报告。

## 4. host-provider-failure-ownership：6 文件 / 7 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/host-provider-failure-ownership/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/host-provider-failure-ownership/tests/README.md)。GAP-143/144。

入口：真实 Host canary 在本包 `tests/support`；配置 hook 在 `OpenCode/Plugin/PluginHooks.fs`；事件边界 `OpenCode/Host/HostSignalBootstrap.fs`；`OpenCode/Host/ProviderFailurePresentation.fs` 目前纯分类器不能代表 UI 已接线；恢复 owner 在 `Participant/Provider/Attempt/Fallback/Workflow.fs`；权限 owner 在 `Execution/Failure/{Model,Decision,Policy}.fs`。静态启发式 `scripts/checks/retry-owner.mjs` 只能作辅助。

### HPFO-002 · P0 / 兼容裁决 · 一个 physical run 一次上游请求（2 TODO）

- **已有/欠缺**：真实 OpenCode/plugin 1.18.29、配置 0、一个 assistant run 却出现两次 provider request 的可执行 TODO 已红；仅观察插件不是完整万象术恢复链。
- **前提**：分开审计 Host chat retry consumer 和底层 SDK/transport retry；依靠公开版本源码、实际入口和受控 503 计数判断，不能篡改用户配置或私有安装。版本变化须 HPFO-007 重新审计。
- **先红 A**：保留当前实际请求总数、固定会话标题去除辅助请求、不按 run 去重；继续使用真实一次失败场景。**先红 B**：加载完整插件，策略允许一个新 run，再耗尽；将每个上游请求关联到精确 run 和 opaque licence，无 licence 的额外请求立即失败。
- **步骤**：先给产品裁决：能否在支持的公开 Host 能力关闭 SDK 重试，或需要受支持 Host 修复/兼容基线变化；确认后最小 wiring。插件恢复必须等确切旧 run 停止，不能用提高预算或改失败计数容纳 Host 重复。
- **验收/停止**：A 每 run 一次；B 每次新发送都有唯一新 run 与许可，耗尽后零发送。支持 Host 根本不提供能力时保留阻塞，不把配置写成 0 当成请求行为已达标。

### HPFO-003 · P1 · 恢复期间无插件额外 final，Host 原生错误保留

- **已有/欠缺**：纯 Presentation 分类器没有生产接线证明；server event hook 无权拦截已发布的原生 UI 错误。
- **前提/先红**：通过完整插件发生可恢复失败，捕获独立 Host 公开事件/UI 路径与插件自有 presentation；duplicate callbacks、一次恢复成功、最终耗尽三条轨迹。受控提前 final 必须失败。
- **步骤**：先确认实际提示 owner，再把分类决定接到拥有最终失败的生命周期；不加全局 suppression，也不声称能阻止 Desktop/CLI 原生提示。
- **验收/停止**：恢复期间插件 final=0，原生错误可见，耗尽 final 归 HPFO-006；只测分类器或控制台字符串不足。没有可观测 UI 接缝先做兼容/人工边界说明。

### HPFO-004 · P1 · 未认领错误完整可见

- **已有/欠缺**：配置入口已返回旧角色配置错误；permission/tool/cancel/unknown 的实际 UI 仍缺。
- **前提/先红**：隔离 Host 中分别触发配置/schema、permission、工具合同、filesystem/Git、用户取消、未知错误及无恢复计划；每类保留准确错误和归属，不能都构造成 provider 503。
- **步骤**：从真实错误产生点到公开事件/SDK/CLI或Desktop观察链检查，修复吞错的边界；仅 provider recovery 认领它合同允许的错误。
- **验收/停止**：各类原生错误可见、没有越权 retry、业务事实未被伪造成功；UI不可自动观察时保留有步骤与原始证据的人工验收，不把 event 到达等同 UI可见。

### HPFO-005 · P0 · observer/Orchestrator 无权自行恢复

- **已有/欠缺**：retry-owner 源码启发式不能证明实际调用者只经许可。
- **前提/先红**：由真实 Host observer 与 Change Orchestrator 各输入 coarse session.error、未知类、重放/重复失败、错误 attempt licence；独立发送端和 ledger 计数，要求零无证发送。正例使用 EFP owner 真正产生的许可。
- **步骤**：清点所有 retry/resume 的物理 sink 反向调用链；去除第二决定者/第二 writer，使它们只传证据到单一 policy owner。共用 PAR-019 的 exact licence 反例。
- **验收/停止**：每条生产 sender 都有许可来源和一次消费证明；删掉 checker 某个关键词不能让真实负例失效。若还有未覆盖调用者，不关闭“全部”。

### HPFO-006 · P0 · 耗尽终态、停止准入、最终呈现恰一次

- **已有/欠缺**：分类器的 Final 值不证明 durable terminal 或 UI once。
- **前提/先红**：真实工作流分别耗尽 domain budget 和全部 target capacity；terminal append 前/后 crash、Unknown/NotCommitted、重复/迟到 callback、重开 Journal。记录终态事实、后续 provider admission、插件 presentation 三类独立证据。
- **步骤**：按实际持久 receipt 串行 terminalize→停止 admission→合法呈现；只在 owner 修去重与恢复，不让呈现文本生成终态。依赖 EFP-006/007 和 PAR-005。
- **验收/停止**：已提交唯一 exceptional terminal，之后 admission=0，自有 final 恰一次；持久化未知不提前宣称完成。如果跨重启“已呈现”没有可证明的协议，先裁决 exactly-once 的持久证据，不用内存 bool 冒充。

### HPFO-007 · P1 · Host 兼容性 consumer 与呈现链漂移门禁

- **已有/欠缺**：依赖精确版本字符串只约束声明，没核实真正安装执行者和 consumer。
- **前提/先红**：给受支持安装取版本、包身份和公开消费位置；受控替换 consumer 字段、重试 owner、session.error producer/SDK/CLI呈现契约时门禁必须明确失败；合法内容重排不应被无关行号锁定误伤。
- **步骤**：把可核验源/包指纹与实际行为 canary 分层；版本漂移先拒绝并重新审计链，不自动更新 expected。核实运行的是声明版本而非 PATH 上另一副本。
- **验收/停止**：从配置到请求次数、error 到公开 client 的完整对应证据；版本相同但 owner 改变也被识别。网络/源码不可得时报告环境证据缺口，不跳过成绿。

## 5. provider-attempt-recovery：19 文件 / 21 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/provider-attempt-recovery/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/provider-attempt-recovery/tests/README.md)。GAP-139/140/141。多数现有用例证明 fold、planner、Surface 或单段 owner，下面要求真正串联，不全部重写。

共享真实 owner：`src/Wanxiangshu/Participant/Provider/Attempt/Fallback/{Workflow,Retry,Ledger,ProviderFailureFactFold,Facts}.fs`；`Participant/Provider/Attempt/{Planner,RequestKind,TerminalValidity,FailureBudget}.fs`；`Composition/Durable/ProviderFailureFact.fs`；`OpenCode/Plugin/PluginRecoveryWiring.fs`；`OpenCode/Host/{ProviderAttemptStopFence,HostSignalBootstrap,SessionRecoveryHost,LoadRecoverySurface}.fs`；`Composition/Turn/{OrdinaryTurnWorkflow,TurnReconcile}.fs`；Blogger 为 `Context/Companion/Blogger/Runtime/{Coordinator,Abandon,Host}.fs` 和 `Context/Companion/HostBlogger.fs`。同一 `Workflow.fs` 只由一名施工者编辑。

### PAR-005 · P0 · 真正恢复到耗尽，不多发一次

- **已有**：预算/重试引擎局部证据；缺完整失败→许可→停机观察→新发送链。
- **前提/先红**：从真实 root/Accepted/ProviderStarted 建立 run，按预算逐次给 exact failure + Host terminal；最后一个合法失败之后继续投递 duplicate/idle/late completion。独立记录 SDK send 和持久事实。
- **步骤**：沿 Workflow 验证每次许可和新物理身份，修 owner 的边界计数；不要在测试中先计算许可再直接调用发送冒充完整 policy。
- **验收/停止**：发送次数与合法新 run 精确对应，耗尽后 send=0，重开仍耗尽；不以 Host transport attempt number 作为 domain count。依赖 HPFO-002 明确物理单次请求能力。

### PAR-006 · P1 · 更换模型目标仍保持参与者与完整提示词

- **已有**：PlannerSurface 计划相等；缺实际 Host submission。
- **先红**：初始 target A 失败后合法路由 B，捕获真实发送的 participant 身份、完整 system prompt、Authority 与 provider horizon；用“目标改变错误带入另角色模板”的受控对照。
- **步骤**：由 policy licence 驱动 Workflow，走真实 model routing 与 Host submit；若身份由目标选择推导，改为从 durable identity 读取。与 PAR-013 共享身份 fixture，语言字段待跨包裁决。
- **验收/停止**：只改变允许的 executor 目标/物理 ID，角色/Persona/权限/完整提示词符合当前语言合同；没有早越 exhaustion 或附加发送。

### PAR-007 · P0 / 部分规范裁决 · 非法预算日志拒绝回放（2 TODO）

- **已有可红**：新 failure 在 exhausted 后被吸收为 AlreadyExhausted；超过 limit 的 successor 也因日志缺 limit 证据而被接受。
- **步骤 A**：保留同一事实重复的幂等正例；追加不同身份的新 failure，要求实际 Journal replay 在该项停止、后续事实不应用。只修 `ProviderFailureFactFold` 的耗尽后新事实分支。
- **步骤 B 前提**：决定预算上限属于哪个 durable root/预算事实、旧日志迁移/拒绝语义、limit 何时固定。不得回放时读取新的运行配置补历史值。
- **步骤 B 先红**：limit=N 下合法 N、非法 N+1、计数跳跃、重启配置改变、旧 schema 日志，分别检查实际 decoder+fold。决定后更新写方、解码、回放和正式用例，再迁移 fixture。
- **验收/停止**：非法事实中止 replay，幂等重复仍合法；A 可单独闭合，B 未有规范证据继续阻塞。禁止放宽 WHAT 或把不合法日志静默截断成成功。

### PAR-008 · P1 · 无用内容走一次 repair，provider failure 不降级

- **已有**：content 判定与 reconciliation 部分真实边界已证，errored empty 在 idle 后等待 exact failure 的回归已补。
- **先红**：真实 Manager 与另一 repairable role 收到空/XML-only 非 error terminal，重复 idle/terminal 后只有一次 repair，预算不变；errored empty 配对轨迹在 exact failure 前零 repair、之后走 licensed recovery。
- **步骤**：从 Host observation 进入 `TurnReconcile`/`Interaction/Repair/CompletedTurn`，不用直接调用分类器。修未完成 turn 被忽略或 duplicate repair 的真实 owner。
- **验收/停止**：完整 repair 次数、预算事实、turn 结算和发送一致；不重复已闭合的 errored-empty 子用例，扩展缺失角色/完整链即可。

### PAR-009 · P1 · Host transport retry 数不进入领域预算

- **已有**：纯 identity/count 映射；没有真实 Host observation→policy→ledger。
- **先红**：对同一 exact failure 给 transport attempts 0/1/较大值，配对一个不同 ProviderRunIdentity；实际 domain journal 应对同一 run 只推进一次，不同合法 run 按许可推进。
- **步骤**：真实 decoder 经 failure owner，捕获物理 licence 与事实；若 adapter 把 Host attempt 用作预算/身份，切断该来源，保留其 diagnostics 价值。
- **验收/停止**：transport 数变化不改 run identity、预算或发送权限；不能通过把输出字段删掉但仍内部参与路由来过关。依赖 HPFO-002 的真实请求观测。

### PAR-010 · P1 · Blogger 维护重试顺序与预算

- **已有**：纯压缩/maintenance 决策；缺实际 licensed send。
- **先红**：durable frame 有/无 × squash 成功/失败；合法 BloggerMain failure 后有材料先 Squash 再 Main，无材料直接 Main；Squash 成功不能写 SuccessRecorded 清预算，失败须下一许可。
- **步骤**：以真实 `Workflow.fs`→BloggerCoordinator→Host 发送记录顺序；使用独立 committed frame，不手设进程内 flight 充材料；和 PAR-017 共用旧材料结清证据。
- **验收/停止**：完整 RequestKind、PromptKey、send 顺序和 failure/success facts 精确；任何无许可第二次发送失败。不是只断言 nextRequest 字符串。

### PAR-011 · P1 · 渲染只观察本次不可变 AttemptPlan

- **已有**：freeze/bind、same physical 重复、冲突 plan 拒绝、终态消费及别的 physical 不消费均有局部真实证据。
- **先红**：先准入冻结 plan A，在 render 前改变外部 transient 状态/产生另一 run 许可；重复 render A 并交错 B；捕获实际 Host payload 和 ledger，A 的 probe/maintenance 不漂移，不消费许可。
- **步骤**：从真实 admission 到 `Context/Prefix/Wire.fs`/PluginTransforms，再到 payload；把发现的跨回调 permission 或 render 时决策挪回 plan owner，不在 renderer 恢复业务调度。
- **验收/停止**：plan 与物理消息 exact binding，重复渲染同字节且预算/发送权限无副作用；不能用测试直接供应 plan 后仅调用 pure renderer 代替准入链。

### PAR-012 · P1 · Host abort 残留不花失败预算

- **已有**：interrupted tool 分类局部测试。
- **先红**：真实 Host tool 正在执行时 abort/cleanup，收到 interrupted residue 后进入 reconciliation；同场景另给确切 provider error 作对照。读取重开后的 budget journal，不只读取内存计数。
- **步骤**：确定 cleanup 信号的真实入口，区分取消控制面与 provider confirmed failure；修错误分类所有者，不能按错误文案过滤。
- **验收/停止**：单纯 residue 没有 failure fact、licence 或 retry send，真实 error 仍计数；工具/turn 其它清理仍完成。依赖 Host 能力和受控取消夹具。

### PAR-013 · P1 / 规范裁决 · 重试/重启保留完整身份来源

- **已有**：PlannerSurface 局部身份字段；缺完整 durable provenance 和 Host horizon。
- **前提**：先解决 `SessionProviderLanguage` 与 PL-002 无会话绑定冲突；其它 identity 不必等语言裁决才调查。
- **先红/步骤**：从真实持久 Persona/version/provenance、Role/CanonicalRole/Authority 建 run，换 executor、重开进程、恢复 admission；独立核对 journal 和完整 provider payload。篡改/缺失 durable identity 时必须拒绝，不能读取当前用户 Persona 代补。
- **验收/停止**：角色/Persona/来源/权限完全来自该 run 证据，机器记账不进 horizon；语言按裁决后的当前合同。未决定语言前不能将完整 TODO 去掉或冻结旧值。

### PAR-014 · P0 · failure 许可与 exact Host stop 缺一不得续发

- **已有**：retry engine 局部 admission；发送前置栅栏真实串联未证。
- **先红**：confirmed failure 先到但 exact finalized errored assistant 未到，持续送 coarse session.error、idle、其它 run terminal，真实 send=0；精确终态到达才一次发送，重复终态不多发。
- **步骤**：走 HostSignalBootstrap→failure owner→ProviderAttemptStopFence→Workflow；保留 event-driven await，不加 timer/poll。发送动作不追加/清空 budget fact。
- **验收/停止**：许可、Host stop 和实际发送可关联同一 run，顺序反转同样合法但不越权；与 PAR-022 共用真实链，分别断言发送/预算和 stop capability 生命周期。

### PAR-015 · P1 · speculative 分支不碰 owner budget

- **已有**：局部重试引擎排除 Replica；缺 owner Journal 的实际隔离。
- **先红**：真实 StrengthReplica 成功、失败、取消、晚到回调，owner 预置非零连续失败；再给 owner 本身合法成功/失败对照。
- **步骤**：从 actual speculative 完成入口经过 RequestKind 证据和账本，独立重开 owner/branch journal；修身份或 owner 归属错误，不用角色名字符串猜请求类型。
- **验收/停止**：Replica 不增加也不清空 owner failure count，不发 owner recovery；owner 合法事件仍生效。涉及 Strength owner 先协调编辑，不改通用 budget 容忍错归属。

### PAR-016 · P1 · 成功记账必须有 durable RequestKind

- **已有**：eligible predicate 已证；未覆盖真正 ordinary 与 Blogger completion 写入。
- **先红**：WorkMain/BloggerMain/tool-calls success 写一次 SuccessRecorded；Squash/InteractionRepair/Replica 不写；只有角色/文本、自称 Main 但无 accepted continuation/cycle receipt 的负例不得清预算。
- **步骤**：真实 Host terminal→ordinary/Blogger owner→账本；以持久 request/cycle/continuation 为依据，补 duplicate 与重启重复 observation。
- **验收/停止**：完整事实内容及预算 fold 正确，eligible 仅恰一次；不能先在测试给对 RequestKind 再调用写账函数冒充类型来源证明。

### PAR-017 · P1 · 旧 Blogger 材料先弃置，再创建新物理所有权

- **已有**：局部 request material owner；缺三个完整替换方向。
- **先红**：Main→Main、Main→Squash、Squash→Main 各在旧 abandonment append 前/后阻断；旧材料未 committed abandon 时新 materialize/bind/send 必须为零。旧 PromptKey 不能绑定重试。
- **步骤**：真实 `replaceFailedBloggerRequest` 与 Coordinator，观察同一 journal 的序号与 Host send；新建 agent-free PromptKey，错误后按已有 owner 清理，禁止先发再补旧事实。
- **验收/停止**：严格 durable abandon→fresh materialize→fresh key binding→send，每方向 crash/replay 不重复；只比较两个 key 不等或测试手拼事件顺序不足。

### PAR-018 · P1 · 只等 durable producer 进展（2 TODO）

- **已有**：TODO 当前尚无完整工作流测试；生产 `Workflow.awaitRecoveryMaterial` 已用 `AgentJournal.awaitChangeFromOrCancel`，不能因存在函数就闭合。
- **先红 A**：关联 Blogger 有 durable open request、无更新覆盖，WorkMain recovery pending；提交无关事实和旧/等高 coverage 仍 pending；真正 close/abandon 或 strictly newer coverage 才继续。
- **先红 B**：没有 durable open producer 立即继续；已有等待时取消必须释放 waiter，随后 committed fact 不再发送。使用受控 append/await barrier，不靠 sleep 窗口猜 pending。
- **步骤**：真实 open request 和 prefix coverage 从 journal 加载，调用 actual recovery owner；独立观察 send/等待注册释放。若因果订阅漏唤醒，修对应 owner，不能把内存 flight 当 durable open。
- **验收/停止**：两 TODO 各有正反例、无轮询/超时授权；在发生 append 与订阅交错的边界仍不丢 committed event。缺事件屏障接缝先补窄端口，不扩大等待预算。

### PAR-019 · P0 · 所有生产 caller 必须持有 exact typed licence

- **已有**：实际 retry engine 和静态 checker 一部分；“全部 caller”未证明。
- **先红**：错 run、错 RequestKind、旧 policy decision、重复 licence、未经许可的 LocalInvariant/取消/Unknown 各经真实调用者进入 ledger；正例必须从 policy 得到 opaque licence。
- **步骤**：从每个 retry/resume sender 反向建立有限调用清单，覆盖 ordinary、sync delegate、Blogger、load recovery、observer/Orchestrator；缩窄开放写口和旁路，ledger 只验 exact identity，不重算政策。
- **验收/停止**：非法输入零 budget mutation/零 send，合法一次；编译不透明性由 EFP-003 补。没遍历到的生产 caller 必须列出，不能以词形 gate 通过宣称完成。

### PAR-020 · P0 · 回放预算不是恢复许可

- **已有**：fold 计数相同不证明重新加载不发请求。
- **先红**：真实进程 A 写合法 budget/failure facts 后退出；进程 B 只重开 Journal，没有新 process-local exact licence/stop evidence；捕获所有 retry/resume/SDK calls 必须为零。另由当前进程合法失败建立许可作正例。
- **步骤**：经 PluginRecoveryWiring/LoadRecovery 真正 boot，审计 fold 输出是否带 callback/next action；恢复仅重新证明义务再走普通 policy，不从 count 生成指令。
- **验收/停止**：重放视图确定、无私自发送；Accepted-without-start 另按 PAR-023 exact resume/terminal 决定，不能为了本卡静默遗弃它。

### PAR-021 · P0 · LWR exact 证据和 target settlement 一起生效

- **已有**：实际 routing retain/condemn 与 durable 查询有局部证据，部分测试自己选择决策，缺 policy→settlement→send 串联。
- **先红**：ordinary 与 sync delegate 两条链：首次失败 retain exact target；只有同 physical message 的 ProviderRetryAttempt Accepted + 同 run ProviderStarted 再失败才 poison。缺任一事实、同消息后续 step、旧回调、取消、append Unknown 都不得 poison。
- **步骤**：取得 licence 后一起读 durable LWR 与失败 target，单次消费 provider-run witness，再发送；让 test 观察真实決策而非传入 retain/condemn。
- **验收/停止**：provider 健康、单次 preference、容量和发送顺序完整正确；重启重入同规则。与 EMR/EFP owner 协调，不新建并行故障策略。

### PAR-022 · P0 · Host stop capability 严格进程内、exact run、可撤销

- **已有**：StopFenceSurface exact 等待/撤销已测；缺真实 Host event 及新进程的组合。
- **先红**：coarse error、idle、另 session 同 run、同 session 另 run 都不释放；exact finalized errored assistant 释放。abort/replace/delete 后旧终态永久无效。进程重启后旧日志不能继承许可。
- **步骤**：实际 Host decoder 与 ProviderAttemptStopFence 连通，boot 新进程单独观察 sends；检查缺 ProviderStarted 但合法 Host finalized error 的分支仍可被观察。
- **验收/停止**：只有精确当前进程观测有能力，撤销不复活；不持久化 stop fence、不用 idle 补证。与 PAR-014 共夹具但保留各自主断言。

### PAR-023 · P0 · boot sweep 定夺 Accepted 未启动

- **已有**：session.idle 选择形状和缺 resume capability 终态化已有实际 Surface 回归；README/GAP 的“仍悬挂”描述须复核更新，不能重复当已知生产 bug。
- **先红**：进程 A commit Accepted 后、ProviderStarted 前退出；B 真实 boot sweep，混入 Started/terminal/其它 session 控制组。有 typed resume capability 只能恢复原 accepted material；没有则 typed terminal + 失败报告。禁止新文本/替换 PromptClaim。
- **步骤**：接 `PluginBoot/PluginRecoveryWiring` 的真实启动恢复，读取同一 durable store；验证一次 sweep/重复 load、resume失败及 append Unknown 的拥有者边界。
- **验收/停止**：只处置目标形状，exact material/identity 不变、无 replacement send；不能以手动调用 Surface 一次等同真实重启。

## 6. execution-failure-policy：8 文件 / 9 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/execution-failure-policy/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/execution-failure-policy/tests/README.md)。GAP-120/121。

owner：`src/Wanxiangshu/Execution/Failure/{Model,Decision,Policy}.fs/.fsi`；`Execution/Session/ChatExecution/{RecoveryRuntime,Acceptance,Settlement}.fs`；`OpenCode/Host/ChatAdmission/{Transaction,ProviderLifecycle}.fs`；`OpenCode/Host/{ModelRouting,HostSignalBootstrap}.fs`；`scripts/checks/fatal-inventory-gate.mjs`，库存是证明索引，不是运行证明。

### EFP-003 · P0 · sealed authorization 和真实 emission 去重

- **已有**：decision/RecoveryRuntimeSurface 有局部 runtime 样本；缺外部调用者编译拒绝与真实发送去重。
- **先红**：合法外部 F# consumer 能消费 owner 发的 licence，直接构造/改写 licence 不能通过 Fable；真实同 identity licence 两次交给 ledger，只一次物理 send，wrong attempt 零发送。
- **步骤**：先 `.fsi` 封闭构造权，再检查真实 ledger 持有与消费边界；为 emission 在提交前/后失败设置受控结果，不在 test 自己 Set 去重。
- **验收/停止**：编译负例由于 opacity，runtime 去重来自 owner；未知物理接受按 EFP-007 保持未决，不能盲目再发。

### EFP-004 · P0 · opaque exact fence 和真实错误释放拒绝

- **已有**：ModelRoutingSurface 的 runtime wrong identity 多数已证；policy 自造 JS 输入不能证明构造权。
- **先红/步骤**：与 EMR-012 共用合法/伪造 Fable consumer，但本文件独立验证真实 release owner：错 target、错 execution、错 generation、foreign custody、已消费 fence 均不释放；正确 fence 一次释放。
- **验收**：运行容量 multiset 与归属完整比较，未释放其它执行；编译失败必须不是缺模块。先协调公共 fence 类型，不能两 agent 同改 fsi。

### EFP-005 · P0 · terminal owner 检查 exact key 和 phase

- **已有**：typed decision 输出不是实际 terminal 写入。
- **先红**：建两个真实 execution，对 A 的命令带 B key、pre-admission 命令给 running、running terminal 给未接纳；实际 journal/store 和容量都保持原样。
- **步骤**：从 `ChatExecution.Settlement`/真实终态入口驱动，不直接断言 decision 字段；把 guard 放在 owner 物理写入前，保留正确 terminal 正例和 duplicate 幂等。
- **验收/停止**：错误 key/cross phase 零事实/零释放，正确 exactly once；规范没定义的 phase 组合先裁决，不默默映射为 generic failure。

### EFP-006 · P0 · terminal append receipt 先于 release/fatal（2 TODO）

- **已有**：pre-provider transaction 正常 terminal→release 有实际证据；两个异常/物理闭环缺。
- **先红 A**：真实 append 注入 NotCommitted 和 Unknown，exact fence 都不得 release；Committed 才按合法决定处理。不要只传 Policy 的模拟 persistenceFailure。
- **先红 B**：同一个 child 经真实 terminal owner，写入可重开的事实后调用真实 fuse；父进程独立重开确认 settlement，核对报告/kill once；append失败时禁止拼接另一 child 的成功记录。
- **步骤**：给 append receipt 薄接缝，修 terminal→释放的顺序；fatal 注入要保留真实物理退出观察。与 HB-029/EMR-016 一起核对 positive fatal/negative nonfatal。
- **验收/停止**：完整事件序列与进程结局来自同一执行；Unknown 留证而非完成/重发。无法区分 committed receipt 时先修存储合同，不添加猜测兜底。

### EFP-007 · P0 · 接受/持久化未知在重启后仍不重复副作用

- **已有**：decoder/policy 的 Unknown 值没有证明物理链。
- **先红**：真实 Host send 已接受但响应丢失、append 已写但 receipt 丢失两类；进程退出再重开，未完成 reconcile 前 send/release 都不重复。之后公开 Host/持久证据解决未知，允许的后续只一次。
- **步骤**：在真实 accept/send/append 边界提供受控“结果未知”，由 owner 持久证据与 Host 查询协调；不把异常统一转成 definite rejection。
- **验收/停止**：唯一物理消息与容量事实完整，不因重启清空 Unknown；没有可靠查询时保留未决，不超时自动推断失败。

### EFP-008 · P1 · policy/interpreter 不靠时间授予恢复

- **已有**：JS 输入多带 clock 字段被忽略只能说明对象解码。
- **先红**：真实 policy/interpreter 依赖编译边界中引入 clock/poll/sleep authority 的受控 consumer 应被实际能力边界拒绝；运行时在没有新合法事实时推进虚拟时间，零 retry/settlement；提交合法事实才进展。
- **步骤**：审阅真实公开端口和调用图，删除多余时间能力；如仅诊断用时间，明确其不可回流决定，不能全仓禁 Date 字符串。
- **验收/停止**：类型依赖和行为两层证据互补；没有新的事实便没有新的许可。扫描器只声明已覆盖的调用形式。

### EFP-010 · P1 · 每个保留 fuse 都有本阶段实际反例

- **已有**：库存/文件存在、部分一跳 alias 扫描只保证可达索引；已退役 fuse 不复活。
- **前提/先红**：从当前库存逐项连到真正触发条件、phase、exact identity、正式测试；每项注入对应 local invariant 违约，并有近邻合法/正常拒绝负例。尤其不能拿一个 generic fatal child 覆盖所有 branches。
- **步骤**：复用真实 owner 接缝，补缺项后更新库存证据路径；死入口确认无产品义务后删除，不为凑数保留。Source 可达性检查与真实物理退出分开。
- **验收/停止**：库存每行有可运行证明且目标分支确实到达，误杀正常拒绝会红；单纯文件名/断言存在不关闭。

### EFP-013 · P1 · repair、catch-up、fuse、Unknown 生命周期分离

- **已有**：纯 failure phase 矩阵，不是 actual transform/reconcile/settlement。
- **先红**：实际 transform 中合法 repair、recovery catch-up、LocalInvariant、AcceptanceUnknown 四条轨迹交错；其中一种完成/取消不得清除其它未决状态，不得获得对方 licence。
- **步骤**：由 PluginTransforms/TurnReconcile 和真实 settlement 入口推动，读取各 owner 的事实与副作用；若共用字段混淆生命周期，按已有领域类型拆开，不造万能 failure state。
- **验收/停止**：每轨迹结果/次数/权限符合主导 WHAT，近似输入不能互相冒充；必要的多轴语义不明先停裁决。

## 7. execution-model-routing：6 文件 / 8 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/execution-model-routing/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/execution-model-routing/tests/README.md)。GAP-128/129/130/131。

owner：`src/Wanxiangshu/OpenCode/Host/ModelRouting.fs/.fsi`、`ModelCapacity/{Model,Ledger,Queue,Borrowing}.fs`；准入 `OpenCode/Host/ChatAdmission/Transaction.fs`、`OpenCode/Plugin/PluginHooks.fs`；durable accepted/terminal 在 `Execution/Session/ChatExecution` 与 `Composition/Durable/ChatExecutionJournal.fs`；真实 road 绑定来源经 `Mission/Relay` 和 session boot 恢复。禁止把 SessionID 当 exact execution fence。

### EMR-004 · P0 / 规范裁决 · required demand 与 optional reservation 的边界

- **已有可红**：optional reservation 在 chat.message 前占 token 的 executable TODO；当前 WHAT 又明确保留 Strength reservation 的专门边界。
- **前提**：对该 TODO 实际用途分类：ordinary send/queue、Strength 非等待 reservation、已接受执行 lender adoption。先按现行跨包 WHAT 决定哪项真的违约，不复用旧标题作结论。
- **先红/步骤**：ordinary 尚无 chat.message 时 running 不增；required demand 在真实 chat.message 才出现；null 保留 Accepted pending 且零 ProviderStarted；合法 Strength reservation 和归还也要有正例。只修错误路径抢占，保留借用 token 单计数。
- **验收/停止**：每用途 multiset、pending、取消与 ProviderStarted 明确；裁决未完成时不删所有 reservation 或加宽普通准入。与 EMR-011 串行实施。

### EMR-007 · P0 · durable terminal 后才能关闭物理 execution

- **已有**：释放 runtime 与信号 decoder 分开测，未串联真正 Host event。
- **先红**：真实 accepted+started execution，先给 idle/business completion/其它 run terminal 都不释放 binding；exact terminal append NotCommitted/Unknown 也不释放；committed terminal 后一次释放。
- **步骤**：HostSignalBootstrap→settlement→ModelRouting，保持 exact fence；观察 journal 序号和真实 capacity，不在 test 直接调用 Release 伪造 event 行为。
- **验收/停止**：只有已持久终态闭合正确 execution，duplicate late callback 不释放下一代；依赖 EFP-006，不能把成功业务输出当物理终止证据。

### EMR-011 · P0 · accept → acquire → project 的真实准入顺序

- **依据修正**：原标题 target resolution 在 Accepted 前已不符 WHAT；本卡先改标题/README/GAP 引用，禁止改生产去迎合旧测试。
- **先红**：真实 chat.message 中观察 durable Accepted→scheduler/容量取得→exact binding→Host projection/ProviderStarted；Accepted失败时后续全零，null 时保持 Accepted 等待，取消/queue满按 typed结果收敛；projection失败不能偷释放别人的 token。
- **步骤**：从 PluginHooks 进入 Transaction 与 ModelRouting，用受控 append/scheduler/Host 端口记录事实边界；测试顺序来自 WHAT 而非要求某个 helper 名先调用。
- **验收/停止**：全部 failure cut 都证明后续零副作用及自身合法结算，运行中没有未接纳就抢槽；Strength专门路径另按 EMR-004 决定，不将例外套到 ordinary。

### EMR-012 · P0 · exact opaque fence 的编译拒绝

- **已有**：runtime 伪造、stale、foreign custody、字段差异的真实失败对照已充分，当前 TODO 是 F# 类型层。
- **先红/步骤**：外部 Fable consumer 分别尝试 record 构造、复制字段、包装别人的 custody，必须因公开签名不允许而失败；合法 owner 创建→传递→释放 consumer 必须可编译运行。与 EFP-004 共 fixture，保留本条主命题。
- **验收/停止**：运行确实启动 Fable，负例诊断绑定目标类型位置；缺工程依赖或 syntax error 不能算 opaque。不能为了让测试触达私有字段扩大 Surface 权限。

### EMR-016 · P0 · routing fatal 的前置核验与 once（2 TODO）

- **先红 A**：经实际 routing fatal 入口给 missing settlement、stale fence、coarse identity；注入 fuse recorder，必须零 kill/report、capacity 不变。
- **先红 B**：合法 exact settled incident 重复触发，真实能力报告和终止各一次；report失败仍遵守既有物理终止合同。和 HB-029/EFP-006 的同子进程证据共用而不拼接。
- **步骤**：核对 ModelRouting fatal owner 的输入类型和校验次序，把精确核验放在物理能力调用前；不以测试直接调用 FatalProcess 代替 routing。
- **验收/停止**：无证据永不触 fuse，合法事件 once 且不改无关容量；如果规范未明确非法 incident 应怎样报告，保留拒绝事实但不自行加全局 kill。

### EMR-019 · P0 / 历史裁决复核 · DevOps road target 不被新 scheduler 改写（2 TODO）

- **已有可红**：`ModelRouting.BindDevopsTarget` 在旧目标 unavailable 时允许更换的 TODO；seed 的重复/矛盾及 readonly-delegate 隔离已有局部证据。当前 WHAT 已明确固定 owner，不代表Replica继承。
- **先红 A**：road 初始化持久 target A，换 scheduler 偏好 B/令 A暂不可用后 resume，要求不可覆盖 A；readonly-delegate 新 execution 仍由 Predictor调度，不继承 owner绑定。
- **先红 B**：真实进程关闭再从 road projection恢复，物理 session替换后 exact target仍A；移除durable binding不能悄悄回退当前scheduler。
- **步骤**：确认 road durable source→session seed→normal admission 唯一路径；只修 owner binding可变口，保留模型健康和容量等待语义。同步过时GAP129裁决状态，不凭旧34-D1标记拖延已明确合同。
- **验收/停止**：A不可用时不换B、不重复占容量；重启/替换继承A，Replica独立；若road数据缺恢复身份，先补事实合同，不能用内存Map当持久证明。

## 8. verification-system：1 文件 / 2 TODO，加不以 TODO 计数的审阅债

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/verification-system/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/verification-system/tests/README.md)。GAP-054/055/056/057/119。

实际入口：`scripts/verify.mjs`、`scripts/build.mjs`、`scripts/lib/build-state.mjs`、`scripts/verify-package.mjs`、`requirements/verification-system/tests/run.mjs`；同测试根下的 `support/{run-inner,verdict-feed}.mjs`、`e2e/support/supervise-node-test.mjs` 与 integration/Long Stroke 入口。016 已选择固定隔离输入方向，不重新论证“前后 hash 足够”。

### VS-016 · P0 · 实际验收绑定同一个不可变候选（2 TODO）

接续[依赖归档准备](../archive/2026-10-04/S03依赖归档准备-2026-10-04.md)已补真实npm安装45项与完整selected Node/npm bundle 15项定向通过，0失败；详细入口与证明范围见[本批记录](../archive/2026-10-04/S03真实npm与Node工具准备-2026-10-04.md)和[016测试说明](../../requirements/verification-system/tests/README.md)。真实安装使用独立HOME/npmrc/cache及私有Node执行`npm ci --ignore-scripts`，原始package/lock一次读取，实际安装完整成员与发布归档物化结果相等，安装临时root回收后才返回。非法registry/依赖/override/workspace/link、工具身份不符、真实integrity失败、lock缺项和取消均有公开反例，`null`取消仍保留原原因。

安装receipt的`bootstrap-admission`只检查选中Node字节与npm CLI入口；独立工具owner的`selected-node-npm-bundle`才绑定选定归档中的完整Node/npm成员并执行真实探针。二者共用归档校验，工具探针后重新验证完整物理成员；实际CLI新增文件被拒绝。步骤边界复核不是只读保护或同阶段改后恢复证明。

npm声明的必需生产依赖图须在所选npm包内闭合；direct `graceful-fs`及transitive `@gar/promise-retry`缺失、父目录实际补包的正式反例拒绝。optional缺失允许，存在则递归校验，不宣称任意loaded module、绝对文件读取或OS闭包已封闭。完整npm11.12.1工具包另取得15项定向通过，仍不是实际仓库依赖安装。Homebrew Node26的58项定向只有45通过、13失败，真实缺`libnode`被拒绝；后续平台证据统一见本批记录，旧失败保留。

早期45项默认夹具实际npm为11.18.0，当时尚未安装仓库依赖；接续实际application安装证据见下文，不扩大旧夹具范围。工具prepare/runProbe取消已先红后绿：挂起实际npm探针时保留Error/null原原因，POSIX进程组/后代退出并排空pipe、回收全部ownedroot；启动前取消先于缺失归档读取。

第二批见[工具归档安装与输出根](../archive/2026-10-04/S03工具归档安装与输出根-2026-10-04.md)：新增`installVerificationDependenciesFromToolArchive`，真实定向10项通过、0失败（含父组）；安装器自己准备完整工具，直接使用原bundle角色路径，最终发布前完整复核工具，固定`toolDigest`进入installation/dependencyDigest，返回独立依赖前回收工具与安装root。两锁定包、held tarball后非CLI库/成员改动、actual Node/npm版本拒绝、Error/null取消及启动前取消都有正式证据。新入口未升级旧raw bootstrap的证明范围，也未接实际verify或提供只读能力。

Mac真实嵌套挂载已证明输出root保留、仅清子项、不动只读输入及owned挂载回收；受控编译spawn证明发布清理，不证明实际Fable只读执行。upstream `590a3f69e` copy/chmod实际父目录替换反例仍报告PASS、exitCode0，文件inode/ctime恢复不能闭合运行期输入。下一步闭合SDK、Git及dotnet/Fable/NuGet，执行真正只读候选并绑定实际verify。Windows子树回收未证；GAP-055仍PARTIAL，T418/T419保持。

实际application依赖现有原生Node定向4项通过、0失败、0skip/TODO（1父3叶）：选定`174c2a2533`的Git tree/sourceDigest，以完整Node22.23.3/npm11.12.1归档真实安装236个仓库锁定包；toolDigest与11693成员完整inventory绑定，平台optional有7项存在、16项缺失，实际Fable List/Acorn/Tar消费者通过且全部ownedroot回收。证据见[590同步记录](../archive/2026-10-04/Upstream增量-590a3f69e-2026-10-04.md)。该结果仅属于明确选定tree及darwin-arm64依赖，不证明当前dirty合并输入、native Host/lifecycle/SDK、实际Fable编译、RO或actualverify，不关闭T418/T419。

接续[选定SDK准备](../archive/2026-10-04/S03选定SDK准备-2026-10-04.md)、[本地工具恢复](../archive/2026-10-04/S03本地工具恢复-2026-10-04.md)与[单项目工程 owner](../archive/2026-10-04/S03工程NuGet单项目准备-2026-10-04.md)，现新增[独立编译 owner](../archive/2026-10-04/S03实际单项目Fable编译-2026-10-04.md)。前批单 net10.0/no ProjectReference、私有派生 lock/清空缓存 locked 复验和 raw/contentHash 区分保持；编译将原 artifacts 按字节/mode复制到自有 seed，实际 SDK/Fable DLL只将合法产物写入本次根，完整库存与四 input digest 绑定。后续按顶部运行器第一批验收→detached tool 独立监护批→同候选实际构建/验证的顺序；准备优化必须语义等价，Git/OS、只读候选仍需证明。其它工程图单独证明，不重复准备owner，不删除T418/T419。SDK packs/tool bundled FSharp.Core不能算前批四包工程图已证明的闭包。

2026-10-04已实施指定tree的源码准备owner及真实Git对象回归，见[源码准备记录](../archive/2026-10-04/S03指定树源码准备-2026-10-04.md)。当前receipt仅绑定源码，不绑定实际verify、依赖或只读执行；后续从该owner接入，不另造工作区copy快照或第二份候选真相。

- **已有**：完整tracked输入集合及Git inventory失败传播已补；源码/资源/规范/脚本等输入和步骤边界变化能拒绝。本次补符号链接枚举前置：输入根、普通文件/目录及tracked corpus父目录的未知链接一律拒绝，不跟随外部可写目标；普通文件不会因名叫obj而被当作输出。见[S03记录](../archive/2026-10-03/S03输入链接边界-2026-10-03.md)。当前仍在可变工作区执行，“一步内改后恢复”是现存 executable TODO。
- **前提**：明确输入闭包包括被generator实际消费的tracked corpus、工具链/锁文件、构建脚本、测试与规范；dist和日志是输出，必须放可写且不反向成为输入的区域。原工作区继续编辑与快照被篡改是两个不同命题。
- **先红 A**：真实 verify入口的 build/unit/integration/package 分别记录所用候选身份与根，原工作区在运行中编辑不影响隔离候选；不能偷借原repo dist、node_modules解析路径、资源或git查询。让任一阶段错误cwd/import回原repo，验收必须失败。
- **先红 B**：在同一步内尝试write→restore、rename、delete/add、符号链接替换快照输入；写被阻止或整个候选立即失效，绝不能最终hash相同就通过。准备期间并发改变输入也不能形成混合世代。
- **步骤**：先设计可证明的固定输入准备与可写输出分界，再让所有阶段从同一封闭根执行，外部依赖按锁定身份解析；记录candidate/closure/toolchain/结果绑定。对准备失败、取消、子进程失败做资源回收。copy/chmod/fs.watch各有绕过边界，不单独当完整设计。
- **验收/停止**：两正式反例在实际入口成立，输入准备一致、所有阶段同源、输出证据绑定唯一候选，原工作区不被锁死。受当前平台权限模型限制无法保障不变性时清楚列出边界，不能先删TODO承诺未来补齐。
- **590合并取舍（未结算）**：上游copy/chmod物化与inode/ctime/mtime/size/hash边界检查不能证明输入不可变。真实父目录替换→阶段读新字节→恢复原目录仍报告PASS exitCode0；本次保留本地verify/build-state/016/017及原TODO，不把上游快照运行或manifest称为本地交付。T418/T419与GAP-055保持PARTIAL，实际verify同候选、完整依赖、真正只读执行及结果绑定按以上步骤继续，证据见[590同步记录](../archive/2026-10-04/Upstream增量-590a3f69e-2026-10-04.md)。

### VS-审阅债 · P1/P2 · 不增加虚假占位测试

- **006/全阶段**：本批 worker start/drain诊断、判决续期、背景不续期、active/queued区分已补；仍要按构建/检查/打包/LongStroke各阶段核对是否只有总时限而没有合法进展监测。新增受控挂起/持续噪声/合法进展反例，不放宽5000ms或调并发掩盖问题。
- **006 本轮增量**：已定位连续同步操作/微任务使Node原生reporter延迟投递判决；完成叶后单次调度让步恢复传输，正式健康、挂起噪声、after异常反例在Node22/26先红后绿。此修复不关闭全阶段审阅债，也不把任意输出变为进展。
- **Host就绪已证增量**：100ms请求截断、响应验证和canonical路径缺陷已修，原health/path各5000ms阶段预算不变。正式物理回归Node22/26各10/10，最终完整integration的resident通过，harness285/285；保留旧失败及受控并发日志，不断言旧全量失败的唯一原因。此卡不用重做，GAP-054其余阶段仍按上文逐项审阅。
- **008/020**：逐测试命题审阅完整结果/副作用、独立oracle、生成器seed和收缩重放；不能按assert数量或JSON字段数机械判充分。
- **009/021**：现有发现集、真实文件完成、结果流排空、skip/TODO非通过已证；继续核对父entry→子entry→CI/release报告，TODO/skip/取消不会被外层“exit 0”抹掉。报告链受控注入真实todo文件，而不是把fixture字符串误算全仓债务。
- **验收**：每条审阅记录链接真实入口/反例；保留已证明边界，没发现新缺陷不要求生产修改。VS-016是发布同候选证据的前置，不让其它本地绿色冒充其完成。

## 9. structured-workflow：3 文件 / 3 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/structured-workflow/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/structured-workflow/tests/README.md)。GAP-061/062/087。

真实退休入口 `src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs`，事实/投影在 `Mission/Relay/{Facts,Fold,Contract}.fs`，生命周期与冻结能力沿其 `ToolRuntimeScope` 注入。`Mission/Relay/Retirement/Surface.fs` 当前自写 decide/freeze，不能作生产退休证明。结构唯一门禁是 `scripts/checks/subsystems.mjs`，不得另建第二治理注册表。

### SW-003 · P1 · 退休恢复从事实重新进入普通流程

- **已有/欠缺**：一些生产决定和源码扫描仍有效；旧 RetirementSurface 不是实际退休owner。
- **先红**：真实suicide/retirement分别在freeze、cleanup、RetirementCommitted前后停止进程，重开只用journal+当前物理资源观察；故意留下过期临时stage/continuation地址，不能成为恢复authority。
- **步骤**：从SuicideTool和真实恢复入口确认未完成义务，根据facts重新证明并调用普通流程；发现保存“下一步”就移除该旁路，但不把真实物理handle/generation误删。保留异常/取消路径。
- **验收/停止**：每个断点最终一次合法退休或明确未决，未恢复closure/程序计数器；独立读取事实顺序和资源。若该退休协议的恢复入口本身不存在，先给所有者设计，不能扩充假Surface。

### SW-006 · P1 · 退休冻结准入、排空资源后才完成

- **已有**：工具角色准入真实用例保留；freeze/resources的自写Surface不能证明实际流程。
- **先红**：实际suicide进行时用受控资源barrier保持一个incumbency资源未排空，同时发新准入；新准入拒绝，已有资源合法结清后才能RetirementCommitted。road级不属于本incumbency的资源按规范处理，不能一概阻塞。
- **步骤**：跟SuicideTool的TryFreezeRetirement/RetirementBlockersFor/UnfreezeRetirement到真正scope owner；测试error/cancel/cleanup失败与重复调用，修仅有依据的组合漏洞。
- **验收/停止**：冻结、排空和commit来自同一执行，父工作流不窥探子执行位置；失败时保留blocked/释放的正确事实。003/006多轴文字若冲突先裁决，不能顺手引入状态调度器。

### SW-018 · P1 / 登记形式先定 · 关键流程的准入/结算证明

- **已有/欠缺**：测试内表格和模拟守恒器已撤，当前无生产证明；WHAT仍要求词汇所属subsystem、主导WHAT、trace关系、可执行proof可核对。
- **前提**：先选现有结构事实可承载的最小登记形式，定义关键流程有限范围与证明指向；不能把proof路径存在当runtime授权，也不建新中央执行器/符号ACL。
- **先红**：选一个真实admission/settlement工作流：无准入却产生trace、一次准入两次结算、未决被整体成功包装三个反例；登记缺/错subsystem/失效proof会被同一实际检查入口拒绝。合法取消/有界无效果终态也有正例。
- **步骤**：从真实owner产出可观察facts/trace，与已登记允许关系对照；逐流程扩展。runtime不保存“proof运行到第几步”，静态只查它能可靠查的引用关系。
- **验收/停止**：每条关键流程真实行为证明可达且有违约对照；局部成功但结算未决不能整体成功。登记形式需要产品/架构裁决时先停，不复活测试本地simulator。

## 10. distribution：6 文件 / 6 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/distribution/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/distribution/tests/README.md)。GAP-210/211。

真实入口 `scripts/verify-package.mjs` 已有实际npm pack、流式archive验证、解包与独立consumer；`scripts/verify.mjs`负责release编排，`scripts/lib/build-state.mjs`/`scripts/lib/owner-compile.mjs`负责构建身份/计划。现有编号用例大多只验证局部checker和仓库视图。应复用这些入口，不再造一套打包流程。

### DIST-001 · P1 · 真包和独立消费者的完整证明

- **已有**：归档闭包checker；入口具备实际pack能力但当前TODO未证明完整运行。
- **前提/先红**：从VS-016同一候选取得真实tgz digest；消费进程CWD在仓库外，独立依赖安装，屏蔽回原repo的解析。删除包内必须资源/模块时消费必须因实际缺项失败。
- **步骤**：正式001驱动实际verifyPackage，保存pack JSON、tar成员hash和consumer结果；网络安装条件单列，不借repo node_modules省事。
- **验收/停止**：真实tarball身份与consumer所装一致，插件可导入并完成规定生产消费，不只default export形状。无法离线/联网取得依赖时标环境阻塞，不以复制目录代替。

### DIST-003 · P1 · 安装manifest只解析已验证归档

- **已有**：仓库manifest一致和外部CWD加载不是安装证明；mock.tgz旧测试已撤。
- **先红**：安装真实tgz后让每个manifest入口解析，实际路径必须落在该包；破坏entry指向源树、缺dist成员、路径逃逸、大小写差异以及repo回退会失败。
- **步骤**：复用DIST-001产物/consumer，从installed package.json和真实resolver观察，不在测试构造另一个manifest替代。证明必须root文件按GAP211裁决计入closure。
- **验收/停止**：完整exports/main/资源入口按当前manifest逐项可用，无相对仓库依赖；不凭路径字符串前缀就宣称实际import已发生。

### DIST-005 · P1 · 同输入clean与incremental字节一致

- **已有**：fresh/no-op/陈旧manifest/缺产物/清理旧输出真实边界；尚未比较实际完整编译结果。
- **先红**：两隔离输出目录同候选输入：A clean；B构建旧版本→合法最小变更/删除/移动→回到目标输入并incremental。比较完整manifest输出闭包、相对路径和每文件bytes，受控旧cache/漏删会红。
- **步骤**：使用真实Fable编译与既有owner compile，覆盖稳定fsi实现改动、签名/源码集合改动、生成资源变化；区分不应比较的时间/日志字段与应确定的生产bytes，不剔除实际差异凑绿。
- **验收/停止**：最终生产集合与每文件字节一致，不含陈旧多余文件；依赖VS-016与structured012真实cache回归。不得只比较目录名列表或mock compiler输出。

### DIST-007 · P1 / 归档合同裁决 · 全release同一generation

- **已有**：fake runSteps证明顺序、archivevalidator反例证明归档校验；两者相加不是完整发布。
- **前提**：先裁决WHAT“仅dist/resources”与必需package.json/README/LICENSE的GAP211；VS-016必须提供真实同源候选。全release还依赖所有阻断性TODO/真实LongStroke条件，不应允诺本卡先于它们整体验收。
- **先红**：真实release某阶段失败/取消/报告有TODO时后续pack或发布成功结论被阻断；故意在阶段间换artifact/generation必须失败；范围缩小环境变量不能继承污染完整执行。
- **步骤**：让clean build、完整unit/integration、单Host LongStroke、actual pack/extract/consume使用同一候选身份；正式007核对实际证据链，不只调度数组。
- **验收/停止**：所有阶段真实执行且证据指向同一generation/tgz，任何未执行项明确阻断。单次verifyPackage成功不能宣称release完成；不真正发布包来证明可打包。

### DIST-008 · P1 · 安装包中全部声明资源可用

- **已有**：repo资源closure、语言对、部分实际读取；缺exact artifact。
- **先红**：从实际安装manifest/生产资源声明生成有限资源集合，在外部CWD经生产loader逐项读取；破坏一个真实语言资源、模板/静态资源后正确失败，不能fallback repo。
- **步骤**：复用DIST-001的tgz而非再次pack；记录每资源installed path与字节，与同候选期望对应。缺语言应fail closed，和PL-009共享loader规则。
- **验收/停止**：所有declared资源实际可读取、精确内容一致、无未声明越界fallback；只检查文件存在/仓库rg清单不足。

### DIST-010 · P1 · 安装插件仅注册活跃工具/资源

- **已有**：repo RolesSurface/ToolSurface当前注册投影；module路径不代表角色，历史解码词不属于活跃注册。
- **先红**：从exact installed package启动真实插件注册，捕获完整tools/resources；注入一个旧角色活跃工具或缺一个现行必需工具的受控包会失败，历史decoder字符串不应误报。
- **步骤**：同consumer载入真实生产entry/config，按当前office/delegation/Sphinx-v2合同验证注册语义；不把全仓禁词扫描当角色生命周期证明。
- **验收/停止**：完整注册集合与各工具真实可见性/能力合同一致；插件导出函数存在不足。复用HB能力fixture，安装环境缺Host时保留必要integration条件。

## 11. js-semantic-surface：2 文件 / 2 TODO

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/js-semantic-surface/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/js-semantic-surface/tests/README.md)。GAP-058/059/060。

实际门禁：`scripts/lib/test-surface-scan.mjs`、`scripts/checks/js-module-linkage.mjs`；本包支持fixture为 `tests/support/workspace-fixture.mjs`。已有完整literal import/re-export图、加载失败、边界注册正反例继续保留；链接成功不等于行为正确。

### JS-001 · P2 / 规范裁决 03-D1 · .js支持文件与.mjs-only

- **已有可红**：正式测试传递依赖有`.js`support，而现行WHAT要求`.mjs`；编译器F#输入是被测数据，不能按测试实现禁掉。
- **前提**：裁决support是否属于正式测试载体规则，以及production JS被测试导入与test support的分界；不能按文件名含domain等关键词裁决。
- **先红/步骤**：决定严格后逐个迁移真实support及所有静态/动态literal引用，更新入口注册而不改业务；决定允许特定载体则先修改唯一WHAT并给准确范围正反例，不能加无依据路径豁免。
- **验收/停止**：传递依赖、循环、未引用support按同一真实scanner检查，所有实际消费者可加载；不批量后缀替换后只看grep干净。裁决前保留TODO。

### JS-006 · P1 / 规范裁决 03-D2 · 物理退出探针的合法入口

- **已有**：真正Host fatal child需要内部入口，链接图本身完备度较高；删除这些physical probes会丢真实证据。
- **前提**：决定非compiler physical probe应经哪个窄公开能力Surface，或规范明确的物理adapter测试边界；与HB-029/EFP-006共同确定，不给所有内部模块通行证。
- **先红/步骤**：先保留现有child行为基线，再迁移一个真实fatal/nonfatal探针；未注册内部语义import仍被拒绝，新合法入口实际触及同一productionowner并真实退出。compiler产物图测试维持其独立豁免语义。
- **验收/停止**：Surface不暴露构造权限，物理证据不缩水，非法内部import继续失败；不能以导出一个测试专用复制实现关闭TODO。

### JS-审阅债 · P2

- GAP-059/060需逐Surface核对独立领域契约、必要性、真实调用和原生JS边界。现有registration只证明存在与引用，`js-contract`只证明当前值结构；不能推断Promise兑现值、getter、opaque内部或业务授权都正确。
- 只对发现的具体漏洞补实际正反例。不得为所有export机械加“函数存在”测试或扩大公共万能DTO。

## 12. requirement-system：0 显式 TODO，但有必要人工审阅

依据：[WHAT](/Users/yuanxi/Workwork/vibe-fs/requirements/requirement-system/WHAT.md)、[测试说明](/Users/yuanxi/Workwork/vibe-fs/requirements/requirement-system/tests/README.md)。GAP-050/052。现存编号测试为001/004/005/006/007/008/011/017/018；不要求每条WHAT必有一个测试文件。

实际检查器 `scripts/lib/spec-rules.mjs` 与 `requirements/requirement-system/tests/support/structure.mjs`；现有AST声明识别和结构正反例保留。

### RS-001/002/003/005/007/008/010/015 · P2 · 语义权威人工审计卡

- **已有/欠缺**：唯一ID、定义/引用、标题锚点、残留测试和存活编号机器检查有效；不能识别不同ID同义、无编号隐性规则、README偷立准入、历史记录与现行规范混用、公理地位或一次变更是否原子闭合。
- **前提**：以当前requirements为权威，不为本计划读取未授权proposals并把它们提升为规范；历史决策号可列待确认，当前WHAT明确的先按现行执行。
- **审阅步骤**：逐包WHAT/WHY/README/测试标题建立“命题—唯一owner—引用—实际行为证据”表；优先本分册已点出的语言/准入/归档冲突；发现重复/隐规则先记录精确句子和互相不兼容场景，再让owner修改唯一正式定义及引用。
- **可反证材料**：同一场景被两条规范要求相反结果、README增加WHAT没有的拒绝条件、旧ID被赋新义、测试标题用已删除条款；机器能判的例子交现有检查器，语义判断保留人工理由。
- **验收/停止**：每个冲突有明确裁决、同步测试和引用；人工审阅说明证据与边界，不能新建恒真测试/关键词计数冒充完成。002/003/010/015没有测试文件本身不是缺陷，不造占位NNN。

## 共用依赖与避免重复施工

1. **证据底座**：VS-016服务真正同候选release；现有runner的TODO/排空/active诊断不得回退。Host canary关联诊断先于扩大真实Host覆盖。
2. **权威与效果**：EFP-003/004/006/007 → PAR-019/020/014/022 → HPFO-005/006；可以并行写独立反例，公共Policy/Workflow/PluginHooks只一人编辑。局部permit测试不代替完整send链。
3. **路由与恢复**：EMR-011按当前accept→acquire→project；EMR-019固定road绑定；PAR-021 target settlement。三项共享ModelRouting，禁止分别修出互斥版本。
4. **Blogger链**：PAR-010/017/018共享durable材料与真实Workflow夹具，但分别保留预算、所有权、等待的正式断言；一个综合快乐路径不能替代失败切点。
5. **语言/表示**：PP-013的真实sink清单供PP-014与PL-008/009复用；不共享两包的权限或语言决定。PL-010锚点对应不代替PL-012语义对读。
6. **安装/发布**：DIST-001取得唯一真实tgz → 003/008/010消费同一包；005独立证明真实构建确定性；007最后汇总实际release。不要每卡重复pack不同世代后拼“全链通过”。
7. **裁决单独排队**：JS-001/006、PAR-007预算limit、PAR-013语言、EMR-004用途边界、DIST-007根成员、SW-018登记形式。每项先提供具体冲突和可选择后果，不要求用户批准抽象“继续调查”。

## 覆盖清单（与运行时 71 TODO 逐项核对）

| 包 | 含 TODO 的全部 NNN 文件 | 文件数 | TODO数 | 其它必须保留的边界 |
|---|---|---:|---:|---|
| host-boundary | 019、029、032 | 3 | 3 | 032真实hook已绿≠安装版异常自动after；013旧不退出说法需按当前证据更新 |
| provider-projection | 001、010、011、013、014 | 5 | 5 | 008/009/012既有真实writer/byte证据不重做 |
| provider-language | 002、008、009、010、012 | 5 | 5 | 008旧bound session标题；013交付局部证据仍不能等于完整Host |
| host-provider-failure-ownership | 002×2、003、004、005、006、007 | 6 | 7 | 002是已知Host行为失败，不是仅未执行 |
| provider-attempt-recovery | 005、006、007×2、008、009、010、011、012、013、014、015、016、017、018×2、019、020、021、022、023 | 19 | 21 | 007预算证据裁决；013语言冲突；023已有局部修复 |
| execution-failure-policy | 003、004、005、006×2、007、008、010、013 | 8 | 9 | 物理receipt/fatal同一child；已退役fuse不复活 |
| execution-model-routing | 004、007、011、012、016×2、019×2 | 6 | 8 | 011旧标题/GAP131不得覆盖新WHAT |
| verification-system | 016×2 | 1 | 2 | 009/021 fixture字符串不算TODO；全报告链和oracle仍需审阅 |
| structured-workflow | 003、006、018 | 3 | 3 | 历史001/007/008/017伪证明不复活；真实退休入口不是Retirement.Surface |
| distribution | 001、003、005、007、008、010 | 6 | 6 | 007归档根成员冲突；真包与完整release分开 |
| js-semantic-surface | 001、006 | 2 | 2 | 两项规范裁决；GAP059/060人工接口审阅 |
| requirement-system | 无 | 0 | 0 | GAP050/052语义债不可因0 TODO遗漏 |
| **合计** | **全部文件均有工作卡** | **64** | **71** | **只读规划，未执行测试/构建** |

以上已经逐包读取当前 WHAT、README、TODO 标题和相关 GAP，并追踪列出的真实 owner/调用入口。生产实现满足程度以每卡“已有/欠缺”限定；本分册不把路径存在、源码词形、一次超时后重跑成功或旧发布DONE当闭合证据。
