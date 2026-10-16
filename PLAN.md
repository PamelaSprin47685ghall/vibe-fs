# 规划底稿：blogger tip 自由化（S3 · 流程与伪代码）

阶段：S3（流程与伪代码）。本稿为接力底稿，逐棒累积；未决处用 ◇ 标注，随裁决收敛。
S1 结账：目标形式化（§1）、事实（§2，含 §2.5 清零）、根因（§3）、取舍与裁决（§4/§5）齐备；末任空集评审通过（关键事实经两轮独立只读复核）。
S2 结账：§7 理解、§8 探索、§9 主路径、§10 反事实检验齐备；末次评审（本任）以空集通过，评审前经 devops 独立抽样四项承重事实（compose 注入公式与 loadFor 双写；zh 节名分布与 9 份英文标题清单；Surface resolveField 包装与签名；chronicle 10 组资源键与显式键机制），全部属实。
历史（已交棒）：修复 §1 语言维度、§4 悬置（1 条 GAP）；修复 §2.6-e en 侧陈述、§8.6 条款清单、§7 ◇（3 条 GAP）；修复 resolveByField 调用点漏记、§8.6 gd [003]/[005]「保留」与原文不符、provider-language 分类裁定点缺失（3 条 GAP）；修复 016 记述（第 184 行为标题串、186/187 行方为断言）、resolveField 写点漏记（Surface.fsi:34 与 007 测试消费点）、§2.2 cyclic-dependency 归类与 §2.6-e 收敛、013/003 测试归属补记（4 条 GAP）；修复 §9.1/§9.3 M3 依赖单一化（1 条 GAP）；修复 gd [004]/[007]/[009]/[011]/[012] 条款处置、§9.2 durable 载荷面缺项、bd-008 锚点归属、bd [015] 族类归属（4 条 GAP）。
上一棒：只读检验判出 4 条 GAP（§9.2 触碰文件族缺 Surface.fs / Surface.fsi；M5 未记录 9 份「英文标题＋中文正文」文件的处置与断言口径；七字段描述文案未入文案资源清单；退役措辞漏「不要误判」）；经 devops 只读核实（zh 节名分布、chronicle 字段描述资源键机制、英文标题 zh 文件形态、Surface 公开面）后修订 §2.2/§2.6-e/§8.1/§8.5/§9.1/§9.2；S2 评审通过（本任空集确认）。
上一棒：S3 首段——写入 S3.0 算法索引与执行顺序、S3.1 Algorithm C [契约定案]；评审留任后续写 S3.2–S3.13 全部十二段（A/S/D/L/K/P/I/B/R/G/F/V），S3.0 索引状态全部更新；评审前独立抽查三项事实（016 三处 120、101 目录与 404 份、ChronicleTool 150–200 行），写作前经 devops 复核 durable 载荷与解码先例（TipGuidanceDelivered.OccurrenceId 手写 decoder 先例、Journal 单条 append、provider-language 四条原文）。
上一棒：只读检验判出 5 条 GAP（G4 同名比较点落点不全；bd-018 测试无处置；F/C7 对 duplicated-control-flow 的覆盖口径不自洽；Codec 协议检测集与 [017]/006 落点缺失；§9.2 最后一项待裁决点未结断）；经两轮 devops 只读核实（比较点行号与集合键、018/017 现状断言、Codec 检测常量、006/001/015/gd-001 断言敏感度）后评审留任修订：G4 扩为同名比较点清单、C3.3/C3.4 注记、C4.1/S6 补细节、C6.4 与 L5.1 补 018、C7.7 与 F1.3–F1.5 口径修正、D5.1/D5.2 补协议检测集与 006 同步、R5.1 结断公开面、§2.2/§7/§8.5/§9.1 数字同步。
上一棒：只读检验判出 4 条 GAP（七节拼装正文的字节界收口缺失；R 读路径形态偏离既有先例且 digest 校验未具体化；tipName「原样保留」字节语义未钉死；V4 的 120 残骸口径与 A2.2/A4 冲突）；经一轮 devops 只读核实（AgentJournal 与读链装配、validateContentBounds 调用点与超限形态、tryCanonicalText/renderStructured 的 trim 语义、FactCodec 现状、016 的 120 位点）后评审留任修订：D4.2 补字节界收口、K3 补写前前提、C2.2/C2.7 措辞精确化、R1/R2 收敛为既有读形态并具体化 digest 校验、R5.1 与 §9.2 同步、D2/D5.2/G3/G4.3/C3.4 统一 tipName 字节语义、V4 口径对齐。
上一棒：只读检验判出 6 条 GAP（解码错误值与 006 L76 更新指引冲突；CanonicalBlogCall/CanonicalCycle 新结构与 EnforcerTip 处置未定；renderTipSections 与注入拼接的字节形态未钉死；「刀口」「提醒」的 canonical 落点未给；casefold 措辞未收敛；V1 前置门语义空转）；经四轮 devops 只读核实（006/018/007 断言与访问面、Surface 输出形状、ChronicleContent 与 evidence 去向、CanonicalCycle 装配链与语言可得性、语言服务公开面、rule.Name 与渲染机制、刀口/提醒节语义）后评审留任修订：D1 定载荷结构、D4.1 定拼装调用点与语言权威、D4.2/D5/D5.3 补类型级收敛与 Surface 形状、D4/I1/C6.1 定字节形态、D2/D5.2/C4.2 定错误面分层、C7.6/F1.3 补刀口/提醒落点、casefold 措辞全稿收敛（§1/§4/§6/§8.3/§9.1/§10.1/S3.0；C3.2 加收敛注记）、V1 改写、§2.6-h 补记。
上一棒：只读检验（经一轮 devops 只读核实：016 的 120 断言与 compose 包含式形态、004 comment 两段镜像、006 八种输入单串全等与 schema 六键、101 目录 en 568085 B / zh 490789 B 字节统计、DeliveryProjection/Tip.fs 行号比对）判出 2 条 GAP（§10.2 的 M3 体积失败分支未落 S3；B5 与 D5.4 对 016 提示词面断言的指令矛盾）；评审留任修订：Algorithm I 增 I6 体积异常步骤（原 I6 顺延为 I7）、B5 收敛为 D5.4 口径。
上一棒：只读检验（经三轮 devops 只读核实：006 八种输入循环与错误串、EnforcementCycleRecord/ByProviderRun/tryFindByProviderRun 与 Tip.fs 访问面、001/008/015/017 测试消费面、ProviderRunIdentity 构造、测试运行入口；并入 §2.6-i）判出 5 条 GAP（006 八格落位未裁；R2 的「CycleId → 投影记录」机制未指名；M2 测试同步覆盖不全——bd-008、bd-017 无处置步骤、bd-015 未点名、bd-001 缺「保留」结论；C3.3 括注措辞与 P2/G4.1 不齐；I6 的异常呈报措辞超出执行者工具面）；评审留任修订：D2 增空值读法裁决（D2.1）与八格落位表（D5.2）、S4 判定序同步；P1/P1.1 补 ByProviderRun 事实、R1/R2 写深取机制（ProviderRunIdentity.create + tryFindByProviderRun）；新增 D5.5/D5.6、K6 补 bd-015、新增 C6.5；C3.3 与 G4.1 注记措辞收敛；I6 呈报语义改写（C8.5 属 M1 规划期、ask 有效、不改）。
上一棒：只读检验（四轮 devops 独立抽样：006/016 断言与 120 三处、投影与深取链、文案资源与 Path 机制、008/015/017/001/003 锚点、观察视图两文件与 016 六块消费面及 observationPayload）判出 3 条 GAP（观察视图承载文件 Observation.fs/ObservationSurface.fs 未入触碰文件族与任何算法落点；观察视图字段面同步（tipIdentities、cycleRecordOfJs）未落步骤、bd [015] 配对视图缺实现挂点；D5.4 仅点名第二块 fixture、未逐块处置 016）；评审留任修订：§2.6 增 (j)；§9.2 文件族补录两文件；K6 精确化（JS 键 camelCase、observationPayload 同 K2 口径）并新增 K6.1/K6.2；D5.4 扩为六块逐块清单（三处同形 fixture）；C6.1 bd [015] 增实现挂点。
上一棒：只读检验（四轮 devops 独立抽样：previousTips 链源码事实（Recovery/ProjectionSurface/BlogSurface/Builder/Prompt/Toml）、gd-008 全文与 composer/resolver/audience 断言、cc 套件键名消费与 WHAT 条款、msl-007 载荷面与两个 JS 构造面、HOST-026 编号）判出 5 条 GAP（previousTips 链未落任何算法与文件族；gd-008 三块处置口径缺失与 bd/gd-008 编号错位；gd [008] 重述与 Q1 双灌入兼容未裁定；K6 的 observationPayload 兜底口径与 K6.2 矛盾；D4.1 的 HOST-026 引用不可定位）；评审留任修订：§2.6 增 (k)；§9.2 文件族补录（BlogSurface.fs、Recovery.fs、Context/Companion 四文件、cc 测试）；Algorithm P 增 P6–P11（previousTips 链）与标题同步；I5 拆 I5.1–I5.4（gd-008 三块口径）；§9.4 的 M2/M3 标注修正；C6.1 gd [008] 三分语义定稿；K6 observationPayload 口径对齐 K6.2；D4.1 语言权威引用修正；S3.0 表行同步。
上一棒：只读检验（两轮 devops 独立核实：① 007.test.mjs 全文与执行期校验可达面——resultForTip 私有、requirements 零命中、bd support 仅达解码面；② tryFindByField 归属与消费面——Catalog 公开函数、016:190/191、018:9、008:27 断言自洽；③ MissingTipError 消费面——Codec 字符串常量、Surface 死导出、bd-006:76 字面量唯一消费）判出 4 条 GAP（L5「既有工具调用面」空指不可执行；C8.5「调用 ask」超出 S4 执行者工具面；旧错误面符号 MissingTipError/missingTipError 退役未处置；006 标题「same error」与分组断言失实）；评审留任修订：L5 改锚 enforcer.decodeCall 并注记执行期不可达边界、C8.5 改 I6 同口径、新增 D2.2 旧错误面退役、D5.2 补标题更新句、V4 清单补入该符号、§2.6 增 (l)。
上一棒：只读检验（两轮 devops 独立抽查：承重事实四项——016 的 184/186/187/188 行逐行性质、blob 读接口 IBlobWriter.Read（Writer.fsi:14）与 Wire.fs:85–96 读回+digest 先例、DeliveryProjection 精确匹配形态、Recovery.fs:111 的 tip.FieldName 映射；边界一致性四项——ChronicleExecution.fs 校验族与路径、两套 tests/support 文件现状（cycle.mjs 默认 tip 键、journal.mjs 仅假 blob 引用）、TomlSurface.fs:53–54 纯转发形态、Catalog.fsi/Codec.fsi/Projection.fsi 存在与签名）判出 2 条 GAP（§9.2「不触碰」段把 bd [015] 列为「与自由化无关条款」，与 C6.1/C6.6 的改写及 K6 的实现同步矛盾；§9.2 触碰文件族缺列 ChronicleExecution.fs/.fsi 与两套 tests/support 文件）；评审留任修订：§9.2「不触碰」段移出 [015] 并新增「[015] 处置」行、触碰文件族补录 ChronicleExecution.fs 与 ChronicleExecution.fsi、requirements/behavior-diagnosis/tests/support/cycle.mjs、requirements/guidance-delivery/tests/support/journal.mjs。

本棒：只读检验（四组 devops 抽查：ChronicleExecution 校验族与计量先例、两套 support 文件与 009 blob port 先例、101 目录族谱与 23 无节抽查、016 第 184–188 行；另两轮补充：guidance support journal 实为真 journal 不透明盒、TipSurface 公开面与 Persistence blob 写读对先例）判出 3 条 GAP（tipName 200 界计量单位未裁决；I5.2 的 blob 物化接线缺口——port/替身路线类型层不可行、最小接线为 TipSurface 新增写入助手；S4/D3 的「missing-tip-field: 节名」与 C4.1 的 camelCase 字段名裁决不齐）；评审留任修订：C2.4/C2.5/S4/D2/L5 定计量单位（String.Length）、S4/D3 指代词统一、I5.2 修正为 TipSurface 写入助手路线并注记与 009 异同、§9.2 触碰文件族补录 Guidance/TipSurface（fsi 同步）、§2.6 增 (m)。

本棒：用户裁决 Q3/Q4——tip 内容字段收敛为 opinion 与 advice 两个（opinion 填对当前操作的主观批判性评价、advice 提出分析经验教训后的建设性意见；tipName 身份键与按名去重保留，Q2 未被推翻）；resources/enforcer 每规则只留中英两文件 lesson.md / lesson.zh-CN.md，不再分检测（enforcer.md）/处置（main.md）两套。实施背景（补录于 §2.6-n）：七字段方案（tipName + whatToDoNow/whyThisMatters/repairStrategy/decisionBranches/commonWrongFixes/verification/doneWhen）已在此前实施棒中全链落地；M5 统一格式已完成——resources/enforcer 404 份文件节序列零变体（en 侧 101×enforcer 八节 + 101×main 七节，zh 侧同构 canonical 中文）。本棒为纯文档修订，就地改写契约面：§1 第 1/2/5 条、§4/§5 补 Q3/Q4 裁决、§7 输出形态、§8.1/§8.2/§8.5、§9 主路径 M1–M5、S3.0 注记、S3.1（C1/C2/C4/C5/C6/C7）、S3.3（S1/S2/S4/S6）、S3.4（D1/D3/D4 及连带）、S3.8（I1/I2/I5）、S3.9（B2/B3）、S3.12 整体改写为库重组工作包、S3.13 残留清单扩充；§8.4/§8.6/§9.4/§10 的失效指称做最小同步。代码与资源未动；未选路线（单一大文本字段、恢复七节）的理由在 §4-E 在案。

## 0. 任务

用户委托摘要：blogger 的提意见目前主要走 tip，但 tip 必须从已有库中选择，限制了发挥。改为：仍然把全部已有库灌入 blogger 上下文（bootstrap + 格式样板），但 blogger 写 tip 时自由发挥。为此，参考所有 enforcer tip 的格式确定字段们的名字，然后用这些文本字段替代 tip 这个离散字段。

解读：把 chronicle 工具中名为 tip 的离散枚举字段替换为一组自由文本字段；字段名参考 enforcer tip 文本的格式确定；库仍全量注入 blogger 上下文，但角色从「可选值域」变为「bootstrap 与格式样板」；blogger 不再受「必须从库中选择」约束。

## 1. 目标状态（形式化）

改造完成后，可判定地应满足：

1. chronicle 工具的 provider-facing schema 中不再存在离散枚举字段 tip；内容字段收敛为两个自由文本字段（Q3 裁决；第一轮实施的七字段 whatToDoNow/whyThisMatters/repairStrategy/decisionBranches/commonWrongFixes/verification/doneWhen 随之收敛，见 §2.6-n）：opinion（对当前操作的主观批判性评价）与 advice（分析经验教训后的建设性意见），camelCase 拼写、必填 string、trim 非空（见 §8.1）；另设一个自由文本「名」字段承担身份与去重（Q2 裁决 = B2；已定：trim 非空、单行、String.Length ≤ 200，比较经 trim + 简单大小写折叠（.NET ToLowerInvariant），见 §8.3 与 C3.2）。
2. blogger system prompt 全量注入全部库文本：每条规则注入两段元素——Name（裸目录名）与 LessonText 全文（Q4 后库为每规则单文档 lesson，EnforcerRule 以单一 LessonText 取代 EnforcerText/MainText；按会话语言单语装载、不合并双语），当前 101 条；文案中「必须恰好选择一个 Rulebook TipName」类约束被替换为「参照样板格式自由撰写、可自由命名」的语义。
3. 执行期与解码期不再存在「任何非空 tip 串被归一化映射到库内某条规则」的行为（resolveByField / Levenshtein 家族退役，调用点全列表见 §2.1）；名字作为身份键保留（按名去重），已定其规范化为 trim + 简单大小写折叠（.NET ToLowerInvariant，见 §8.3 与 C3.2）；新文本字段沿用既有内容字段的准入校验族（trim 非空、字符/字节界等，见 §2.5-C）。
4. 落库、投影、Main 交付按 Q2 裁决改造：交付链骨架保留（首次 Full、之后 identity-only、按名去重）；Full 正文以 blogger 自由撰写的文本字段为主体（库文本退出交付正文来源）。换源触发面已核实（§2.5-C）：owner 语言选择（gd [002]）、Coverage 身份单位（gd [001]/[003]/[005]）、Audience 隔离（gd [008]）需重设；冻结机制可原样复用；设计路径已展开（正文走新 blob 通道落库、交付读回 + digest 校验，见 §8.4 与 §9.1 M4；残余 ◇ 见 §10.1 假设 A）。
5. 库重组为两文件制（Q4 裁决，取代已完成的 Q1 统一格式工作包，完成基线见 §2.6-n）：resources/enforcer/<TipName>/{lesson.md, lesson.zh-CN.md} 共 202 份，不再分检测（enforcer.md）/处置（main.md）两套；H1 = 「# <TipName> — Lesson」（en/zh 同）；顶层两节 en =「## Opinion」「## Advice」、zh =「## 评价」「## 建议」；子节为原节内容降级为 ### 搬移——en Opinion 收 enforcer.md 的 Definition / Governing Principle / Trigger When / Do Not Trigger When / Distinguish From / Decision Procedure / Examples 七节原序，Advice 收 main.md 七节原序 + enforcer.md 的 Nudge 殿后，zh 同构且子节保留原中文名；节正文原样搬运、零改写、零丢失；旧 4 文件全部退役删除。结构与验收方式已展开（§8.5、Algorithm F）。
6. 规范与测试同步：behavior-diagnosis 与 guidance-delivery 中与「tip 必然归一到 TipName」绑定的条款与测试锚点全部更新，不留条款-实现矛盾（条款见 §2.3，锚点见 §2.4）。

## 2. 已核实事实

出处：本任两次 devops 只读调查（含真实 bash 静态读取）。行号为调查记录，标「约」者为区间引用，以当前 HEAD 为准。未标「待核实」者均为已核实。

### 2.1 现状链路（blogger 产出 → 校验 → 落库 → 交付）

- blogger 提示词：resources/provider/role/blogger/en.md（zh-CN 成对）；因果脊 charge → occurrence → settlement → consequence → tip；tip 约束「恰好一个 Rulebook TipName」（约 50–52 行）。
- 工具 schema：src/Wanxiangshu/OpenCode/Tools/ChronicleTool.fs；tip 为枚举，值域 = 全部 TipName（约 186–187 行；清单由 tipFieldNames() 生成，约 160 行）；charge/occurrence/settlement/consequence 为必填 string（约 174–181 行）；evidence 为可选 string（约 183–185 行）。
- 执行校验：resultForTip（约 100–106 行）：空 tip → missing-tip；resolveByField 命中 → remembered。内容字段过 tryCanonicalText（trim 非空；ChronicleExecution.fs 约 17–23 行）。
- 归一解析：EnforcerCatalog.resolveByField（Catalog.fs 约 130–140 行）：trim → 精确命中 → Levenshtein 最近 → 并列取 LexicalOrder 最小；任何非空串必解析、无 UnknownTip 逃逸（behavior-diagnosis [007]）。调用点、写点全列表（经本任 devops 独立复核）：Catalog.fs:130（定义）、Codec.fs:80、ChronicleTool.fs:104、Surface.fs:221–227（resolveField，JS 语义面包装，接 caller 提供的规则数组做解析）、Surface.fsi:34（resolveField 公开签名）、Catalog.fsi:22（签名）；另有测试消费点 007.test.mjs:19/24/25/27（该 JS 面的既有行为锚，随 Levenshtein 家族退役处置，见 §2.4、§8.6）——此前「两处调用点」的记载据此更正。
- 解码校验：Enforcer/Codec.fs decodeCall（约 159–165 行）：tip 缺失/非串/空白 → MissingTipError；CanonicalBlogCall = { Content; Tip }（约 24–26 行）。
- 落库：Enforcer/Cycle/Commit.fs：BlogObservationCommitted 携 TipRuleId、FieldNameAtCommit（约 191–209 行），与 blob/coverage/provider run 原子提交。
- 投影：Enforcer/Projection.fs：EnforcementCycleRecord（约 7–17 行）；RecentTip { RuleId; FieldName; CycleId }（约 21–24 行），容量上界 8。
- 交付：Enforcer/Guidance/Tip.fs：resolveTipGuidance（约 181–186 行）→ owner 最近 tip → tryFindByField → Full 形态（「# Enforcer Tip」+「tip = "<name>"」+ main.md 正文，约 37–43 行）或 IdentityOnly（「tip: <name>」，约 30 行）；首次 Full 原子写 TipGuidanceDelivered（约 84–109 行；payload 见 Host/Facts.fs 约 36–40 行）；注入点 OpenCode/Host/PairProgrammingThoughtTransform.fs composeMarkerDocument（约 1325–1346 行）。
- Coverage/Frontier：Enforcer/Guidance/DeliveryProjection.fs；重锚清 Coverage、保 Frontier（Context/Companion/Blogger/ContextFactFold.fs 约 133 行）。
- 库与装载：resources/enforcer/<TipName>/{enforcer.md, main.md, enforcer.zh-CN.md, main.zh-CN.md}；目录 basename = TipName = RuleId = FieldName（单一真相；behavior-diagnosis [001]）；当前 HEAD 实测 101 个规则目录（文档与测试标题中出现过 120、334 等历史数字）。装载层整文件读入、不解析小节结构（EnforcerCatalogResource.fs 约 51–104 行；composeBloggerSystemPromptFor 约 28–43 行）。
- 注入点：RuntimeResourceAssembly（启动预载）；ProviderSystemTransform.fs 约 101–108 行（请求期按语言重拼，Blogger 分支）；Enforcer/Surface.fs 约 186 行（测试面）。
- 文案资源：resources/provider/tool/chronicle/ 10 组 × 中英：description（含 {{rule_count}} 占位）、arg-tip、missing-tip、remembered、nothing-to-remember、arg-charge/occurrence/settlement/consequence/evidence；{{rule_count}} 由实测条数填充（ChronicleTool.fs 约 160–167 行）。

### 2.2 格式统计（字段名单的事实基础；本棒全量复核）

- en 侧复核（确认）：enforcer.md —— 78/101 文件共享固定顺序的 8 个小节：Definition / Governing Principle / Trigger When / Do Not Trigger When / Distinguish From / Decision Procedure / Examples / Nudge；23 个文件无任何「## 」小节（仅 H1 与散文）。main.md —— 同批 78/101 共享固定顺序的 7 个小节：What To Do Now / Why This Matters / Repair Strategy / Decision Branches / Common Wrong Fixes / Verification / Done When；无小节者同为那 23 个文件。目录总数三口径复核 = 101。
- 23 个无小节文件清单（与既有记录逐名核对一致）：behavioral-boundary-untested、blob-after-event、cancellation-not-propagated、contract-test-missing、failure-path-untested、fragment-event-as-data、log-as-recovery-protocol、lost-update、memory-before-disk、missing-regression-test、optimistic-retry-assumption、order-dependent-test、overwrite-history、partial-write-assumption、property-test-missing、race-first-wins-semantics、recovery-by-filesystem-state、resource-not-scoped、retry-not-idempotent、sleep-based-synchronization、snapshot-as-truth、test-implementation-coupled、time-dependent-test。
- zh 侧全量分布（本棒实测；按「enforcer.md / enforcer.zh-CN.md / main.md / main.zh-CN.md」节数分组，各组数量之和 = 101）：
  - 0/0/0/0：23 个（即上述清单；zh 两侧亦无小节）。
  - 8/0/7/0：20 个（en 两侧完整；zh 两侧小节骨架整个丢失）：big-batch-intent、blind-edit、blocking-event-loop、boundary-collapse、canary-skipped、catch-all-swallows-future、cross-layer-internal-import、ephemeral-verification、guessed-migration、manual-toil-repeat、mutable-public-state、permit-leak、random-source-in-logic、release-ladder-skipped、repeated-known-mistake、serial-when-parallel、status-announcement-noise、time-source-in-logic、tool-error-ignored、unbounded-fanout。
  - 8/5/7/5：24 个（zh 用 5 节变体：enforcer 缺 Governing Principle/Decision Procedure 且自创「刀口/提醒」；main 缺 Repair Strategy/Decision Branches）：commented-out-code、dead-code-delivered、debug-print-left、destructive-without-authorization、dirty-hack、domain-language-drift、leftover-scaffolding、legacy-cruft-retained、misleading-name、missing-architecture-gate、missing-invariant-documentation、mock-hidden-state、premature-optimization、premature-unification、scope-creep、secret-in-code、spike-not-cleaned、stale-documentation、todo-bomb、truncation-skips-damaged、unrecorded-decision、unversioned-schema、weak-boundary-parsing、wholesale-rewrite。
  - 8/7/7/6：12 个（zh enforcer 7 节变体；zh main 缺「决策分支」；其中 cyclic-dependency 的 zh main 为「分支判断」六节变体、非普通缺节，见 §2.6-e）：clone-and-mutate-derived、command-event-confusion、context-model-leak、cyclic-dependency、duplicated-truth、implicit-control-flow、impure-core、mixed-side-effect-boundaries、phase-flag-accumulation、rule-spaghetti、translator-layer-bloat、wrong-rule-composition。
  - 8/5/7/6：1 个（unrecorded-lesson）；8/7/7/7：1 个（duplicated-control-flow，「分支判断」替「决策分支」）；8/8/7/7：20 个（唯一完全同构组）：boolean-blindness、compatibility-cruft、coverage-theater、facade-hides-mess、false-gate、flaky-test-tolerated、guess-based-fix、guessed-not-verified、half-finished-refactor、illegal-state-representable、incidental-complexity-dominates、non-exhaustive-transition、null-ambiguity、primitive-obsession、repeat-until-pass、stringly-typed-error、timeout-inflated-to-pass、type-erosion-at-boundary、unverified-completion-claim、weakened-test-to-pass。
- zh 与英文主流结构同构计数：enforcer 20（16 个中文映射 + 4 个英文标题原文）、main 21（15 个中文映射 + 5 个英文标题原文 + 1 个「分支判断」变体按角色映射）；同构 = 语义映射后节名一一对应且顺序一致。
- 「en 有节而 zh 无节」20 例；「en 无节而 zh 有节」0 例。
- 混合形态：9 个 zh 文件为「英文标题＋中文正文」（enforcer：illegal-state-representable、non-exhaustive-transition、stringly-typed-error、type-erosion-at-boundary；main：上述 + null-ambiguity）。中文措辞分叉：「为什么重要」与「为什么这很重要」并存；自创节名「刀口」「提醒」为英文结构所无。
- 中英不同构（此前单点观测 domain-language-drift）现已知为普遍现象：正文按语言整文件选取，无逐节映射。
- 小节词汇当前非契约：guidance-delivery/tests/008 标题即「Blogger composer takes detection source regardless of section vocabulary」；behavior-diagnosis [008] 视 Nudge 类桥接字段为禁物（「## Nudge」仅是小节名，不装载为字段）。
- 待重写规模（统一格式工作包基线）：en 侧 23 个目录的两份 en 文件（46 份）+ zh 侧非同构 enforcer 81 份 / main 80 份（161 份）+ 9 份「英文标题＋中文正文」文件（enforcer 4 / main 5；清单与处置见下行）+ duplicated-control-flow 的 zh main（「分支判断」措辞替换 1 份；见 §2.6-e）；合计 217 份。
- 「所有 tip 的格式」无法字面满足——Q1 裁决以「统一格式」工作包消解此边界：23 条无小节文件、全部双语不同构者、以及 10 份标题/措辞待替换 zh 文件（9 份「英文标题＋中文正文」：enforcer 4 = illegal-state-representable、non-exhaustive-transition、stringly-typed-error、type-erosion-at-boundary，main 5 = 上述 4 加 null-ambiguity；另 duplicated-control-flow 的 main）一并对齐主流标准，后者的处置为标题/措辞替换为 canonical 中文名（见 §1 第 5 条、§8.5）。

### 2.3 规范锁定（须逐条面对的条款）

- behavior-diagnosis/WHAT.md（全量 [001]–[019] 连续 19 条，本任 devops 全量清单复核）：涉 tip 语义者——[001] 身份恒等与唯一真相（TipName = RuleId = FieldName；禁平行清单）；[004] 检测语料全量注入；[005] 多语言本地化叶子完整性（正文非空且 TipName / RuleId / FieldName 恒等）；[006]「tip 必须可归一到 TipName」、缺失/空白/非串稳定错误面；[007] trim → 精确 → Levenshtein → LexicalOrder、无 UnknownTip 逃逸；[008] 离散单条 tip、禁数值评分与桥接字段；[009] 每 run 恰一次 chronicle；[011] fail-closed 内容硬界（新协议四句正文渲染后 ≤512 KiB）；[012] 单一 TipRuleId/FieldName 原子提交；[014] RecentTip 容量 8；[015] tip↔frame 配对、禁假 tip；[016] 历史压缩协同裁剪 min(K, tips) 条 RecentTips、不新增 tip、不触发新交付；[017] 无效 terminal 含缺 tip；[018] RulebookRevision 冻结「chronicle.tip 枚举、解码映射表、Main 处置索引」。与改造无关者——[002] 规则装载 fail-fast、[010] Cycle provider-run 身份门禁、[013] Coverage 出生门、[019] Enforcer fatal typed incident（四者均不涉 chronicle tip 字段语义，本任 devops 逐条正文核对）。
- guidance-delivery/WHAT.md：[001] Frontier 按 occurrence、Coverage 按 TipName；[002] Full = 标题 + TipName + main.md 正文（owner 语言）；[003] IdentityOnly =「tip: <name>」；[005] 重锚清 Coverage、保 Frontier；[006] 最近已提交 tip；无关联/无 tip/无对应规则 → None；[008] 两端共用 TipName，检测正文仅 Blogger、处置正文仅 Main。（gd 全集 11 条、编号无 [010]；清单外 [004]/[007]/[009]/[011]/[012] 经复核与改造无文本冲突，处置见 §8.6。）

### 2.4 测试锚点（tip 语义相关）

- behavior-diagnosis/tests：001、003、006、007、008、009、012、013、014、015、016、017、018。其中 007 为 Levenshtein 行为锁；014 为单 tip/RecentTips；016 的 120 断言位于第 186、187 行（rules.length 与 ruleCount 等于 120），第 184 行为测试标题字符串（含 120 字样、非断言），第 188 行为动态派生先例；120 是历史硬编码（commit 8f9485c12 时 resources/enforcer 实为 120 目录，HEAD 为 101），当前静态推定必红、未实跑（见 §2.5）；018 为枚举冻结。归属补记（本任 devops 独立复核）：013 属 [013] Coverage 出生门，与 tip 语义无涉（fixture 的 tip 字段未被任何已执行测试调用，无须改动）；003 覆盖 TipName 身份三元组与目录校验（第 46–57 行动态 N 写法可作 M0 修复 016 的对照先例），保留作身份面回归。
- guidance-delivery/tests：001、002、003、005、006、007、008、012（004/009/011 与 tip 正文解耦，处置见 §8.6）；002 按 locale 直接读库 main 正文文件并断言 first Full 包含其全文（换源后需改）；005 断言重锚后恢复 Full 正文一次（换源后恢复路径须保持；frontier 断言为 todo GAP-115）；007 为 latestTipNudge/latestTipGuidance 别名字节相等的锚（别名结构保持即自动保绿）；008 锁定「小节词汇不参与合成」。
- 测试全走 JS surface 行为名断言，不直调 F# 标识符（改造可借力此隔离层）。

### 2.5 待核实项的清零结果（本棒 devops 只读调查；三项均可复跑）

- 016 的「120」：断言位于 016.test.mjs 第 186、187 行（rules.length 与 ruleCount 等于 120）；第 184 行为测试标题字符串（含 120 字样，非断言）；第 188 行为动态派生先例——经本任 devops 独立复核摘录原文确认。证据：commit 8f9485c12 时 resources/enforcer 实为 120 个目录（含现已不存在的 abbreviation-anxiety），其后裁剪至 HEAD 的 101 个，断言未同步。按 Surface.rules → EnforcerCatalogResource → PackageResources → 仓库根/resources 的读取路径，静态推定该断言当前失败；未实跑（边界如实标注）。
- zh 全量小节分布：见 §2.2（全量分组、同构计数、清单）。en 侧复核一致；「en 有节而 zh 无节」20 例、「en 无节而 zh 有节」0 例。
- 字节预算与冻结契约（结论）：
  (a) 字节界——MaxBlogTextBytes=512KiB、MaxEvidenceBytes=128KiB（Cycle/Model.fs 约 8–12 行；validateContentBounds 约 77–91 行）与解码期 MaxInlineEvidenceChars=1024（Codec.fs 约 55–58、104–111 行）均作用在 Blogger chronicle 提交/解码层；超限行为是 fail-closed 拒绝，不是截断（bd [011]）。Main 交付侧（Guidance/Tip.fs 全 197 行、PairProgrammingCalibration、Host marker 写入路径）在已读范围内无字节界与截断——「已读范围内未发现」，非全仓证明。
  (b) 冻结与重锚——交付冻结靠 PairProgrammingGuidelineAnchored.MarkerText 按 occurrence 落盘（Host/Facts.fs 约 16–23 行），commitPairInjection 判重后原字节回放（PairProgrammingThoughtTransform.fs 约 1067–1100 行）；重锚只清 Coverage、保 Frontier（DeliveryProjection.applyReanchor）；Full 正文的 owner 语言经 languageOf + rulesFor 查库选择（Tip.fs 约 156–158 行）；重复交付走 IdentityOnly。条款：gd [011]、prefix-stability [010]/[014]、time-capability [007]。
  (c) 换源触发面——若 Full 正文改为 blogger 自由文本：owner 语言选择机制失效（gd [002]）；Coverage 身份单位需从 TipName 重设（gd [001]），gd [003]/[005] 措辞级改写（原文「规则全文」「已交付规则」的指称物失效，双轴语义保留，见 §8.6）；Audience 隔离与「Blogger 历史为低信任观察」定性需处理（gd [008]）；枚举映射与处置索引族重写（bd [001]/[007]/[018]）；provider-language 无条款点名交付正文（[006] 资源文件门禁不覆盖；Tip.fs:32–35 标题串为代码常量、本就不在资源机制内），但 [005]「完成文案」与 [013]「进入模型的 Class A 文本」字面可覆盖交付 marker 文本，分类裁定见 §8.6；冻结机制本身可原样复用。测试触发清单：gd-002（第 7–28 行）、gd-008（第 58–61 行）、gd-003、gd-006、bd-016（第 184–203 行）、bd-007、bd-018、bd-012。
- 不可核实项：无（三项均静态核实完成；动态行为归后续测试覆盖）。

### 2.6 接口事实补足（多轮 devops 只读调查）

（a）chronicle 文案与校验链：
- 文案面 10 组 × 中英成对：description、arg-charge、arg-occurrence、arg-settlement、arg-consequence、arg-evidence、arg-tip、missing-tip、remembered、nothing-to-remember。
- description en（15–17 行）：「tip is required: choose exactly one of the {{rule_count}} Rulebook TipNames for the reusable lesson this concrete transition teaches」；zh（13–14 行）同义。arg-tip（单行）：en「Exactly one Rulebook TipName naming the reusable lesson carried by this concrete transition. Choose the best lesson, not a diverse or novel label.」；zh「恰好一个 Rulebook TipName，用来命名这次具体转折携带的可复用 lesson。选择最贴切的教训，不要为了多样或新奇而换标签。」。missing-tip：en「Choose exactly one Rulebook lesson for this transition.」；zh「请为这次转折恰好选择一条 Rulebook lesson。」。remembered / nothing-to-remember 不涉 tip 语义。
- schema 装配：ChronicleTool.fs 160–161 行 fields = tipFieldNames () / ruleCount = List.length fields；167 行 description 渲染填 {{rule_count}}；174–181 行四个内容字段为必填 string（stringSchemaDescribed）；182–185 行 evidence 为可选 string；186–187 行 tip = enumSchemaDescribed fields（值域 = 全表）。tipFieldNames = EnforcerCatalog.fieldNames（75–76 行）。
- 执行期校验：resultForTip（100–106 行）：空白 → missing-tip；否则 resolveByField 命中 → remembered（返回值丢弃）。
- 解码期校验：Codec.fs decodeCall 159–165（缺键 → Error；null → Error）；decodeTip 87–94（非 string → Error）；decodeTipString 70–85（trim 空 → Error；非空 → resolveByField 必中 → EnforcerTip.ofRule；Option.defaultValue Error 分支实际不可达）。两处生产校验点并存：工具执行期 + transform 解码期（resolveByField 调用点全列表含 Surface.fs:221–227 的 JS 语义面包装，见 §2.1）。

（b）装载 / 合成 / 重拼：
- localizedFiles（EnforcerCatalogResource.fs 21–24）：English → enforcer.md / main.md；zh-CN → enforcer.zh-CN.md / main.zh-CN.md；单语言成对选择，双语各装载一次、互不合并。
- loadFor（51–104）：两份叶子均读入记录（82–83 行读入，94–95 行 EnforcerText / MainText 写入 EnforcerRule）。
- composeBloggerSystemPromptFor（28–43）：只对每条注入 rule.Name + rule.EnforcerText.Trim()（40–41 行）——**MainText 已读入记录但未进 prompt**；改造即在此处加注入。标题 en「Enforcer Rulebook」/ zh「Enforcer RuleBook（规则书）」，按 LexicalOrder 排序。
- RuntimeResourceAssembly 8–37：启动时双语各预载一次（11–14），安装 BloggerSystemPrompt（23–29），RuntimeResources 同时持有三份列表（32–34）。ProviderSystemTransform 101–108：请求期对 Blogger 重拼同一公式。
- blogger 角色提示词 en.md 50–52：「tip names the one reusable lesson this occurrence teaches the participant. Choose exactly one Rulebook TipName. The tip is the abstraction; the other four fields describe this concrete turn.」

（c）交付与投影：
- Tip.fs：tipIdentityText 30（tip: %s）；tipHeading 32–35；tipFullText 37–43（# Enforcer Tip + tip = "%s" + 正文；正文空 → fallback identity）；tryOwnerMainSession 45–51；latestOwnerTip 53–63；isTipIdentityOnly 65–79；recordFullTipDelivered 84–109（TipGuidanceDelivered 写入，失败仅诊断）；guidanceForRule 117–147（130–136 IdentityOnly；137–146 Full，138 行正文来源 = rule.MainText）；guidanceForField 149–161（156 行语言 = languageOf mainSessionId；158 行 tryFindByField 查库）；resolveTipGuidance 181–186；latestTipGuidance 189–193。
- Facts.fs：TipPresentation 10–13（Full / IdentityOnly）；PairProgrammingGuidelineAnchored 16–23（MarkerText 第 20 行）；TipGuidanceDelivered 36–40（SessionId / TipName / OccurrenceId / Presentation）。
- DeliveryProjection.fs：State 7–16（DeliveredOccurrences 单调、重锚不清；CoveredTipNames 重锚清空）；isIdentityOnly 26–31；apply 46–73（Full + occurrence → Frontier；Full + tip → Coverage；IdentityOnly / None → 状态不变）；applyReanchor 77–79（保 Frontier、清 Coverage）。

（d）条款关键句补充：
- bd [004]（15–17 行）：「有效的 Blogger effective system prompt 由基础 Blogger prompt、# Enforcer Rulebook 标题、live Rulebook 全部 ## <TipName> 及其对应当前语言的 EnforcerText 全文按 LexicalOrder 确定性拼接而成」。
- prefix-stability [010]（41–45 行）：「guidance 只能附在本次请求最新一条真实消息上……同一未重锚 horizon 按 durable occurrence 原位、原字节回放，不删除、过滤、去重、搬移或叠加第二后缀……ContextReanchored 退休旧 occurrence 的可见性」。
- prefix-stability [014]（62–64 行）：「guidance 后缀正文只参与 provider 呈现，不进入 XTrace、Companion decode、Blogger delta、WorkRecord 或 compaction 输入」。
- gd [011]（39–41 行）：「每个自动注入 occurrence 持久记录序号、CallId、放置点与实际 wire payload 原文字节。历史重放使用这些字节，不随规则版本改写」。

（e）zh 标题全量族谱（M5 统一格式的依据）：
- en 侧：78 × 2 文件与标准节名全等（八节 / 七节，顺序一致），零例外；另 23 × 2 文件无任何「## 」小节（与 §2.2 同批的 23 个目录，双侧一致；本棒 devops 实测复核）。
- zh enforcer 五族：无 43（= §2.2 的 23 双侧无节 + 20 zh 无节）；「定义 / 何时触发 / 不要误判 / 刀口 / 提醒」25；「定义 / 支配原则 / 何时触发 / 不应触发 / 与相邻规则区分 / 判定程序 / 例子 / Nudge」16；「定义 / 何时触发 / 不要误判 / 刀口 / 与近邻区分 / 例子 / 提醒」13；英文八节 4。
- zh main 七族：无 43（同批）；「现在该做什么 / 为什么这很重要 / 常见假修复 / 验证 / 完成条件」24；「现在该做什么 / 为什么重要 / 修复策略 / 决策分支 / 常见假修复 / 验证 / 完成条件」15；「现在该做什么 / 为什么这很重要 / 修复策略 / 常见假修复 / 验证 / 完成条件」12；英文七节 5；「分支判断」六节 1（cyclic-dependency）；「分支判断」七节 1（duplicated-control-flow）。
- 更正（对 §2.2）：8/7/7/6 组中 cyclic-dependency 的 zh main 实为「分支判断」六节变体（非普通缺节）；unrecorded-lesson 与之无关，其 zh main 为含「修复策略」的普通六节（8/5/7/6 组合不变）。null-ambiguity 为混合形态：zh 检测叶 = 中文八节，zh 处置叶 = 英文七节。
- canonical 中文节名草案（取唯一完整翻译族的用词）：enforcer = 定义 / 支配原则 / 何时触发 / 不应触发 / 与相邻规则区分 / 判定程序 / 例子 / Nudge；main = 现在该做什么 / 为什么重要 / 修复策略 / 决策分支 / 常见假修复 / 验证 / 完成条件。退役措辞：「刀口」「提醒」「不要误判」「分支判断」「为什么这很重要」「与近邻区分」（「不要误判」经核实存在于全部 38 个有节 zh 检测叶：25 个五节族 + 13 个七节族，canonical 对应「不应触发」）。英文标题的 9 份 zh 文件（enforcer 4 + main 5）与 duplicated-control-flow 的 zh main（「分支判断」措辞替换）按「标题/措辞替换为 canonical 中文名」入 M5 对齐集合（见 §8.5）。

（f）落库载荷与 blob 通道：
- BlogObservationCommitted（Context/Companion/Facts.fsi 36–54）：TextRef: BlobRef + TextDigest: BlobDigest；TipRuleId: string + FieldNameAtCommit: string option；EvidenceRef: BlobRef option。
- 构建（Cycle/Commit.fs 185–209）：四个内容字段先经 EnforcerCycle.ofCall → renderStructured 合并为单一自然段（MergedText），写 Blob（185–186 行）；fact 携 ref / digest（202–203 行）与 TipRuleId / FieldNameAtCommit（206–207 行）。evidence 的 MergedEvidence 恒空，writeOptionalEvidence 实际返回 None——evidence blob 通道空置可复用其模式。
- 接口：WriteBlob → BlobWriteReceipt { BlobRef; BlobDigest }（AgentJournal.fsi 42；Writer.fsi 8–14）；读 IBlobWriter.Read: BlobRef → Task<Result<string, string>>（Writer.fsi）。消费样例：Wire.fs 91–96 readFrameBody（ReadBlob + sha256 digest 校验，不符即拒绝）。

（g）交付侧读取路径：不存在（新工作量确认）：
- Tip.fs 内无任何 blob / 正文读取——数据源只有 RecentTip 投影与 TipGuidanceDelivered fold；正文来自库文件 rule.MainText（138 行）。
- 系统别处有通用读能力（IBlobWriter.Read；Prefix 的 readFrameBody 带 digest 校验），但未接入交付链。改造需在交付侧新增「RecentTip → Cycle 载荷 → 新 blob ref（tip 处置正文，见 §8.4-4d）→ Read + digest 校验」调用（读能力经 journal.Writer.BlobWriter.Read 既有可达，无需新穿线；S3 设计伪代码见 Algorithm R）。

（h）S3 收尾轮补充核实（语言穿线、渲染链与载荷现状；本棒 devops 只读调查）：
- 语言服务（Language.fs:125、Language.fsi:29–36；SessionLanguage.fs:11–16；ProviderLanguageBinding.fs:15/64–70）：GlobalProviderLanguage.current: unit -> ProviderLanguage 为唯一权威（live global preference，启动与偏好变更时 refresh）；SessionProviderLanguage.languageOf 与 ProviderLanguageBinding.forSession/forSessionText 均接参弃参、恒返回 current ()。语言取用零上下文门槛；交付期 Tip.fs:156 即经 languageOf(mainSessionId)。
- 解码/落库链现状：Continuation.fs 与 BlogSurface.fs 全文件无语言符号；Context（Continuation.fs:59–70）持有 BloggerSessionId/Owner/Previous.MessageId；ofCall（Model.fs:50–59）签名 (call) -> cycle 无语言参数、validateBounds（Decode.fs:235–244）/validateSingleCall（:250）同样不接语言；BlogSurface.decodeCycle（:858–866）无语言。语言未穿线但零门槛可得（上条），ofCall 拼装点由此在函数内取全局权威（D4.1）。
- Surface 输出形状（Surface.fs:54–80）：tipToJs → { ruleId; fieldName; lexicalOrder }；callToJs（Structured）→ { charge/occurrence/settlement/consequence/evidence/tip }、（Legacy）→ { text/evidence/tip }；cycleToJs → { mergedText; mergedEvidence; tip }；decodeCall/canonicalCycle 经 resultToJs 包 { ok; value }/{ ok; error }。
- 解码载荷现状（Codec.fs:11–20）：ChronicleRecord = { Charge; Occurrence; Settlement; Consequence; Evidence: string option }；ChronicleContent = Structured of ChronicleRecord | Legacy of text * evidence option；decodeCall（:159–165）先查 tip（None/Some null 均 → MissingTipError）、再 decodeContent、再 decodeTip；evidence 经 decodeEvidence（:104–111）读取、入 ChronicleRecord（:128/:139），由 renderStructured（Model.fs:47）内联进 MergedText；MergedEvidence 恒空、writeOptionalEvidence 恒 None（独立通道空置）。
- 注入渲染链（EnforcerCatalogResource.fs:40–43/93–98；LlmFacing.fs:29/51–53/63；SyntheticToml.fs:103–107/205–215）：rule.Name 为裸目录名；每元素经 comment 渲染为 `# ` 注释块（空行为裸 `#`）、元素间单换行、末尾单换行；bd [004] 条款文字「## <TipName>」与实际字节的偏差随 I1/C6.1 修正。bd-004 以本地 comment 镜像固化该形态（004.test.mjs:5/13–18/20–21）。
- EnforcerTip（Catalog.fs:23–33）：{ RuleId; FieldName; LexicalOrder }，ofRule 唯一生产调用 Codec.fs:84；消费链 CanonicalBlogCall.Tip → CanonicalCycle.CanonicalTip → fact（Commit.fs:206–207）→ Projection；Surface 经 tipToJs 暴露。随 D1/D4.2 退役。
- 路径更正：ApplyEnforcerContinuation 在 OpenCode/Plugin/PluginTransforms.fs:963–973、调用点 :1235–1242（步骤 12）；管线层持有 languageFor（:161–162）未传入续作链（applyContinuation 只收 projectionSessionIdOpt/outObj）。
（i）S3 本棒补充核实（测试消费面、投影深取链与运行入口；三轮 devops 只读调查，行号以当前 HEAD 为准）：
- 006 现状（L72–78）：八种输入（undefined、null、''、'  '、1、false、{}、[]）循环断言同一错误串 'missing required argument: tip'，测试标题含「same error」；L44–49 四内容字段缺失循环断言 match(name)；L80–93 legacy 用例（tip 必带、entry/evidence 形态）；L131 schema 六键 deepEqual（charge/occurrence/settlement/consequence/evidence/tip、entry 缺席）；L95–126 合并渲染断言。
- 投影深取链：EnforcementProjectionState = { ByProviderRun: Map<ProviderRunIdentity, EnforcementCycleRecord>; RecentTips: RecentTip list }（Projection.fsi:22–24）；applySquash 只裁 RecentTips、不裁剪 ByProviderRun；tryFindByProviderRun: run → state → record option（Projection.fsi:39–40；impl Projection.fs:103–104），现状消费点仅 Continuation.fs:627 与 Commit.fs:242；Tip.fs 现状经 AgentJournal.snapshot → session.Enforcement → EnforcementProjection.recentTips → List.tryLast 取值（Tip.fs:55–63），无 ByProviderRun 直接访问；guidanceForOwner（Tip.fs:163–168）已携带 tip.CycleId。
- ProviderRunIdentity：private 单 case union（Foundation/Identity.fs:91；Identity.fsi:19），create / value 为唯一构造与取值（fs:281–283；fsi:110–112），无校验；RecentTip.CycleId = ProviderRunIdentity.value（Projection.fs:71）。
- Projection.fsi 公开面：三类型（EnforcementCycleRecord / RecentTip / EnforcementProjectionState）+ 六 val（RecentTipLimit = 8、empty、applyFromEntry、applySquash、tryFindByProviderRun、recentTips），无其它成员。
- 测试消费面：bd-001 仅目录打包身份（L12–17：rules.map(name) / fieldNames() / ruleId / fieldName = basename），不涉 tip；bd-008 三段消费（decodeCall/facade 的 tip 对象 L36/41、L227/230–231；fact fixture 键 L66–67/156–157/215–216；旧 journal 字面与匹配 L105/122/131）；bd-015 两处 cycleRecord fixture（L111–122、L162–173，键 tipRuleId/fieldNameAtCommit）；bd-017 末段解码器用例（L676–697）与 support/cycle.mjs 的 call()（默认 input 带 tip: 'primitive-obsession'）。
- 运行入口：npm run format-build-test → scripts/verify.mjs → unit 步骤 → requirements/verification-system/tests/run.mjs（build 陈旧门、TESTS_MJS_FILES 定向、固定 WANXIANGSHU_PROVIDER_LANGUAGE='en'）；behavior-diagnosis / guidance-delivery / prefix-stability 各套件均经该 runner 执行。

（j）S3 留任修订轮补充核实（观察视图承载文件、016 六块消费面与 observationPayload；本棒 devops 只读调查）：
- 观察视图域面（src/Wanxiangshu/Enforcer/Observation.fs，52 行，模块 ObservationProjection）：tipIdentities（26–31 行）从 recentTips 取 `(tip.FieldName, tip.CycleId)` 对（取 FieldName、非 RuleId）；observationsOf（37–41）＝RulebookObservation.ofTipsAndFrames(tipIdentities, frameDigests)；observationsAfterSquash（45–52）先 applySquash 再 observationsOf；文件头注释（6–13）声明配对折叠不拥有第二存储。P2 收敛（RecentTip 删 FieldName）后 31 行必改。
- 观察视图 JS 面（src/Wanxiangshu/Enforcer/ObservationSurface.fs，310 行；fsi:16/27/28 为 obj 签名）：workLogToJs（39–43）输出观察视图三键 { tipName; cycleId; frameDigest }；tipToJs（99–103）输出 recentTips 面 { ruleId; fieldName; cycleId }；cycleRecordOfJs（168–181）逐字段读 10 键（tipRuleId 在 178、fieldNameAtCommit 在 179，另含 run/textRef/textDigest 等）；applyEnforcementCycle（231–233）＝stateOfJs + cycleRecordOfJs → applyFromEntry；observationsOf（294–305）、observationsAfterSquash（307–310）。
- 类型面（src/Wanxiangshu/Enforcer/Rulebook.fs:12–30）：ObservationUnit = { TipName option; FrameDigest option; FrameBody option }；WorkLogObservation = { TipName; CycleId; FrameDigest option }——观察视图三键与之一一对应。
- 016 六块消费面（本棒逐块核实）：块 1（L53 OBS_PROJ_003，L3–95）fixture L22–33 经 applyEnforcementCycle，readObs/observationsAfterSquash/applyEnforcementSquash 读三键，断言 L73/L88–90/L92–93，不碰 recentTips；块 2（L184 A42_001）L190–198/L200–202 不动，L186/187 已由 M0 动态派生；块 3（L204 A42_002）L212–218 recentTips 映射、L222/L225；块 4（L227 A42_003）L232–236、L268–273、L277–278；块 5（L280 A42_004）L283–293 经 readObservations（L175–180）读 tipName/cycleId；块 6（L317 ENFORCER_TIP_12）fixture L304–315、L320、L324–327 只读 t.cycleId。三处 fixture（L22–33、L128–139、L304–315）字段同形（10 键）。
- BlogSurface.observationPayload（src/Wanxiangshu/Enforcer/BlogSurface.fs:604–625，private，17 键；tipRuleId 在 622、fieldNameAtCommit 在 623）：经 factOfJs（640–642）与 serializeFact/deserializeFact/deserializeEnvelope（fsi:75/78/86）触达；测试不按其名引用，间接消费于 008.test.mjs:103/129/180、012.test.mjs:21–22/30–33；managed-session-lifecycle/tests/007.test.mjs:272–273/284 经同类 HandleSurface 面触达（非 BlogSurface 面）。

（k）S3 留任修订轮补充核实（previousTips 链与 gd-008 / cc 套件；本棒 devops 只读调查）：
- previousTips 链：生产映射 Recovery.fs:105–112（精确落点 L111 的 tip.FieldName 须改取 tip.TipName；消费点 L189/L203–212 的传参位不变）；JS 输入面 ProjectionSurface.fs:131–169（ProjectionInput 的 PreviousTips L137；L169 读键 field）与 BlogSurface.fs:712–737（buildProjectionPlan；L737 读键 tipName）；渲染与转发层 Builder.fs:37/70–86、Prompt.fs:28–29、Toml.fs:117–125（键 kind/tip/cycle 硬编码、值取参数）、TomlSurface.fs:53–54。
- previousTips 测试消费面：gd-008:35/41；cc-012:480/481/508/509/523/585；cc-014:113/114/184；cc-018:693；bd-016:232–253（自 recentTips 映射 tipName/cycleId）。断言形态（本棒复核）：序列映射 deepEqual + regex 包含 + 布尔全等，键名字面替换不触断言逻辑。cc 套件 WHAT 无 previousTips 专条，最近条款为 [019]（X→Y 冷边界辅助注入归零）。
- gd-008 全文（71 行）：L11–19 composer 面（L18 的否定包含断言钉死 REMEDIATION SOURCE 不进 composer，双灌入后必红）；L22–30 resolver 面与 L49–68 audience 面（L28/L60 断言含库正文；support/journal.mjs 的 observe 直接种 observation 事实、textRef 为假引用、不写 blob；无「chronicle→读回」完整先例，最近先例为 prefix-stability 009 的受控 blob port L206–208）；L33–46 previous tip 低信任身份；L71 todo（GAP-116）。
- HOST-026：requirements 下零命中；host-boundary [026] 为「Host Contract/Runtime 编译分界」同号异义；语言权威的规范落点为 provider-language [002]/[003]，代码侧锚点见 ProviderLanguageBinding.fs:8 等 9 处注释。

（l）S3 本任补充核实（007 可达面与执行期校验面、MissingTipError 消费面；两轮 devops 只读调查，行号以当前 HEAD 为准）：
- 007.test.mjs 现状（36 行）：三处用例——L18–20 精确+trim（resolveField ' cat ' 命中 rules[0]）；L22–28 Levenshtein 决胜（'cot'/'ct'/'caut' → rules[1]、'coat' → rules[0]）；L30–36 远距必中（decodeCall('completely-unknown-name') ok、tip.fieldName 与 tryFindByField 一致）；四处 resolveField 消费点在 L19/24/25/27（此前记载属实）。007 改写锚既有可达解码面（enforcer.decodeCall）；§9.4 的 M2 行经核对无需改动。
- 执行期 tip 校验无 JS 可达面：resultForTip（ChronicleTool.fs:100）与 executeValidChronicle/executeChronicle 均为私有；Surface 唯一 tip 相关导出为常量 missingTipError（Surface.fs:155 = EnforcerCodec.MissingTipError），不执行判定；requirements 下 resultForTip/executeValidChronicle/missingTip 零命中；bd support 三文件（blogger-recovery.mjs、cycle.mjs、resource-package.mjs）中 cycle.mjs:6 仅构造 chronicle part 喂 decodeCycle（解码面）；最接近的真实执行路径为 bd-006 的 withExecutablePlugin + hooks.tool.chronicle.execute（L151–173），断言只达 NoLiveCycle 分支（L170）。
- MissingTipError 消费面：Codec.fs:28–30 [<Literal>] let MissingTipError = "missing required argument: tip"（Codec.fsi:22 签名）；五处产生点 decodeTipString L77–78/L85、decodeTip L94、decodeCall L161/L162；Surface.fs:155 转存为 missingTipError（Surface.fsi:26）、全仓无消费者（死导出）；测试面唯一消费为 bd-006:76 字面量（requirements 下该符号零命中、该字面串唯一命中）。

（m）S3 修复轮补充核实（TipSurface 与真 journal、计量单位与 blob 写读先例；本任 devops 只读调查，行号以当前 HEAD 为准）：
- TipSurface（src/Wanxiangshu/Enforcer/Guidance/TipSurface.fs，190 行；TipSurface.fsi 23 行）：公开面 8 val——createJournal（L76–89，经 JournalSurface.boot 起真 journal）/ disposeJournal / appendCompanionLink / appendObservation（L109–138，构造 BlogObservationCommitted，TextRef / TextDigest 取入参 L129–130、不写 blob）/ appendContextReanchored / resolve / latest / latestNudge（fsi L10–23）；文件内无任何 blob 写/读函数（blob 仅以类型构造出现于 L116/L129–130）；handle 链 = JournalHandleBox（私有，L20–21）→ journalHandleOf（unbox，L23）→ agentJournalOf（.Journal，L24）。
- guidance-delivery support 的 journal 实为真 journal 的不透明盒：journal.mjs 的 withJournal → guidance.createJournal；textRef/textDigest 为合成假引用（blob-${ordinal} / sha-${ordinal}，L24）、无 blob 物化；JS 假对象无法通过 journalHandleOf 的 unbox——port/替身路线不可行；009/cc-016 的受控 port 由 xwire.candidateFromJournal 消费（wire 候选链），与本交付读链非同一条。
- 计量单位两族并存：512 KiB / 128 KiB 族经 LlmFacing.byteCount → SyntheticToml.byteCount 手工 UTF-8 字节计数；MaxInlineEvidenceChars=1024 族为 .Length 字符语义（Codec.fs:58 定义、L109 判定）；ChronicleExecution.tryCanonicalText（L17–23）仅 trim 非空、无长度检查。
- Persistence JournalSurface 的 writePayload（Surface.fs:613–625）/ readPayload（:628–636；fsi:33–34）为真 journal 上的 blob 写读对；调用形态先例 durable-events-012:31–43（boot → writePayload → blobRef 为 blobs/<64 位 hex> 形态 → readPayload 读回）；I5.2 的 TipSurface 助手与其同形。

（n）Q3/Q4 修订轮补充核实（实施现状；本棒补录，来源 = 实施棒交付记录与 M5 执行记录；Model.fs 一处经本会话文件片段证实，其余未在本棒重新独立核实）：
- 七字段方案已全链实施：ChronicleTool schema 为 tipName + whatToDoNow/whyThisMatters/repairStrategy/decisionBranches/commonWrongFixes/verification/doneWhen 八字段（字段序按 C1.7）；Codec 含 decodeTipName 与内容字段解码、错误面含 missing-tip-field:<field> 参数化标识；Model.renderTipSections 位于 src/Wanxiangshu/Enforcer/Cycle/Model.fs:52–70（tipSectionTitles 七节名表 zh/en 同处证实）；ChronicleExecution 校验族含 tipName 单行/String.Length ≤ 200 与内容字段 trim 非空；bd WHAT.md 的 [004]/[006]/[008]/[011]/[015]/[017] 与 006/007/008/011/012/014/015/016/017 测试均已按七字段语义更新。
- M5 统一格式已完成：resources/enforcer 404 份文件节序列零变体（en 侧 101×enforcer 八节 + 101×main 七节；zh 侧同构 canonical 中文——enforcer = 定义/支配原则/何时触发/不应触发/与相邻规则区分/判定程序/例子/Nudge，main = 现在该做什么/为什么重要/修复策略/决策分支/常见假修复/验证/完成条件）；§2.2 的 217 份对齐口径已按此结清，结构断言与内容覆盖已跑。

## 3. 根因

- 症状：blogger 只能从库中选 tip，发挥受限。
- 三层机制叠加：① schema 层——tip 是枚举字段，值域 = 全部 TipName；② 归一化层——resolveByField 把任何非空串确定性映射到既有规则（自创新名也被吸附到最近邻，无逃逸）；③ 身份层——TipName 是下游全链的身份锚：落库 TipRuleId、RecentTips、Frontier/Coverage、交付查找、配对观察。
- 设计动机（不可改的边界）：确定性交付、反数值评分漂移、反假造规则、字节与前缀稳定（[007][008][015]、gd [006] 等的严格性皆服务于这些目标）。
- 真实冲突：自由创作必然把「不可预测文本」引入交付链；改造实质不是改 schema，而是把确定性从名字空间迁移到别处，并明确定义 Main 收到什么。Q2 裁决（B2）的迁移方式：确定性留在名字上（按名去重、identity-only 保留），但名字的解释权从库（枚举与归一）转为 blogger 自定与精确同名匹配；库退为样板。

## 4. 裁决与取舍记录（本棒由用户裁决）

A. 字段名单来源 —— 裁决：A1（字段名取 main.md 七节）+ A1/A2 双灌入 blogger 上下文 + 101 条统一格式。
  - 未选 A2 独用、A3：八节保留为 blogger 上下文样板（与 A1 并灌），不作工具字段名；统一格式工作包负责让全体文件符合两套标准结构。
  - 已知代价：统一格式工作包的真实规模已测得（§2.2）——en 侧 23 个无小节目录、zh 侧 enforcer 81 / main 80 个非同构文件（含 20 个「en 有节而 zh 无节」），重写范围远大于 23 条；统一后的验收方式已展开（结构断言 + 内容覆盖，见 §8.5、§10.4）。
B. tip 身份与交付去向 —— 裁决：B2（自由命名；同名仍然去重）。
  - 未选 B1（放弃名字）与 B3（不交付 Main）。交付链骨架（按名去重、identity-only）保留；名字不再映射到库，归一化（resolveByField/Levenshtein）退役。
  - 已知代价：名字稳定性由 blogger 自担；同名判定的规范化已定为 trim + 简单大小写折叠（.NET ToLowerInvariant；完整 Unicode casefold 经 C3.2 淘汰，见 §8.3）。
C. 消费者侧目录角色：main.md 退出交付正文来源（正文以 blogger 自由撰写的字段为主体）；MainText/tryFindByField 查库路径的处置已展开：退役，交付正文改为读回 blogger 撰写的落库字节（见 §8.4、§9.1 M4）。本棒已核实触发面（§2.5-C）：owner 语言选择（gd [002]）、Coverage 身份单位（gd [001]/[003]/[005]）、Audience 隔离（gd [008]）需重设；冻结机制可原样复用；交付侧无字节界（已读范围内）。
D. 新字段验证强度（已定：全部必填，见 §8.1）：trim 非空与字节界沿用既有族（tryCanonicalText、1024 字符、512KiB；作用层见 §2.5-C）；过重必填与「单 turn 单 lesson」简洁性有张力。
E. tip 内容字段收敛（Q3）——裁决：内容字段收敛为 opinion 与 advice 两个。opinion 填对当前操作的主观批判性评价；advice 提出分析经验教训后的建设性意见。tipName 身份键与按名去重保留（B 未被推翻）；tipName 校验（trim 非空、单行、String.Length ≤ 200）与规范化（normalize 比较）沿用已实施面不变。
  - 未选单一大文本字段：丢失评价/建议的语义分离（opinion 与 advice 各有明确的评价对象与时间指向，合并即不可判别）。
  - 未选保留七节（whatToDoNow…doneWhen）：违背用户简明裁决；第一轮实施的七字段（main.md 七节 camelCase 化）随本裁决收敛退役。
  - 已知代价（接受）：已落地的 schema、文案、条款与测试面（006/007/008/011/012/014/015/016/017）随本裁决二次改写。
F. 库文件结构（Q4）——裁决：resources/enforcer 每规则只留中英两文件 lesson.md / lesson.zh-CN.md，不再分检测（enforcer.md）/处置（main.md）两套。
  - 未选保持四文件制：检测/处置分套随 Q3 字段收敛失去承载者（内容字段已收敛为评价/建议两节，库不再按检测与处置分文档）。
  - 已知代价（接受）：库 404 份重组为 202 份；装载与注入面随单叶映射与单一 LessonText 同步（§8.2、Algorithm I）；旧 4 文件退役删除。

## 5. 用户裁决记录（本棒已回填）

- Q1：A1 格式（字段名取 main.md 七节）；灌入 blogger 上下文时 A1+A2 都灌入；另外把 101 条 tip 统一格式。
- Q2：B2——允许 blogger 自由命名；同名的仍然会去重。
- Q3：tip 内容字段收敛为 opinion 与 advice 两个——opinion 填对当前操作的主观批判性评价，advice 提出分析经验教训后的建设性意见；tipName 身份键与按名去重保留（Q2 未被推翻）。
- Q4：resources/enforcer 每规则只留中英两文件（lesson.md / lesson.zh-CN.md），不再分检测（enforcer.md）/处置（main.md）两套。
- 裁决引入的新工作面：统一格式工作包（§1 第 5 条，Q1 引入、已实施完成，经 Q4 由库重组取代）；双文本注入（§1 第 2 条，Q1 引入、已实施，经 Q4 由单文档两段元素注入取代）；交付正文换源（§1 第 4 条、§4-C，已实施）；内容字段收敛（§1 第 1 条、§8.1，Q3）；库重组为两文件制（§1 第 5 条、§8.5，Q4）；七字段符号、arg-what-to-do-now 族键与旧 4 文件退役（§8.6、Algorithm F/V）。

## 6. 继任者工作方位

- S2 成果：全部三节——探索（§8）、主路径（§9）、反事实检验（§10）；接口事实经多轮 devops 只读调查补足（§2.6），并由本轮全仓条款扫描复核（§8.6）；§1 与 §7 经修复与 S2 结论完全闭合（语言维度单语、悬置注记全部回填）。
- S1/S2 现状：S1 已由空集评审确认；S2 待下一任（未写过字的干净任期）空集评审确认后晋级 S3。接力不换阶段。
- S3 入口（按 §9 主路径）：M1 契约定案落实为可执行定义（字段表 / 必填性 / 校验族 / 规范化 / 错误面文案 / 条款改写文本）；M2–M4 的函数级伪代码（Codec / ChronicleTool / Catalog / Commit / Projection / compose / Tip.fs / 新增读路径）；M5 统一格式的批次表与结构断言脚本设计；M0（016 修复）的具体改动。
- 遗留 ◇（S3 须处置）：交付「恢复 Full」的精确实现路径（blob 重读 vs anchor 回放，见 §10.1）；provider-language 分类裁定（§8.6）；{{rule_count}} 去留与语义；资源键改名（arg-tip → 新名）；中文节名对照表终稿（§2.6-e 草案）；「Nudge」是否保留英文；七字段拼装的分隔形态（小标题 vs 合并段）；身份规范化实现细节（大小写折叠算法）；旧 journal 数据与旧字段名兼容性评估。
- 未实跑边界：016 的「红」仍为静态推定；交付侧无字节界为已读范围内未发现；「重锚后恢复 Full」的源码实现细节未走到。若 S3 需要，可实跑确认。

## 7. S2 · 理解（问题重述）

**输入（现状）**：一条封闭的值域链——chronicle 的 tip 是枚举，值域恰为 101 条规则的 TipName；执行期任何非空串经归一化（trim → 精确 → Levenshtein → LexicalOrder）确定性吸附到库内某条规则，无逃逸（bd [007]）；库以「值域」身份全量注入 blogger 上下文（每条贡献 Name + enforcer.md 全文；按会话语言装载，双语不合并）；TipName = RuleId = FieldName = 目录名，是贯穿落库、RecentTips、Frontier/Coverage、交付查找与 Main 处置索引的身份锚。

**输出（改造后，按 Q3/Q4 收敛）**：同一个 blogger 不再「选」tip，而是「写」tip——两个自由文本字段 opinion（对当前操作的主观批判性评价）与 advice（从经验教训得出的建设性意见）＋一个自由命名「名」字段 tipName（按名去重）替代离散枚举；归一化家族退役；库文本留在上下文但退为 bootstrap 与格式样板（每规则单文档 lesson、两段元素注入）；Main 交付骨架保留（首次 Full / 之后 identity-only / 按名去重）而 Full 正文换源为 blogger 自由撰写的 opinion/advice 两节拼装正文；resources/enforcer 每规则重组为 lesson.md / lesson.zh-CN.md 两文件。

**本质张力（约束的核心）**：改造不是「打开一个字段」，而是把确定性重新安置。旧确定性由三件事承载：①名字空间封闭（枚举值域）；②任何输入归一化到闭集（resolveByField，无逃逸）；③交付正文随时可回查（来自库 main.md）。改造后 ①②退役，③改为「首次交付冻结字节、之后按名回放」——确定性从「名字的解释权」迁移到「名字的身份键 + 冻结回放」。凡依附旧宿主的机制（owner 语言选择 gd [002]、Coverage 身份单位 gd [001]/[003]/[005]、Audience 隔离 gd [008]、双语资源门禁覆盖面）都要显式重设，不能默认延续。

**难点**：
- （a）自由文本第一次成为交付正文主体：Main 收到的 Full 不再有库中正文可查，冻结回放与身份键是唯一保障。收敛（§8.4、§10.1-B）：identity-only 与旧机制同构——名字是字符串身份键兼 Main 的记忆锚（首次 Full 后同名 occurrence 只发「tip: <name>」提醒），形态与机制保留，无需新语义设计。
- （b）注入面改造同时触及装载（loadFor 单语言列表）、合成（compose 只取 EnforcerText）、请求期重拼（ProviderSystemTransform）三处。收敛（§8.2、§1 第 2 条）：维持按会话语言单语装载，每条注入库文本全文（Q1 期为 EnforcerText + MainText 双文档，Q4 后为 LessonText 单文档两段元素，见 §8.2 收敛记录）；双语全灌已淘汰。
- （c）统一格式工作包规模 217 份（en 46 + zh 171）。收敛（§2.6-e、§8.5）：canonical 中文映射为对齐目标，验收 = 结构断言 + 内容覆盖，批次方向已列；残余（「Nudge」保留英文与否等）移入 §6 遗留。
- （d）016 的 120 断言（第 186–187 行）与 101 目录失配。收敛（§9.1 M0）：独立先行修复，改为动态派生（同文件 188 行已有先例）。

**S2 必须回答的问题集**（§6 的展开）：
1. schema 与文案：七字段＋名字字段的标识化拼写、必填/可选、校验族（tryCanonicalText / 字节界）；chronicle 文案重写。
2. 双文本注入：装载/合成/重拼三层改动与语言策略。
3. 归一化退役：resultForTip / resolveByField / Levenshtein / decodeCall 的替代校验；落库与投影身份单位（BlogObservationCommitted、RecentTips、Coverage）。
4. 交付换源：Full 正文形态、identity-only 语义、语言选择、Audience、冻结复用。
5. 统一格式工作包：标准、验收、批次。
6. 规范与测试同步：bd / guidance-delivery 条款与锚点、016 处置。

## 8. S2 · 探索（候选路径）

按 §7 问题集六个工作面。每条候选：思路 / 风险 / 事实依据（§ 引用）。淘汰项附一行理由。§4/§5 已裁决边界不再重开。

### 8.1 工作面一：schema 与文案

- **候选 1a（采纳；经 Q3 裁决收敛）**：两个自由文本内容字段 + 一个 tipName 字段，全部必填 string；字段名 camelCase：opinion / advice / tipName；校验沿既有族（tryCanonicalText trim 非空 + 内容字节界；tipName 另有单行与 String.Length ≤ 200 界，已实施面不变，见 §2.6-n）。
  - 收敛记录：第一轮实施取七字段（main.md 七节 camelCase 化：whatToDoNow…doneWhen）并已全链落地（§2.6-n）；Q3 裁决把内容字段收敛为 opinion / advice 两个——opinion 填对当前操作的主观批判性评价，advice 提出分析经验教训后的建设性意见。未选单一大文本字段（丢失评价/建议的语义分离，§4-E）；未选保留七节（违背用户简明裁决）。
  - 风险：两字段必填加大单轮撰写负担（§4-D 已记录的张力）；字段名一旦发布即为消费者契约。
  - 事实：旧 tip 必填（§2.1）；四内容字段必填、evidence 可选（§2.6-a）；字段名来源 = 用户裁决 Q3（§5-Q3，原 A1 七节来源随收敛退役）。条款落点：bd [006] / [008] / [017]（错误面与离散措辞随本工作面改写，见 §8.6）。
- **候选 1b（淘汰）**：单一大文本字段承载全部教训。理由：丢失节结构，交付拼装无锚、测试不可锚定、样板对齐价值消失（§2.2 的格式基线本身即「按节」组织）。
- **候选 1c（淘汰）**：内容字段可选、缺省跳过。理由：正文残缺，与旧 tip 必填强度不对等；缺节时 Full 正文无法成形（§2.6-c tipFullText 只处理整体空体）。
- 文案侧：description / arg-tip / missing-tip 三组必须重写（现文案均以「从库中选择」为语义，§2.6-a）；内容字段与 tipName 的 schema 描述文案为逐字段显式键 + 成对叶子（Path 常量，无自动派生；缺失由上游库严格拒绝）——Q3 后新增 arg-opinion / arg-advice 两键（第一轮实施的 arg-what-to-do-now 族七键随字段收敛退役，见 §4-E），tipName 用改名后的 arg-tip-name 键（arg-tip 已退役）；{{rule_count}} 语义已结断为「样板库规模」、保留（C5.6）。

### 8.2 工作面二：双文本注入

- **候选 2a（采纳；经 Q4 裁决收敛）**：维持「按会话语言单语」装载，每条注入 Name + LessonText 两段元素（单文档全文；EnforcerRule 以单一 LessonText 取代 EnforcerText/MainText，localizedFiles 单叶映射）。
  - 收敛记录：第一轮实施按 Q1 双灌入（Name + EnforcerText + MainText 三段元素）落地；Q4 裁决把库重组为每规则中英两文件 lesson.md / lesson.zh-CN.md，检测/处置分套失去承载者，双文档注入随之为两段元素注入（lesson 全文承载原 enforcer+main 两侧内容，注入体积与原双文档相当）。
  - 风险：注入字节变化触及冻结与测试（§2.6-d）。
  - 事实：compose 现注入两段元素（§2.6-n 实施现状）；用户裁决 Q4（§5-Q4）。
- **候选 2b（淘汰）**：双语全灌（en + zh 四文档）。理由：体积翻倍；混合语言样板干扰；需重构单语言装载机制，风险大于收益。
- 语言一致性说明：blogger 子会话跟随 owner 语言，撰写字节即交付语言——gd [002] 的「按 owner 语言查库」由此消解（§2.6-c、§2.6-g）。

### 8.3 工作面三：归一化退役与身份

- **候选 3a（采纳）**：resolveByField / Levenshtein 家族整体退役（调用点全列表见 §2.1：Catalog.fs:130 定义、Codec.fs:80、ChronicleTool.fs:104、Surface.fs:221–227 的 JS 语义面 resolveField 包装、Catalog.fsi:22 签名）；替换为「trim 非空 + 身份规范化」；落库 / 投影身份收敛为 TipName。resolveField 处置方向：随家族退役；测试面若需新的 trim/规范化校验语义，由 M2 一并设计，不保留独立解析包装。
  - 风险：旧保证「任何非空格必解析」消失，错误面语义变化；身份字段改名波及落库、投影、交付、条款、测试五面。
  - 事实：调用点全列表已核实（§2.1，含 Surface.fs:221–227 的 JS 语义面包装）；「default 分支不可达」为现状（§2.6-a）；用户裁决 B2（§5-Q2）。条款落点：bd [001] / [007] / [012] / [014] / [015] / [018]（身份与归一化族，见 §8.6）。
- **候选 3b（淘汰）**：保留归一化作「相似规则提示」。理由：无消费者、引入第二语义与第二真相源。
- **候选 3c（淘汰）**：保留 Levenshtein 防误拼。理由：与自由命名直接冲突（自创名被吸附到库名），违背 B2。
- **规范化强度 3d**：采纳 trim + 大小写折叠；原样精确（大小写差异导致重复投递）与空白折叠（边界模糊、过度设计）淘汰；完整 Unicode casefold 亦经 C3.1/C3.2 淘汰，最终实现为简单折叠（.NET ToLowerInvariant）。实现细节（算法与实现载体）见 C3.1/C3.2。

### 8.4 工作面四：交付换源

- **候选 4a（采纳）**：Full = 标题 + tip = "<name>" + 内容字段拼装正文（Q3 后为 opinion/advice 两节；blogger 撰写；落库拼装字节；交付读回 + digest 校验）；IdentityOnly = tip: <name> 保留；owner 语言选择退役（正文 = 撰写字节）；Coverage 身份 = 自由 tipName；冻结机制（anchor MarkerText 原字节回放）原样复用。
  - 风险：交付侧读路径为新增（§2.6-g 确认无既有路径）；「重锚后恢复 Full」的精确实现（blob 重读 vs anchor 回放）需 S3 确认（§10.1 假设 A）。
  - 事实：Full 现源 = rule.MainText（§2.6-c，138 行）；TipGuidanceDelivered / 锚点 / fold 机制齐备（§2.6-c）；blob 写读接口与 digest 校验样例存在（§2.6-f）；裁决 B2 与 §4-C。条款落点：gd [001] / [002] / [003] / [005] / [006] / [008]（交付族，见 §8.6）。
- **候选 4b（淘汰）**：Full 附库参考正文。理由：第二真相源；与「库退出交付正文来源」冲突；owner 语言选择机制被迫保留。
- **候选 4c（淘汰）**：回放原始工具调用文本。理由：形态不受控（无稳定节结构），无法承担 marker 的稳定呈现与冻结回放。
- **落库通道 4d**：内容字段在落库时拼装为「tip 处置正文」字节（Q3 后为 opinion/advice 两节拼装），走新 blob 通道（可仿 evidence 通道模式，§2.6-f），fact 携 ref / digest；交付读回以 digest 校验。不选「与四字段共用同一 blob」：因果主干段落与处置正文是两个语义体，合并后交付侧无法分离。

### 8.5 工作面五：库重组为两文件制（原统一格式工作包，经 Q4 取代）

- **候选 5a（采纳；经 Q4 裁决取代）**：Q1 统一格式工作包已实施完成（404 份零变体，§2.6-n）；Q4 裁决把库重组为每规则中英两文件——resources/enforcer/<TipName>/{lesson.md, lesson.zh-CN.md} 共 202 份，不再分检测（enforcer.md）/处置（main.md）两套。结构：H1 = 「# <TipName> — Lesson」（en/zh 同）；顶层两节 en =「## Opinion」「## Advice」、zh =「## 评价」「## 建议」；子节为原节内容降级为 ### 搬移（映射见 C7.2）；节正文原样搬运、零改写、零丢失；旧 4 文件全部退役删除。
  - 风险：搬移中的信息丢失或改写（验收须断言零丢失）；旧文件删除后装载/注入面必须同步切换，否则断链。
  - 事实：M5 完成后的库基线（§2.2、§2.6-n、§2.6-e 族谱）；用户裁决 Q4（§5-Q4）。
- **候选 5b（淘汰）**：保持四文件制、lesson 内嵌两套原文。理由：检测/处置分套随 Q3 字段收敛失去承载者；四文件并存即第二真相源。
- **变换性质（与内容性重写的区别）**：重组是确定性结构变换（标题降级 ## → ### 与内容搬移），非内容性重写——与仓库「严禁自动化程序批量增删改查程序代码」纪律不冲突：对象是资源文件、正文零改写由 program 自校验断言保证（Algorithm F 的 F2）。
- 验收与批次（S3 展开，Algorithm F）：确定性结构变换规则 → program 自校验（结构断言 + 零丢失覆盖）→ 装载面切换 → 旧 4 文件删除 → 抽样复核。

### 8.6 工作面六：规范与测试同步

- **候选 6a（采纳）**：随各工作面同步更新条款与测试锚点（每步原子闭环）；016 的 120 断言独立先行修复（改为动态派生，同文件 188 行已有先例）。
  - 风险：条款改写文本量大（bd 7 条 + gd 6 条）；「退役断言」（如 bd-007 的 Levenshtein 行为锁）需明确删除而非留空。
  - 事实：条款与锚点清单（§2.3 / §2.4）；016 硬编码现状（§2.6-e、D3）。
- **候选 6b（淘汰）**：代码先行、条款测试后置集中改。理由：中间态条款-实现矛盾，验收不可锚定。
- 改写清单（草案；逐条覆盖 §2.3 全量 19 条（[001]–[019]），无静默遗漏；条款面完整性经本轮 devops 全仓扫描复核——53 套件中五类语义落点仅在 bd/gd，无第三套件）：bd [001]（身份语义）、[004]（注入内容）、[003]/[005]（Q4 后随库结构改写：[003] 目录与单叶装载校验、[005] 单叶完整性——每规则恰中英两叶、每叶 LessonText trim 非空；身份三元组保留，落点 Algorithm F）、[006]（校验与错误面）、[007]（Levenshtein 退役）、[008]（「离散的单条 tip 选定」重述为单条 lesson 的字段组）、[009]（不涉及——每 run 恰一次 chronicle 与 tip 语义无关）、[011]（内容硬界文本扩展：512 KiB 界覆盖对象为「因果主干合并段与两节拼装正文」，见 C2.2/C6.1）、[012] / [014]（身份字段收敛）、[015]（tip 指代更新为三字段组 tipName + opinion + advice + normalize 同名判定；配对视图与「禁假 tip」语义保留）、[016]（RecentTips 协同裁剪语义在 P2 字段收敛下保留；文本评估与 G5/G1 关系注明，见 C6.1）、[017]（无效 terminal 条件清单更新：缺 tipName、opinion/advice 任一缺失/空白、tipName 超长或含换行、新旧协议混用判定）、[018]（冻结对象重设）；gd [001]（Coverage 身份）、[002]（正文来源与语言）、[003] / [005]（措辞级改写：原文「规则全文」（[003] 第 13 行）「已交付规则」（[005] 第 21 行）的指称物改为「该 occurrence 绑定的处置正文」一类表述；Frontier/Coverage 双轴与重锚恢复语义保留）、[006]（载荷读取替代查库）、[008]（Audience 重述）；gd 全集 11 条（编号无 [010]）经复核评估为不需改文本的：[004]（通用交付判定——新方案仍只依赖持久事实投影；测试 004 仅断言 presentation 恢复与 owner 隔离）、[007]（latestTipNudge → latestTipGuidance 纯委托结构（Tip.fs:197）保证字节相等，与正文来源无关）、[009]（权限语义与正文来源无关；测试 009 仅锚注入通道机械不变量）、[011]（marker 通道字节冻结机制原样复用，与正文来源解耦）、[012]（动态材料合成与冻结语义仍适用；附 M4 交互点——tip 材料单次读取在 PairProgrammingThoughtTransform.fs:1336、冻结点 1089–1098、重放不重渲染分支 1081–1085，「恢复 Full」若走 blob 重读必须落在单次读取内；行为证明为既有 todo GAP-116）。落点：schema 族（bd [006] / [008] / [017]）→ §8.1；身份族（bd [001] / [007] / [012] / [014] / [015] / [018]）→ §8.3；注入族（bd [004]）→ §8.2；交付族（gd 各条）→ §8.4。
- provider-language 处置（本轮 devops 核实；分类裁定点，非自动改写）：[005]/[006]/[008] 无条款点名交付正文（[006] 资源文件门禁不覆盖；Tip.fs:32–35 标题串为代码常量、本就不在资源机制内）；但 [005]「完成文案」与 [013]「进入模型的 Class A 文本」字面可覆盖交付 marker 文本。裁定两分支：属 Class A → [009]「所有 Class A 文本通过统一资源机制装载」与 [013]「全局设置变化立即生效于下一次渲染」需补充/修订（与冻结回放的冲突消解）；不属 → 整套保留。裁定点列入 §9.2 边界待裁决点（S3）。
- 全仓扫描结论（本轮 devops，53 个套件 WHAT.md）：五类语义（tip 归一化、TipName 三元组、Full 正文来源、owner 语言、冻结对象）的条款落点仅在 behavior-diagnosis 与 guidance-delivery；cognitive-environment 的「Rulebook」为 Office Library 分类词，无关。

## 9. S2 · 主路径（分解、边界、顺序）

**主路径**：契约先行、双链并行。线 A（代码与契约）：契约定案 → 后端契约落地 → 提示面与交付面 → 收尾；线 B（资源）：统一格式工作包与线 A 并行；016 独立先行修复。确定性从「名字解释权」迁至「名字身份键 + 冻结回放」，库文本退为 bootstrap 与样板。

### 9.1 分解（步骤与依赖）

- **M0. 016 先行修复**（独立小修）：硬编码 120 改为动态派生，恢复绿。无依赖。
- **M1. 契约定案**（设计产出，按 Q3/Q4 修订）：字段表（opinion / advice + tipName，含各自描述文案键）、必填性与校验族（opinion/advice trim 非空；tipName 校验沿用已实施面不变）、tipName 规范化（trim + 简单大小写折叠，C3.2）、错误面文案、文案稿（description / arg-opinion / arg-advice / arg-tip-name / invalid-tip，中英）、条款改写文本草案。依赖：无（§7/§8 已就位）。
- **M2. 后端契约落地**（按 Q3/Q4 修订；归一化退役、身份与 blob 通道等已实施面沿用，见 §2.6-n）：ChronicleTool（schema 收敛为 tipName + opinion + advice + evidence；resultForTip 校验改两内容字段）；Codec（decodeCall 内容字段解码改 opinion/advice 两节；tipName 解码不变）；Model.renderTipSections（节名表改两节，见 C7.3）；Catalog/装载（EnforcerRule 单一 LessonText 取代 EnforcerText/MainText，localizedFiles 单叶映射）；chronicle 文案资源（新增 arg-opinion / arg-advice，退役 arg-what-to-do-now 族七键）。依赖：M1。
- **M3. 提示面落地**（与 M4 可并行）：compose 两段元素注入（Name + LessonText）；EnforcerRule 装载面单叶化；blogger role 提示词与文案同步（自由命名与 opinion/advice 两节语义）。依赖：设计输入取自 M1（契约定案）；执行起点在 M2 之后，与 M4 同为 M2 后的独立分支（M3 ∥ M4 自 M2 落地起成立）。
- **M4. 交付面落地**（与 M3 可并行；已实施，Q3/Q4 下增量 = gd-002 拼装断言随两节节名表同步）：新读取路径（RecentTip → fact → 新 blob ref（tip 处置正文，见 §8.4-4d）→ Read + digest）；Tip.fs Full 组装改源；语言选择退役；gd 条款与测试更新。依赖：M2。
- **M5. 库重组工作包**（线 B，与 M2–M3 并行；原统一格式工作包已实施完成并由 Q4 取代）：确定性结构变换规则定稿（Algorithm F）→ 404 份旧文件变换为 202 份 lesson 文件 → program 自校验（结构断言 + 零丢失覆盖）→ 装载面切换 → 旧 4 文件退役删除 → 抽样复核。依赖：M1（lesson 结构契约；实质独立）。
- **M6. 收尾**：全链测试（bd / gd 两套 + prefix-stability 相关）；条款一致性复查；残留清理。依赖：M2–M5。

### 9.2 边界（每步触碰与不触碰）

- 触碰文件族：src/Wanxiangshu/OpenCode/Tools/ChronicleTool.fs；src/Wanxiangshu/Enforcer/{Codec, Catalog, Cycle/Decode, Cycle/Model, Cycle/Commit, Projection, Guidance/Tip, Guidance/TipSurface（新增 blob 写入助手；fsi 同步新增 val，见 Algorithm I 的 I5.2）, Guidance/DeliveryProjection, Surface}.fs（resolveField 包装实现 221–227 行）及 src/Wanxiangshu/Enforcer/Surface.fsi（公开签名 34 行；包装退役，见 §8.3；新读路径暴露与否留在边界待裁决点）；src/Wanxiangshu/Enforcer/ChronicleExecution.fs 与 ChronicleExecution.fsi（校验族扩展：tipName 判定序与 C4.1 错误标识，见 Algorithm S 的 S4/S6）；src/Wanxiangshu/Enforcer/Observation.fs 与 src/Wanxiangshu/Enforcer/ObservationSurface.fs（观察视图两文件：域面 tipIdentities 26–31、observationsOf 37–41、observationsAfterSquash 45–52；JS 面 cycleRecordOfJs 168–181、tipToJs 99–103、recentTips 238–242、observationsOf 294–305；ObservationSurface.fsi 为 obj 签名、不变）；src/Wanxiangshu/Enforcer/BlogSurface.fs（observationPayload 604–625、buildProjectionPlan 712–737）；src/Wanxiangshu/Enforcer/Cycle/Recovery.fs（previousTipsOf 105–112）；src/Wanxiangshu/Context/Companion/{ProjectionSurface.fs（projectionInput 139–169 的 previousTips 键）、Builder.fs（L37 注释随动）、Prompt.fs（28–29 转发）、Blogger/Toml.fs 与 Blogger/TomlSurface.fs（随动评估）}；src/Wanxiangshu/Resources/EnforcerCatalogResource.fs；src/Wanxiangshu/OpenCode/Host/ProviderSystemTransform.fs（仅当合成变化所需）；durable 载荷面（新 blob 通道与身份字段）：src/Wanxiangshu/Context/Companion/Facts.fsi / Facts.fs（BlogObservationCommitted 定义；包装 Composition/Durable/CompanionFact.fs）；其序列化/采集链——Persistence/Journal/EventStoreJournalWriter.fs（blob ref/digest 采集点，154–157 行，新增字段须进入）、Envelope.fs（类型形状驱动的自动解码）、FactCodec.fs（legacy 检查）随字段形态评估；resources/provider/tool/chronicle/**；resources/provider/role/blogger/**；resources/enforcer/**（M5）；requirements/behavior-diagnosis/WHAT.md、requirements/guidance-delivery/WHAT.md；behavior-diagnosis 与 guidance-delivery 两套 tests/*.test.mjs 及其 support 支撑文件（requirements/behavior-diagnosis/tests/support/cycle.mjs，见 Algorithm D 的 D5.6；requirements/guidance-delivery/tests/support/journal.mjs 增补 blob 物化能力，见 Algorithm I 的 I5.2）；requirements/context-compression/tests/{012,014,018}.test.mjs（previousTips 键字面替换，见 Algorithm P 的 P9）。
- 不触碰：冻结机制语义（PairProgrammingGuidelineAnchored / MarkerText 回放原样复用）；DeliveryProjection 双轴结构；evidence 字段与通道；bd [009]（每 run 恰一次）等与自由化无关条款；不引入第二真相源；不引入数值评分；AgentJournal.fsi/fs 本体（读能力经既有 Writer.BlobWriter.Read，见 R1）；旧运行数据不做迁移（兼容性仅评估，◇）。
- [015] 处置：配对视图与禁假 tip 语义保留；文本与实现随字段收敛同步（tip 指代更新、Observation.fs 与 ObservationSurface.fs 收敛、bd-015 两处 fixture），见 C6.1、C6.6 与 K6.1/K6.2。
- 边界待裁决点（S3）：ProviderSystemTransform 是否随合成变化改动（compose 签名不变则不动）；Surface.fs 测试面是否需暴露新读路径（另见 §8.3 resolveField 包装退役）；durable 载荷承载点（BlogObservationCommitted 新 ref/digest 字段 vs 新 fact 类型）；旧 journal 数据兼容处置；provider-language 分类裁定（交付 marker 文本是否属 Class A，两分支后果见 §8.6）。

### 9.3 顺序（串行与并行）

- 串行主链：M1 → M2 →（M3 ∥ M4）→ M6。
- 并行线 B：M5 自 M1 后随时可起，在 M6 前完成。
- 独立先行：M0。
- 理由：M1 是字段 / 文案 / 条款的单一契约源（防并行漂移）；M2 是 M3 执行测试面与 M4 接口的共同底座；M3 与 M4 文件族不重叠、依赖已满足；M5 只动资源内容，与代码无依赖，验收在 M6 统一；M0 与一切无耦合，先绿为净。

### 9.4 验收面（步骤 ↔ 锚点）

- M0：bd-016。
- M1：设计评审（S3 输入）。
- M2：bd-001/003/006/007/008/012/014/015/017/018（改后语义；含 Surface.fs resolveField 包装退役的测试面影响；008 的 Codec/解码断言面在此）+ chronicle 文案断言 + cc-012/014/018（previousTips 键字面替换，见 Algorithm P 的 P9）。
- M3：bd-004/009/016（注入面）+ gd-008（装载/composer 面，逐块口径见 Algorithm I 的 I5.1–I5.3）。
- M4：gd-001/002/003/005/006/007/008/012 + prefix-stability [010]/[011]/[014] 相关测试（004/009/011 与正文来源解耦，见 §8.6）；gd [003]/[005] 措辞级改写后核对对应断言（重锚恢复语义见 §8.6）。
- M5：lesson 结构断言 + 零丢失覆盖 + 抽样复核（验收口径随 Algorithm F 更新为重组工作包）。
- M6：两套全链。

## 10. S2 · 检验与反思（反事实与目标核验）

### 10.1 关键假设与证伪条件

- **假设 A（冻结回放承载自由文本足够）**：证伪条件——重锚或重启后，同一 occurrence 的 Full 字节不可得或不一致。论据：anchor MarkerText 按 occurrence 落盘、原字节回放（§2.6-c）；gd [011]「历史重放使用这些字节，不随规则版本改写」（§2.6-d）。残余风险 ◇：「重锚后恢复 Full」（gd [005]）的精确实现路径未走到——若它需重新组装正文而非回放旧字节，则 blob 重读路径是硬的；两分支都不改变路径成立，但影响 M4 细节（S3 确认）。若证伪：「每次重新拼装」破坏原字节冻结（[010]）不可接受；备选为「首次注入时把正文字节同时写入 anchor 与档案，恢复以 anchor 为准」。
- **假设 B（按名去重对自由命名足够）**：证伪条件——blogger 频繁发明新名导致 Coverage 永不命中、重复 Full。影响面：重复注入体积与语义噪声（正确性不损）。缓解：注入样板提供命名参照（M3）；大小写折叠归一（M1，C3.2）。若证伪严重：接受重复 Full；或在 role 提示词加「优先复用已有名字」语句（可选，S3 定）。
- **假设 C（双文本注入不破坏前缀稳定）**：证伪条件——注入字节变化引发 revision 冻结不一致或前缀断裂。论据：注入公式固定、内容经 RulebookRevision 冻结（bd [018] 需重设冻结对象）；prefix-stability [010] 只管 guidance 回放、不管 prompt 内容。残余风险 ◇：[018] 的冻结对象重设范围（枚举退役后为何物）S3 定。

### 10.2 步骤失败分支

- M2 若发现 resolveByField 的第三处消费者：暂停退役，先全仓只读扫描（devops）列全调用点，再调整 M2 范围。已触发并结清（本轮 devops 核实第三处为 Surface.fs:221–227 的 resolveField 包装；处置方向见 §8.3）。
- M2 若身份字段改名引发旧 journal 读失败（回放 / 投影）：评估向后兼容；若不可行，如实记录兼容边界（本机运行数据、无跨机承诺）并交用户裁决。
- M3 若 prompt 体积超预算：退为「MainText 抽样注入」违背「全量灌入」裁决，需用户裁决而非自行降级。
- M4 若 blob 读路径在交付侧装配不可达：备选「首次 Full 时把正文写入 anchor 档案，恢复以 anchor 为准」（见假设 A 备选）。
- M5 若搬移中信息不变量与结构映射冲突：按 F1 的确定性映射消解（以零丢失为前提）；无法消解的个案记录待审并由 V 复核，断言不放宽、不豁免。
- M6 若全链测试暴露条款-实现矛盾（如 gd [008] Audience 与双灌入的紧张）：回 M1 契约表重新裁决该条并同步。

### 10.3 目标状态核验（§1 逐条）

1. schema 去枚举、opinion/advice + tipName → M1 / M2。
2. 全量注入（每规则两段元素 Name + LessonText；语言单语为设计决定，注记于 §8.2）→ M3。
3. 归一化退役、身份按名 → M2。
4. 交付链骨架保留、正文换源 → M4。
5. 库重组为两文件制（202 份 lesson）→ M5。
6. 条款与测试同步 → 各步内嵌 + M6 复查；另含 provider-language 分类裁定（§8.6）。

### 10.4 表象反思

- 本路径是否只治「字段替换」的表象？否：确定性迁移（名字身份键 + 冻结回放 + digest 校验）与新读路径是结构性改动；库的角色转变（值域 → 样板）在 M3 / M5 落为字节。
- 最危险的「新表象」：重组若在搬移中改写或丢失内容，将得到失真的库——验收必须以「结构断言 + 零丢失覆盖 + 抽样复核」三重门禁（Algorithm F），断言对内容漂移变红。
- 本路径不解决的（如实记录）：自由撰写的质量下限（用户已接受 B2 的代价）；旧运行数据的迁移；命名稳定性的长期统计。

# S3 · 流程与伪代码

本节把 §9 主路径写成可执行流程。算法头给出输入与输出；步骤以字母加数字标号；▷ 为裁决点，结论与理由就地在案；异常路径写成步骤。本节逐段累积，未写段落见 S3.0 索引。

## S3.0 算法索引与执行顺序

算法与里程碑的映射（算法字母已定，后续段不再改名；S3.k 编号为写作顺序）：

| 里程碑 | 算法 | 内容 | 状态 |
|---|---|---|---|
| M0 | Algorithm A | 016 测试硬编码修复 | 已写 |
| M1 | Algorithm C | 契约定案（字段 / 校验 / 规范化 / 错误面 / 文案 / 条款验收 / lesson 结构） | 已写 |
| M2 | Algorithm S | ChronicleTool：schema 替换与执行期校验 | 已写 |
| M2 | Algorithm D | Codec：tipName 与内容字段解码与校验 | 已写 |
| M2 | Algorithm L | Catalog：归一化退役与 Surface 包装处置 | 已写 |
| M2 | Algorithm K | Commit：TipName 落库与处置正文 blob 通道 | 已写 |
| M2 | Algorithm P | Projection：RecentTip 字段收敛与 previousTips 链 | 已写 |
| M3 | Algorithm I | composeBloggerSystemPromptFor 两段元素注入 | 已写 |
| M3 | Algorithm B | blogger role 提示词改写 | 已写 |
| M4 | Algorithm R | 交付读路径：载荷 → blob 重读 + digest 校验 | 已写 |
| M4 | Algorithm G | Tip.fs Full 组装、语言选择退役、冻结交互 | 已写 |
| M5 | Algorithm F | 库重组工作包：确定性变换与自校验 | 已写 |
| M6 | Algorithm V | 收尾验收：全链测试与条款一致性复查 | 已写 |

执行顺序：Algorithm A 独立先行；Algorithm C 是其余算法的契约源；M2 五算法按 S → D → L → K → P 落地；M3 与 M4 在 M2 落地后并行（Algorithm I 与 Algorithm R/G 并行，M4 内 R 先于 G）；Algorithm F 与代码线并行、在 V 前完成；Algorithm V 最后。写作按里程碑顺序推进，一段可含同一里程碑内的一至多个算法。

实施现状注记（Q3/Q4 修订轮）：上表 M1–M5 的七字段版与 M5 统一格式已全链实施（§2.6-n）；各算法按 Q3/Q4 裁决就地修订——C1/C2/C4/C5/C6/C7、S1/S2/S4/S6、D1/D3/D4/D5 连带、I1/I2/I5、B2/B3、F（整体改写为库重组工作包）、V1/V4 扩充；L/K/P/R/G 的已实施机制（归一化退役、身份与 blob 通道、previousTips 链、读路径、语言退役）不受 Q3/Q4 影响、维持原貌。

遗留 ◇ 归属（§6 清单）：恢复 Full 实现路径 → Algorithm R/G 的 ▷；provider-language 分类裁定 → Algorithm G 的 ▷；{{rule_count}} 去留、arg-tip 改名、拼装分隔形态、Nudge 保留英文、对照表终稿、大小写折叠实现 → 本段 Algorithm C（已结断）；旧 journal 数据兼容 → Algorithm C 定原则（C8.3）、Algorithm R/G 落行为。
§9.2「边界待裁决点（S3）」五条全部结断（其中三条不在 §6 清单）：ProviderSystemTransform 是否随合成变化改动 → I4（不动，compose 签名不变）；Surface.fs 测试面是否需暴露新读路径 → R5.1（不新增公开符号）；durable 载荷承载点 → K1.1（改既有 fact，非新 fact 类型）；旧 journal 数据兼容处置 → C8.3（不迁移原则）+ K2（兼容读）+ G5（identity-only 行为）；provider-language 分类裁定 → G7（不属 Class A，整套保留）。

## S3.1 Algorithm C [契约定案]

输入：用户裁决（Q1：字段名取 main.md 七节；A1+A2 双灌入；101 条统一格式——已被 Q3/Q4 收敛取代。Q2：自由命名、同名去重。Q3：内容字段收敛为 opinion/advice。Q4：库重组为每规则中英两文件 lesson）；S2 六工作面结论与主路径（§8/§9）；仓库事实（§2，本任评审前经 devops 抽样复核；实施现状见 §2.6-n）；S3 遗留 ◇ 清单（§6）。
输出：定义 C1–C8，作为 M2–M5 各算法与条款改写的单一契约源。

C1. [字段表]
C1.1. 定内容字段名 ← ⟨opinion, advice⟩（加 tipName 共三字段）。 # 收敛记录：第一轮实施取八字段 ⟨whatToDoNow, whyThisMatters, repairStrategy, decisionBranches, commonWrongFixes, verification, doneWhen, tipName⟩ 并已全链落地（§2.6-n）；Q3 裁决把内容字段收敛为两个——opinion 填对当前操作的主观批判性评价、advice 提出分析经验教训后的建设性意见。未选单一大文本字段：丢失评价/建议的语义分离；未选保留七节：违背用户简明裁决。
C1.2. 定内容字段与 lesson 顶层两节一一对应：opinion ↔ Opinion（zh 评价）；advice ↔ Advice（zh 建议）。 # 拼装节名表见 C7.3，同一真相。
C1.3. [命名形态] ▷ 候选：camelCase；kebab-case。结论：camelCase。理由：与 chronicle 现有字段（charge / occurrence / settlement / consequence / evidence）风格一致；provider 工具的 JSON 参数名惯用 camelCase。
C1.4. [名字字段命名] ▷ 候选：tipName；name；tip。结论：tipName。理由：tip 与旧枚举字段同名会造成新旧混淆；name 语义过泛；tipName 在「tip 的自由名」语义上唯一。
C1.5. [必填性] ▷ 候选：三字段全部必填；部分必填。结论：全部必填。理由：与旧 tip 必填对等；Full 正文由两节拼装，缺节即残缺，且库已退为样板、无法补全。已知代价（接受）：单轮撰写负担增大（§4-D 已记录）。
C1.6. 旧 tip 枚举字段退役：新 schema 不含枚举 tip；执行期与解码期不再把任何非空 tip 串归一化映射到库内规则（resolveByField / Levenshtein 家族退役，步骤见 Algorithm L）。 # 八字段与旧 tip 无并存期。
C1.7. [字段顺序] 定 schema 字段顺序 ← charge、occurrence、settlement、consequence、tipName、opinion、advice、evidence。理由：因果脊 → 教训命名 → 评价与建议两节 → 证据；evidence 沿用现状（可选 string），不参与本次改造。

C2. [校验族]
C2.1. 定单字段校验 ← trim 后非空（沿用 tryCanonicalText 现族），适用内容字段（opinion/advice）与 tipName。
C2.2. 定正文界 ← MaxBlogTextBytes = 512 KiB（沿用）；校验对象 = 因果主干合并段（MergedText）与两节拼装正文（TipSectionsText）两者；承载 = validateContentBounds/validateBounds 扩展（Decode.fs:242 生产调用点、Surface.fs:209 JS 面包装同步）；超限 = fail-closed 拒绝（沿用 contentBoundsError 字符串错误；cycle 不成立、不落 blob；不映射 invalid-tip 工具文案，见 D4.2、K3）。 # 界为 fail-closed 拒绝，不是截断。
C2.3. 排除 MaxInlineEvidenceChars = 1024（evidence 专属）；不新增内容单字段字符界（现状内容字段亦无）。
C2.4. [tipName 界] ▷ 候选：不设界（仅非空）；设「单行、200 字符」界。结论：设界；「字符」计量单位 = .NET String.Length（UTF-16 code units；与 MaxInlineEvidenceChars=1024 同族——Codec.fs:58 定义、L109 判定；与 512 KiB / 128 KiB 族的 UTF-8 字节计数相区分，后者经 SyntheticToml.byteCount 链用于正文体量）。理由：tipName 以原串进入交付 marker（「tip: <name>」与「tip = "<name>"」）与投影展示面；换行破坏行结构，200 字符封住病态超长名；与「名字应短」的语义相称。
C2.5. 定校验顺序：先 trim；再查不含 \n 与 \r；再查字符数（String.Length）≤ 200；最后取非空。
C2.6. 定类型校验：八字段均须为 string；非串在解码期拒绝（沿用现状语义，见 Algorithm D）。
C2.7. 若任一字段级校验失败，则转 C4 错误面；字节界（C2.2）失败走 decode 阶段 contentBoundsError（D4.2），不进 C4；两类均不截断、不静默跳过。 # fail-closed。

C3. [身份与规范化]
C3.1. 定 normalize(s) ← trim(s) 后做大小写折叠（.NET ToLowerInvariant，不变文化）。
C3.2. [规范化实现] ▷ 候选：完整 Unicode casefold（含 ß→ss 等展开）；简单大小写折叠；原样精确。结论：简单大小写折叠 + 首尾 trim（§8.3 的「Unicode casefold」由此收敛为本条的简单折叠）；不做 NFKC、不做空白折叠、不做重音折叠。理由：命名以 ASCII 与中文为主，大小写是唯一实际风险；完整 casefold 引入数据表与 Unicode 版本敏感行为，超出收益；空白与重音折叠无证据支持（过度设计）。
C3.3. 定存储与比较分离：落库、投影、交付一律存与展示 tipName 原串（decode 期 trim 后的撰写字节，见 C3.4/D2）；一切同名比较点（Coverage 判定、交付同名判定——消费最近 tip 的场合）经 normalize 后比较；RecentTips 列表层不去重（P2），集合型落点的键形态见 G4.2。
C3.4. [存原串还是规范形] ▷ 候选：存规范化名；存原串。结论：存原串。原串 = decode 期 trim 后的撰写字节（保留大小写与内部空白；不做枚举归一与大小写变换；见 D2）。理由：展示忠实于撰写者；规范化函数演进时历史字节不漂移（与 gd [011] 历史字节精神一致）；normalize 是纯函数，无存储成本。Coverage 集合为内部比较结构、以规范化键存储（G4.2），不属本条展示面。
C3.5. 定同名语义：normalize(a) = normalize(b) 即同名；同名时交付走 identity-only。 # 大小写或首尾空白之差视为同名。

C4. [错误面]
C4.1. 定内部错误标识 ← ⟨missing-tip-name, missing-tip-field:<field>, tip-name-too-long, tip-name-multiline, tip-field-not-a-string⟩；测试锚定。产生面分层（与 S4.1 的读取原语裁决一致）：missing-tip-name / missing-tip-field:<field> / tip-name-too-long / tip-name-multiline 由执行期与解码期两期产生；tip-field-not-a-string 仅由解码期产生（执行期读取面 HostToolArguments.Text 把非 string 归 ""、落 missing-tip-name，见 S4.1）。<field> ▷ 候选：camelCase 字段名；本地化节名。结论：camelCase 字段名（与 C1.1 同源，如 missing-tip-field:opinion / missing-tip-field:advice）。理由：与 provider 字段名同源、测试锚定不随会话语言漂移；内部标识无展示消费者。
C4.2. [文案面] ▷ 候选：每个标识各配文案键；单一键覆盖全部无效 tip。结论：单一键。理由：blogger 是唯一消费者，修复动作相同（补齐名字与两节）；文案面最小。invalid-tip 文案只作用于工具执行期的 blogger 面；解码期返回 C4.1 内部标识本身、不映射文案（D2/D5.2）。
C4.3. [键名] ▷ 候选：保留 missing-tip；改名 invalid-tip。结论：invalid-tip。理由：错误面从「缺失」扩为「缺失或不合规（缺节、超长、含换行）」，键名与语义对齐；旧键无外部消费者（§2.6-a 已核实引用面）。
C4.4. 定 invalid-tip 文案：en「The tip is invalid: give it a short name and write both the opinion and the advice.」；zh「这条 tip 无效：请给出简短的名字，并写好评价与建议两节内容。」。
C4.5. 保留 remembered 与 nothing-to-remember 两键原义（§2.6-a 已核实不涉 tip 语义）。

C5. [文案资源]
C5.1. 新增两键（en + zh-CN 成对叶子）：tool/chronicle/arg-opinion、arg-advice。 # 第一轮实施的新增七键 arg-what-to-do-now / arg-why-this-matters / arg-repair-strategy / arg-decision-branches / arg-common-wrong-fixes / arg-verification / arg-done-when 及其 Path 常量随 Q3 退役（退役面见 S6/V4）。
C5.2. 改名 arg-tip → arg-tip-name；退役旧键 arg-tip。 # 键无外部消费者。
C5.3. 定内容字段描述文案（en；落资源文件时允许风格级微调，语义不得变更）：
  arg-opinion:「Your critical opinion of the current operation: judge frankly what this concrete transition did well or badly, and why.」
  arg-advice:「Constructive advice drawn from this transition's lesson: what to do differently next time, stated concretely.」
  arg-tip-name:「A short free-form name for this tip. Reuse the same name when the same lesson recurs; names equal up to case and surrounding spaces are the same name.」 # 原义保留。
C5.4. 定内容字段描述文案（zh-CN；与 C5.3 一一对应，同微调约定）：
  arg-opinion:「对当前操作的批判性评价：坦率判断这次具体转折做得好在哪里、差在哪里，为什么。」
  arg-advice:「从这次转折的经验教训中得出的建设性意见：下次具体该怎么做，要落到实处。」
  arg-tip-name:「为这条 tip 自由命名，保持简短。同一教训再次出现时沿用同一个名字；只有大小写或首尾空白不同的名字视为同名。」 # 原义保留。
C5.5. 改写 description：en「tipName, opinion and advice are required: name this reusable lesson freely, give your critical opinion of the current operation, and offer constructive advice from its lesson; the {{rule_count}} Rulebook entries are format samples to borrow from, not a closed set.」；zh「tipName、评价与建议均为必填：请自由命名这条可复用教训，对当前操作给出批判性评价，并从教训中提出建设性意见；上下文中的 {{rule_count}} 条 Rulebook 条目是格式样板，不是必须选择的封闭集合。」
C5.6. [{{rule_count}}] ▷ 候选：删除；保留并改语义为「样板库规模」。结论：保留。理由：数值真实（当前 101）；对 blogger 是样板规模的有用信号；语义更新只在文案内完成，无机制改动。
C5.7. [blogger role 提示词] 定改写要点（文本随 Algorithm B 落地）：① 删除「Choose exactly one Rulebook TipName」类措辞；② 明确 tipName 自由命名、可自创新名、同名沿用；③ 明确 opinion 与 advice 两节自由撰写（评价与建议各司其职）、库为 bootstrap 与格式样板；④ 保留「tip 是可复用教训、具体字段描述这次具体转折」的因果脊语义（原「the other four fields」措辞随字段拆分更新为现字段指代）。
C5.8. 定资源契约：新增字段文案须新增显式 Path 常量与成对叶子；缺键导致资源读取失败（上游严格配对，无 fallback）。 # 启动期契约，非错误面。

C6. [条款改写验收]
C6.1. 逐条改写要点（文本按 C6.6 落点表随对应算法同步定稿；以下为验收标准）：
  bd [001]：保留目录 basename = RuleId = FieldName 的库内单一真相；删除「tip 必归一到 TipName」；新增「tip 身份 = blogger 自由命名 + normalize 比较」。
  bd [003]：改写（Q4）——目录打包与装载校验随库单叶化更新：localizedFiles 单叶映射（English → lesson.md、zh-CN → lesson.zh-CN.md）、每规则 LessonText trim 非空；目录 basename = TipName = RuleId 身份三元组保留，作身份面回归。
  bd [005]：改写（Q4）——本地化叶子完整性改为单叶合同：每规则恰中英两叶、每叶 LessonText trim 非空；TipName / RuleId 身份保留（与 bd [001]/[003] 同列）。
  bd [004]：注入内容改为「基础 Blogger prompt、# Enforcer Rulebook 标题、全部规则的 Name 与 LessonText 两段按 LexicalOrder 拼接（渲染沿用既有注释机制：逐元素 `# ` 前缀、元素间单换行；rule.Name 为裸目录名元素）」。
  bd [006]：校验与错误面改为 C2 校验族与 C4 单一 invalid-tip 文案（字段列举随 C1.1 定稿：tipName、opinion、advice）；删除「无逃逸、必解析」表述。
  bd [007]：归一化映射条款整体删除（resolveByField、Levenshtein 最近邻、并列取最小）；不保留同义条款；不误伤 LexicalOrder 的排序用途。 # 排序用途与归一化无关。
  bd [008]：「离散的单条 tip」重述为「单条 lesson 的三字段组（tipName + opinion + advice）同批提交」；数值评分与桥接字段禁令保留。
  bd [011]：内容硬界文本扩展——原文「新协议四句正文」的 512 KiB 界覆盖对象更新为「因果主干合并段（MergedText）与两节拼装正文（TipSectionsText）」，与 C2.2 的 validateBounds 扩展一致；inline evidence 1024 字符界与「独立 evidence 通道为空」不变。
  bd [009]：不改。
  bd [012]：原子提交对象改为「TipName 原串 + 处置正文 ref/digest」（字段细节见 Algorithm K）。
  bd [014]：RecentTip 容量 8 保留；字段收敛为 ⟨TipName, CycleId⟩（见 Algorithm P）。
  bd [016]：RecentTips 协同裁剪语义保留——squash 时按 1:1 共同裁剪最老 min(K, tips) 条 RecentTips、不新增 tip、不触发新交付；P2 字段收敛后裁剪单位随 ⟨TipName, CycleId⟩；文本不涉字段名故可不改，但须注明「无处置正文 ref 的旧 occurrence 不触发新 Full（G5 identity-only）」与「不新增 tip、不触发新交付」同向。
  bd [015]：tip 指代更新为「三字段组（tipName + opinion + advice）+ normalize 同名判定」；配对视图与禁假 tip 语义保留；配对视图实现随字段收敛同步（Observation.fs:26–31 改取 TipName、ObservationSurface.fs:168–181 输入面与 99–103 输出面收敛，见 K6.1/K6.2）。
  bd [017]：无效 terminal 条件更新为「缺 tipName、opinion/advice 任一空白、tipName 超长或含换行、新旧协议混用」。
  bd [018]：冻结对象重设为 ⟨字段表与必填性、校验族版本、normalize 算法版本、库目录集与文本⟩；删除旧三项（chronicle.tip 枚举、解码映射表、Main 处置索引）。
  gd [001]：Coverage 身份单位改为 normalize(tipName)；Frontier 按 occurrence 保留。
  gd [002]：Full = 标题 + tipName 原串 + 处置正文（撰写语言）；删除 owner 语言查库选择。
  gd [003]/[005]：措辞级——「规则全文」→「该 occurrence 绑定的处置正文」；「已交付规则」→「已交付处置」；双轴与重锚恢复语义保留。
  gd [006]：载荷读取改为「从 occurrence 载荷取处置正文 ref 读回」，删除 tryFindByField 查库。
  gd [008]：Audience 按三分语义重述（Q4 后）——库单文档（lesson）作为样板进 Blogger 上下文、退出交付正文来源（检测/处置分套已随库重组消失，lesson 内子节不再区分 audience）；交付正文（撰写 opinion/advice 两节）仅 Main；「Blogger 历史为低信任观察」定性保留（previousTips 注入面语义见 Algorithm P 的 P6–P10）。 # 「处置正文」指称物为撰写正文；双灌入的样板例外由单文档 lesson 容纳。
  gd [004]/[007]/[009]/[011]/[012]：不改文本；其中 [011] 冻结机制与 [012] 动态材料语义原样复用。
C6.2. provider-language 分类裁定（交付 marker 文本是否属 Class A）转 Algorithm G 的 ▷；两分支的改文本范围已定：属 Class A → 修订 provider-language [009]/[013]；不属 → 整套保留。 # 本算法唯一转出项。
C6.3. 全仓条款检索验收：改造后对五类语义（tip 归一化、TipName 三元组、Full 正文来源、owner 语言、冻结对象）做只读扫描，确认落点仅在 bd 与 gd 两套。 # 复核 §8.6 结论。
C6.4. [018 锚点] 定 bd-018 测试锚点改造要点：018.test.mjs 现断言（fieldNames == rules.map、tryFindByField 一致性、decodeCall 携带 tip 的一致性）改为——① 目录身份与字段表一致性断言保留；② enum 冻结视图断言改为新语义：新三字段（tipName + opinion + advice）下 decodeCall 合规（tipName 保留 trim 后原串、内容字段解码）与缺 tipName/旧形态的稳定拒绝；③ tryFindByField 保留作身份面回归（与 bd [003] 一致），其一致性断言沿用。 # RulebookRevision 在代码中不存在（本任已复核，仅需求文本两处）；「冻结视图」由 RuntimeResources 承载，「BIRTH 不改视图」沿用既有 todo（GAP-112），不新开工作面。落点：L5.1。
C6.5. [bd-001 保留] 001.test.mjs 只断言库目录打包身份（rules.map(name) / fieldNames() / ruleId / fieldName = basename，L12–17），不消费 chronicle tip、不含归一化符号（本任 devops 已核实，见 §2.6-i）；改造后原样保留、按改后语义重跑（与 §2.4 的 003 保留同列）。

C6.6. [条款落地落点表] 每条须改写条款的唯一归属落点如下（改写对象 = requirements/behavior-diagnosis/WHAT.md 或 requirements/guidance-delivery/WHAT.md 的对应条目；改写口径见 C6.1 对应行）：
  - bd [001] / bd [007]：Algorithm L 的 L6（整体改写 / 整体删除；落点已写）。
  - bd [003] / bd [005]：Algorithm F 落地同批（库结构单叶化与 LessonText 单字段校验；整体改写）。
  - bd [004]：Algorithm I 的 I1 落地同批（整体改写；两段元素）。
  - bd [006]：Algorithm S 的 S4/S5 与 Algorithm D 的 D2/D3/D5 落地同批（整体改写）。
  - bd [008]：Algorithm S 的 S2 落地同批（整体改写）。
  - bd [011]：Algorithm D 的 D4.2 落地同批（文本扩展：硬界覆盖对象含两节拼装正文）。
  - bd [012]：Algorithm K 的 K1/K3 落地同批（整体改写）。
  - bd [014]：Algorithm P 的 P1/P2 落地同批（整体改写）。
  - bd [016]：Algorithm P 的 P2 落地同批（语义核对；文本不涉字段名，处置见 C6.1 的 bd [016] 行）。
  - bd [015]：Algorithm K 的 K6.1/K6.2 落地同批（整体改写）。
  - bd [017]：Algorithm D 的 D5.1/D5.6 落地同批（整体改写）。
  - bd [018]：Algorithm L 的 L5.1 落地同批（整体改写）。
  - gd [001]：Algorithm G 的 G4 落地同批（整体改写）。
  - gd [002]：Algorithm G 的 G1/G2 落地同批（整体改写）。
  - gd [003]/[005]：Algorithm G 的 G8 落地同批（措辞级）。
  - gd [006]：Algorithm R 的 R5（整体改写；落点已写）。
  - gd [008]：Algorithm I 的 I5.1–I5.3 与 Algorithm G 的 G8 落地同批（整体改写；三分语义）。
  不改文本者（bd [009]、gd [004]/[007]/[009]/[011]/[012]）不设落点。 # 本表为条款改写落点的唯一权威；执行者随对应算法落地，V1 按本表逐条核查。

C7. [lesson 结构与拼装节名表定稿]（Q4 后取代原「M5 对照表定稿」；原 canonical 节名对照已随 M5 完成沉淀为子节搬移映射的源侧依据，见 §2.6-e）
C7.1. 定库文件结构：resources/enforcer/<TipName>/{lesson.md, lesson.zh-CN.md} 共 202 份；H1 = 「# <TipName> — Lesson」（en/zh 同）；顶层两节 en =「## Opinion」「## Advice」、zh =「## 评价」「## 建议」。
C7.2. 定子节搬移映射（确定性；节正文原样搬运、零改写、零丢失）：en lesson 的 Opinion 收 enforcer.md 的 Definition / Governing Principle / Trigger When / Do Not Trigger When / Distinguish From / Decision Procedure / Examples 七节原序，各节降级为 ### 子节；Advice 收 main.md 的 What To Do Now / Why This Matters / Repair Strategy / Decision Branches / Common Wrong Fixes / Verification / Done When 七节原序（同降级），殿后收 enforcer.md 的 Nudge（### Nudge）。zh 同构：评价收 enforcer.zh-CN.md 的定义 / 支配原则 / 何时触发 / 不应触发 / 与相邻规则区分 / 判定程序 / 例子；建议收 main.zh-CN.md 的现在该做什么 / 为什么重要 / 修复策略 / 决策分支 / 常见假修复 / 验证 / 完成条件，殿后收 Nudge（### Nudge，英文名保留——原 C7.5 的 Nudge 裁决沿用）。 # 子节名即 M5 完成后的 canonical 节名（en 侧英文原节名、zh 侧原中文名）。
C7.3. 定 renderTipSections 拼装节名表：en = [Opinion, Advice]、zh = [评价, 建议]；拼装形态沿用已实施现状——「## <节名> + 空行 + 节文」段、段间空行、单一文本、无尾随换行。 # 与 C1.2 的字段—节映射共用同一真相。
C7.4. 定退役文件：每目录旧 4 文件（enforcer.md / main.md / enforcer.zh-CN.md / main.zh-CN.md）在 lesson 校验全绿后删除；最终库 = 101 目录 × 2 = 202 份。
C7.5. [变换性质] ▷ 重组为确定性结构变换（标题降级与内容搬移），非内容性重写：与仓库「严禁自动化程序批量增删改查程序代码」纪律不冲突——对象是资源文件、变换是确定性搬移、正文零改写由 program 自校验断言保证（F2）。

C8. [遗留处置与异常]
C8.1. 本算法结断：字段与命名（C1）、校验（C2）、规范化（C3）、错误面（C4）、文案键与文案（C5）、条款改写验收（C6，provider-language 除外）、lesson 结构与拼装节名表（C7）。
C8.2. 转出：恢复 Full 实现路径 → Algorithm R/G；provider-language 分类裁定 → Algorithm G；旧数据行为 → Algorithm R/G（原则见 C8.3）。
C8.3. [旧数据原则] ▷ 候选：迁移旧事实；按「历史已消费」处理。结论：不迁移、不重建 Full、不报错；旧事实的 TipRuleId 按 TipName 语义继承（字符串身份不变），其无处置正文 ref 的 occurrence 不触发新 Full；冻结 marker 字节回放不受影响。理由：本机数据、无跨机承诺；旧 Full 的交付记录（TipGuidanceDelivered）与 marker 已冻结在案；重建 Full 会引入第二真相源或不可得字节。精确行为（跳过或 identity-only）在 Algorithm G 的 ▷ 结断。 # 与 gd-005 断言的潜在冲突留给 Algorithm G。
C8.4. [异常] 若 C1–C8 任一定义与 §2 已核实事实冲突，则暂停结案，记录冲突全文，回退该条重裁；不得带冲突进入 M2。
C8.5. [异常] 若某裁决涉及用户偏好且无法从既有裁决推导，则停，把该裁决的事实与候选呈报用户裁决、等待用户决定；执行者（S4 Manager）禁用 ask，呈报 = 停并明确呈现阻塞（与 I6 同口径）；本段已结断项均不属此类。

C9. 返回 C1–C8 定稿。算法终止。

## S3.2 Algorithm A [016 测试硬编码修复]

输入：016.test.mjs 当前文本（第 184–188 行；本任 devops 已复核：184 行为标题串、186/187 行为断言、188 行为动态派生先例）；owner surface 读回的规则集合。
输出：016.test.mjs 第 186–187 行修改后的断言文本；重跑绿的确认。

A1. [锁定断言] 读第 186–187 行：rules.length 与 ruleCount() 两条断言均等于 120。
A2. [定派生源] 期望值 ← 由 owner surface 读回的规则集合长度派生（同文件第 188 行先例）。
A2.1. [裁决] ▷ 候选：写死 101；动态派生。结论：动态派生。理由：120 的病因是写死值随目录裁剪失效；101 仅为当刻事实，会随目录集变化再红；动态派生同时断言断言基数与加载基数同源。
A2.2. [裁决] ▷ 候选：一并改写第 184 行标题串；保留。结论：保留。理由：标题串非断言（§2.5 已核实）；改动破坏测试锚点标识稳定性，收益为零。
A3. [替换] 以派生表达式替换 186/187 两处常量 120。
A4. [清扫] 只读检索同文件 120，确认仅余第 184 行标题串。
A4.1. [异常] 若发现第三条 120 落点：逐处按 A2/A3 口径处置，处置后转 A5。
A5. [重跑] 重跑 016 测试。
A5.1. [红] 若失败：回 A1 复核派生源，不得回写常量。
A6. [绿] 通过则返回修改后文本。算法终止。

## S3.3 Algorithm S [ChronicleTool：schema 替换与执行期校验]

输入：C1、C2、C4、C5 定稿（Algorithm C）；ChronicleTool.fs 现状（150–200 行，本任已复核）；ChronicleExecution 校验族（tryCanonicalText）。
输出：ChronicleTool.fs 的 spec 装配与执行期校验修改后文本；ChronicleExecution 的校验族扩展。

S1. [字段重排] spec 的 Arguments 顺序 ← charge、occurrence、settlement、consequence、tipName、opinion、advice、evidence（C1.7）。
S1.1. [裁决] ▷ 候选：evidence 保持现状位置；移到末位。结论：移末位。理由：C1.7 既定序把两节正文集中在 tipName 之后；evidence 为可选证据殿后，不插入正文段。
S2. [schema 替换] 删除 tip 的 enumSchemaDescribed 字段；tipName、opinion、advice ← stringSchemaDescribed，逐字段 description ← ProviderProse.render(language, Path.ArgTipName / Path.ArgOpinion / Path.ArgAdvice)（C5.1/C5.2）。
S3. [描述] catalogDescription ← C5.5 文本；rule_count 占位由实测条数保留填充（C5.6）。
S4. [校验族] 于 ChronicleExecution 扩展：tryCanonicalText 沿用（trim 非空）；新增 tipName 检查，判定序（此列序即判定序）：读取 tipName 值（读取原语见 S4.1）→ trim 空（含缺失/undefined/非 string 经读取面归并者）→ missing-tip-name；含换行或回车 → tip-name-multiline；字符数（String.Length）大于 200 → tip-name-too-long；内容字段逐个缺失或空白 → missing-tip-field:<camelCase 字段名>（C2.5/C2.6/C4.1，如 missing-tip-field:opinion）。evidence 不入族（C2.3）。
S4.1. [读取原语裁决] ▷ 候选：沿用 args.Text（执行期只判缺失/空白/换行/超长，类型分层交解码期）；新增字符串面区分读取原语（Result<string option, unit> 形，仿 HostToolArguments.OptionalNonNegativeInteger）；经 HostToolArguments.Raw 自解析。结论：沿用 args.Text。理由：HostToolArguments.Text（ToolHostCodec.fs:233–240）把「键缺失」与「非 string」同归 ""，无法承担 D2 的 None/非 string 分家；执行期目的（给 blogger invalid-tip 文案，C4.2 单一文案）已由缺失/空白/换行/超长四类判足，非 string 经 Text 归 "" 后落 missing-tip-name、与 D2 的 None 分支一致；类型层分家由解码期 decodeTipName（D2.1）承担，C2.6「非串在解码期拒绝」即此分层；新增原语会把 ToolHostCodec 拉入触碰面、违反 L4/R5.1 的最小公开面原则。已知代价（接受）：执行期对非 string tipName 返回 missing-tip-name 而非 tip-field-not-a-string，两期内部标识在类型面上分层——C4.1 措辞据此收窄。 # 读取面事实：ChronicleTool 现状三处 args.Text 调用（L111 四字段、L116 evidence、L123 tip），无区分类型读法；数值面 OptionalNonNegativeInteger 与 Raw 为仓内区分/原始解析先例（本任 devops 复核）。
S5. [执行期调用] 以新校验替换 resultForTip：取 args 的 tipName、opinion、advice 值逐个过 S4；任一失败 → 以 invalid-tip 文案返回（C4.3/C4.4，语言按会话）；全部通过 → remembered（C4.5）。删除 resolveByField 调用。
S5.1. [失败路径] 失败不落库、不投影、不改状态；错误面走既有工具错误通道。
S6. [文案键] 确认 Path 常量与 en/zh-CN 成对叶子：arg-opinion、arg-advice、arg-tip-name、invalid-tip（文案见 C4.4）；退役 arg-what-to-do-now 族七键及其 Path 常量、arg-tip 与 missing-tip 键（C5.1/C5.2/C5.8）。
S7. 返回修改后文本。算法终止。

## S3.4 Algorithm D [Codec：八字段解码与校验]

输入：C1、C2、C4、C7.3 定稿；Codec.fs 现状（CanonicalBlogCall 约 24–26 行、decodeCall 约 159–165 行、decodeTip 约 87–94 行、decodeTipString 约 70–85 行）。
输出：Codec.fs 的解码、校验与载荷构造修改后文本；两节拼装函数。

D1. [载荷形态] CanonicalBlogCall ← { Content: ChronicleContent（现状容器保留：Structured 携 Charge/Occurrence/Settlement/Consequence/Evidence option，Legacy 携 text/evidence option）；TipName: string（decode 期 trim 后原串）；Opinion / Advice: string（内容字段，decode 期 trim 后文本，同 tryCanonicalText 族） }。
D1.1. [裁决] ▷ 候选：平铺 tipName 与内容字段；打包为子记录。结论：平铺（与 C1.7 schema 顺序平行，测试访问形如 decoded.value.tipName）。 # 旧 Tip: EnforcerTip 字段退役。
D1.2. EnforcerTip 类型与 EnforcerTip.ofRule 随旧 Tip 字段退役（唯一生产调用在 Codec.fs:84 decodeTipString 内，随 D2 重写删除）；Surface 的 tipToJs/callToJs/cycleToJs 形状同步（D5.3）。
D2. [tipName 解码] 以 decodeTipName 替换 decodeTip/decodeTipString。判定序（此列序即判定序）：None（键缺失或值 undefined，读取层同归）→ missing-tip-name；非 string（含 null）→ tip-field-not-a-string；trim 空 → missing-tip-name；含换行或回车 → tip-name-multiline；超 200 字符（String.Length）→ tip-name-too-long（C4.1）。返回值 = trim 后原串（保留大小写与内部字节；不做枚举归一与大小写变换；沿用 tryStringArg/tryCanonicalText 返回值语义），该返回值即落库值。错误值为 C4.1 内部标识字符串、作 Error 载荷；不映射 C4.4 invalid-tip 文案（该文案仅用于工具执行期，见 S5）。
D2.1. [空值读法] ▷ 候选：None 与 Some null 同归 missing-tip-name（延续现状，§2.6-h）；分家（None → missing-tip-name、Some null → tip-field-not-a-string）。结论：分家（读法见 D2）。理由：判定序的类型层以「值是否 string」为准，null 是显式非 string 值，归类型层使「缺失」与「类型错」两分、C4.1 两组标识各得其位；现状同归是旧单一错误面的遗留（改造后错误面已扩为五类）。已知代价（接受）：null 值的错误标识相对现状变化，006 L73 八格分组随 D5.2 落位表更新。
D2.2. [旧错误面退役] MissingTipError 常量（Codec.fs:28–30、Codec.fsi:22）与其 Surface 转存导出 missingTipError（Surface.fs:155、Surface.fsi:26）随新判定序退役：五处产生点（decodeTipString L77–78/L85、decodeTip L94、decodeCall L161/L162）全部由 D2/D3 的新错误值替换；常量、fsi 签名与 Surface 转存一并退役删除。 # 消费面已核实（见 §2.6-l）：Surface 导出全仓无消费者；测试面唯一消费为 bd-006:76 字面量（随 D5.2 更新）；其余套件零引用。
D3. [内容字段解码] 逐个：opinion / advice 键缺失或 trim 空 → missing-tip-field:<camelCase 字段名>（missing-tip-field:opinion / missing-tip-field:advice）；非 string → tip-field-not-a-string。
D4. [拼装] 定义 renderTipSections(language, 内容字段值)：按撰写语言取 C7.3 拼装节名表（en = [Opinion, Advice]、zh = [评价, 建议]），按节序拼装为「## <节名> + 空行 + 节文」段、段间空行、单一文本、无尾随换行（C7.3）；纯函数（language 为参数）。
D4.1. [裁决] ▷ 候选：拼装在解码期；落库期。结论：拼装函数 renderTipSections(language, 内容字段值) 定义在 Enforcer 层（与 renderStructured 同层，Model.fs 的 EnforcerCycle 模块）；结果随载荷传递（CanonicalCycle.TipSectionsText，见 Algorithm K）；调用点在 ofCall 内——ofCall 增携 TipName/TipSectionsText，从 call 取内容字段值与语言后拼装。语言取用遵循全局唯一语言权威：拼装时刻的全局当前偏好 GlobalProviderLanguage.current ()（规范落点 provider-language [002]/[003]；代码注释锚点 ProviderLanguageBinding.fs:8 的 HOST-026 在 requirements 无对应编号；与交付期 SessionProviderLanguage.languageOf 同源；会话 id 不参与裁决）——现状解码/落库链未穿线语言（Continuation.fs/BlogSurface.fs 无语言符号），该函数零上下文可得，不新增签名链。理由：落库层（Commit）不接触语言；语言服务为 unit → ProviderLanguage 零门槛；renderTipSections 保持纯函数（language 为参数）便于测试。 # 已核实事实见 §2.6-h。
D4.2. [字节界] 两节拼装文本随载荷进入 decode 校验：CanonicalCycle 字段收敛为 { MergedText: string; TipName: string; TipSectionsText: string; MergedEvidence: string }（CanonicalTip: EnforcerTip 退役，见 D1.2）；validateBounds（Decode.fs:235–244）扩展为对 MergedText 与 TipSectionsText 分别执行 C2.2 界（validateContentBounds 的签名与两个既有调用点——Decode.fs:242 生产路径、Surface.fs:209 JS 面包装——同步更新）。超限 → 沿用 contentBoundsError 字符串错误、cycle 不成立（fail-closed、不落 blob）；不映射 invalid-tip 工具文案。
D5. [调用点] decodeCall 装配新载荷（D1）：Content 容器保留，Tip 字段替换为 TipName 与内容字段值；删除旧 Content/Tip 形态与 resolveByField 调用（Codec.fs:80）。
D5.1. [协议检测集] structuredFieldNames（Codec.fs:96–97）与 decodeContent（:142–150）维持现状语义不变：形态互斥按「四内容字段 vs entry/text」两族判定——两族各至少一名同现 → MixedChronicleProtocolError；仅四字段 → Structured；仅 entry/text → Legacy；decodeStructured（:113–132）仍独立读取四字段、不消费检测集（现状）。tipName 与内容字段是形态无关的顶层必填字段（D1），不参与形态判别、不入检测集；其解码在 decodeCall/D2/D3 完成（tip 键在 decodeCall:160 单独处理，改名只触 decodeCall 与 decodeTip 族）。改造后 legacy 形态的完整行为：带 tipName/内容字段的 entry/text 调用走 Legacy 成功路径（006 L81–88 入参更新后断言保持）；不带 tipName/内容字段的历史调用在 decodeCall 首关稳定拒绝（missing-tip-name）。 # bd [017]「新旧协议混用」= 内容形态两族同现，与 tip 字段无关。
D5.2. [测试] bd-006 同步（解码面；执行期分层见 S4.1——tip-field-not-a-string 仅解码期产生）：schema 键集断言（006.test.mjs L131）更新为 C1.7 新字段序；tip 解码形态断言（L33/L40）改为 tipName 的 trim 后原串保留（大小写与内部字节不变，口径同 D2）；错误面精确串（L76）更新为 C4.1 内部标识的分组断言（判定序见 D2/D3；内容字段缺失/空白落 missing-tip-field:opinion / missing-tip-field:advice，Q3 后为两字段）；测试标题描述半句随之更新（保留 WHAT[behavior-diagnosis-006] 锚点前缀，不再宣称「same error」）；L73 循环现状八种输入按 D2 判定序逐格落位、分组断言（不再单串全等）：undefined → missing-tip-name；null → tip-field-not-a-string；'' → missing-tip-name；'  ' → missing-tip-name；1 → tip-field-not-a-string；false → tip-field-not-a-string；{} → tip-field-not-a-string；[] → tip-field-not-a-string（两组：missing-tip-name = {undefined, '', '  '}，tip-field-not-a-string = {null, 1, false, {}, []}）；legacy 用例（L80–93）按 D5.1 的形态语义处置：L81–84 入参更新为 tipName + opinion + advice（entry/evidence 保留），ok/text/evidence 断言保持；L86–88 同形更新 canonicalCycle 用例，mergedText/mergedEvidence 断言保持；L90–92 混合用例以含新字段的 structured() 基底重建、/cannot be mixed/ 断言保留；合并渲染断言（L95–126）在四内容字段保留前提下核对（不改语义）。
D5.3. [Surface 形状] callToJs/cycleToJs 同步：退役 tip 对象（tipToJs: ruleId/fieldName/lexicalOrder），callToJs 增 tipName 与内容字段键（Structured 与 Legacy 两分支）、cycleToJs 增 tipName 与 tipSectionsText 键；测试访问形态随之更新（006 L33/L40 → decoded.value.tipName；018 L18 → decoded.value.tipName，见 D5.2/C6.4）。
D5.4. [016 同步] 016.test.mjs 按六个测试块逐块处置（行号经 devops 逐块核实；三处同形 fixture 统一口径）：
  块 1（L53 OBS_PROJ_003，L3–95）：fixture L22–33 随 K6 观察载荷同步（tipRuleId → tipName、删 fieldNameAtCommit、按输入面补 tipTextRef/tipTextDigest 或 None）；L58 applyEnforcementCycle 入参同形更新；断言 L73、L88–90（tipName 'field-3'、cycleId 'msg_tip_3'）、L92–93 保持（值不变）。
  块 2（L184 A42_001）：L190–198（tryFindByField 规则身份）与 L200–202（compose 包含）不动；L186/187 的 120 断言已由 M0（Algorithm A）处置为动态派生。
  块 3（L204 A42_002）：L212–218 recentTips 映射改为新 RecentTip 形状（t.tipName/t.cycleId，随 P2/K6 口径）；L222/L225 decoded.value.tip.fieldName → decoded.value.tipName。
  块 4（L227 A42_003）：L232–236 的 t.fieldName 取新名；L268–273 投影消息正则随 fixture 修正保持；L277–278 decodeCall 入参更新为 tipName + opinion + advice 后保持 ok（精确名 TIP_X）。
  块 5（L280 A42_004）：L283–293 经 readObservations（L175–180）读观察视图 tipName/cycleId；断言值不变、不涉及 FieldName。
  块 6（L317 ENFORCER_TIP_12）：fixture L304–315 同块 1 口径同步（tipRuleId/fieldNameAtCommit 在 L311–312）；L320 applyEnforcementCycle 入参更新；L324–327 recentTips(...).map(t.cycleId) 只读 cycleId、断言保持。
  三处 fixture（L22–33、L128–139、L304–315）字段同形（10 键，含 tipRuleId/fieldNameAtCommit），逐处点名、同一同步口径，不再以「等」字省略。 # 与 P5 的 014/012 口径一致。
D5.5. [bd-008 同步] 008.test.mjs 涉 tip 三面逐段更新（行号经本任 devops 核实，见 §2.6-i）：① decodeCall / facade 面——L29–42 用例的入参（L36 的 tip）按 D5.4 口径改为 tipName + opinion + advice，L41 的 result.value.tip.fieldName 改为 result.value.tipName；L221–231 facade 用例的 L227 入参同形更新、L230–231 的 sample.value.tip.{ruleId,fieldName} 改为 sample.value.tipName 与内容字段键（D5.3）；② fact fixture 键——L66–67、L156–157、L215–216 的 tipRuleId/fieldNameAtCommit 随 K6 的观察载荷同步（TipName 与 TipTextRef/TipTextDigest）；③ 旧 journal 段——L105/L122/L131 的 ScoreVectorRef 用例按 K2 兼容读口径核对（ScoreVectorRef 条目拒绝语义不变；「缺键 → 拒绝」的条件核对为「TipName 与 TipRuleId 皆缺」）；L11–18 桥接字段禁令不涉 tip、不动。重跑 008。
D5.6. [bd-017 同步] 017.test.mjs 末段解码器用例（L676–697）与 support/cycle.mjs：call() 默认 input 的 tip 键替换为 tipName + opinion + advice（精确名；随 D2/S5 口径）；L679–689 负例的期望保持（completed 但无 tipName/内容字段 → decodedCalls 0 且 decision.ok false）；L696 正例随 call() 修复自动成立；其余修复链测试不涉 tip、不动。重跑 017。
D6. [失败] 任一错误 → Error 拒绝本次调用；不归一化、不重试、不 fallback（C2.7）。
D7. 返回修改后文本。算法终止。

## S3.5 Algorithm L [Catalog：归一化退役与 Surface 包装处置]

输入：C6.1 bd [001]/[007] 改写要点；§8.3 退役方向；调用点全列表（Catalog.fs:130 定义、Codec.fs:80、ChronicleTool.fs:104、Surface.fs:221–227、Surface.fsi:34、Catalog.fsi:22、007 测试第 19/24/25/27 行消费点）。
输出：Catalog/Surface 退役修改后文本；007 测试处置。

L1. [定义退役] 删除 Catalog.fs 的 resolveByField 与 Levenshtein 最近邻族；保留 LexicalOrder 与目录/身份校验（bd [003] 不减）。
L2. [签名退役] Catalog.fsi 删除 resolveByField 签名。
L3. [调用点退役] 逐点处置：Codec.fs:80 随 D2 删除；ChronicleTool.fs:104 随 S5 删除；Surface.fs:221–227 的 resolveField 包装退役；Surface.fsi:34 公开签名删除。
L4. [测试面裁决] ▷ 候选：Surface 面暴露 normalize 包装；不暴露。结论：不暴露。理由：最小公开面；同名判定的真实消费者在交付与投影侧（见 Algorithm G/P），测试从消费面锚定；避免为纯函数再造平行 API。
L5. [007 处置] 将 007 测试由 Levenshtein 行为锁改写为「自由名原样保留、不再吸附」的行为锁，锚 enforcer.decodeCall（既有可达解码面；原 L19/24/25/27 四处 resolveField 消费点随本步改写移除）：① ' cat ' → tipName 'cat'（trim 后原串，不再解析到库内规则）；② 'cot'/'ct'/'caut'/'coat' 各自原样保留、互不吸附（不再映射到 'cut'/'cat'）；③ 'completely-unknown-name' 解码成功且 tipName 原样（不再与库绑定）；另补超长（String.Length > 200）与含换行名 → tip-name-too-long / tip-name-multiline（八格判定序由 006 承接，见 D5.2）。不新增 Surface 符号；执行期文案（invalid-tip/remembered）在现有 JS 面不可达（resultForTip 私有、Surface 仅常量 missingTipError、bd support 仅达解码面），不新建触达面、不新增导出，该现状边界如实注记（与 L4 一致）。
L5.1. [018 处置] 按 C6.4 改写 018.test.mjs 并重跑。 # 与 L5 同类：退役行为锁的语义替换。
L6. [条款] bd [001] 改为「tip 身份 = blogger 自由命名 + normalize 比较」；bd [007] 归一化映射条款整体删除，不保留同义条款（C6.1）。
L7. 返回修改后文本。算法终止。

## S3.6 Algorithm K [Commit：TipName 落库与处置正文 blob 通道]

输入：C1.7、C2、C3、C6.1 bd [012]、C8.3；§8.4-4d；BlogObservationCommitted 现状（Facts.fsi 36–54）；commitWithCoverage 写序（Commit.fs 120–280，本任已复核）；Journal 接口（AppendAgent 单条 fact、WriteBlob）；TipGuidanceDelivered.OccurrenceId 演进先例（Envelope.fs 240–340）；观察视图现状（§2.6-j：Observation.fs / ObservationSurface.fs 字段面）。
输出：fact 载荷、解码兼容与写序的修改后文本；同步点清单。

K1. [字段演进] BlogObservationCommitted 载荷：TipRuleId 改名 TipName（canonical 名）；新增 TipTextRef: BlobRef option 与 TipTextDigest: BlobDigest option（成对）；FieldNameAtCommit 退役（不再构造）。
K1.1. [裁决] ▷ 候选：改既有 fact 加字段；新增独立 fact。结论：改既有 fact。理由：bd [012] 的单一原子提交对象（TipName 原串 + ref/digest）；Journal 无批量 append（单条 fact 接口）；投影 1:1 假设（每 occurrence 一条 observation fact）；TipGuidanceDelivered 的 OccurrenceId 是同类演进先例。
K1.2. [裁决] ▷ 候选：保留字段名 TipRuleId；改 TipName。结论：改名。理由：canonical 词为 TipName（交付与投影侧既定用词）；避免 RuleId 承载自由名语义的概念漂移。
K2. [解码兼容] contextFactDecoder 由纯 auto 改为分流：BlogObservationCommitted → 手写载荷 decoder（仿 hostFactDecoder 的 TipGuidanceDelivered 分支）：TipName 读键 TipName 优先、TipRuleId 兜底（两者皆缺 → 拒绝，与 tip v2 语义一致）；FieldNameAtCommit 不读；TipTextRef/TipTextDigest 用可选读法（旧字节 → None）；其余字段保持现状必填；其余 case 仍走 auto。
K2.1. [裁决] ▷ 候选：依赖 auto 对 option 字段缺失的容忍；手写分流。结论：手写分流。理由：Envelope 注释既定 auto 缺字段给 opaque Thoth error；先例同法；兼容逻辑显式且可测。
K3. [写序] 前提：载荷已过 D4.2 的字节界校验（本函数不重复校验，界在 decode 收口）。commitWithCoverage 内：textBlob（四字段合并正文，现状）→ tipSectionsBlob（TipSectionsText，新）→ evidence（现状）→ refuseStaleAfterBlob（现状）→ 构造 fact（TipName 与两个新 ref/digest）→ appendOrClassify（现状）。
K3.1. [失败路径] 任一 blob 写失败 → KnownNotCommitted（不 append；fail-closed）；孤儿 blob 不可达、无害。
K4. [语言边界] 拼装正文在 ofCall 内按全局当前语言（GlobalProviderLanguage.current；D4.1）拼装、随 CanonicalCycle.TipSectionsText 传递；落库层（Commit）不接触语言。
K5. [旧事实语义] 旧字节的 TipName（原 TipRuleId 值）照旧进入投影；TipTextRef 为 None 的 occurrence 不触发新 Full（C8.3；行为在 Algorithm G 结断）。
K6. [同步点] FactCodec 的 tip 校验句同步为合法键 TipName 或 TipRuleId（旧 ScoreVectorRef 条目拒绝语义不变）；BlogSurface.observationPayload（BlogSurface.fs:604–625，private，经 factOfJs:640–642 与 serializeFact/deserializeFact/deserializeEnvelope 触达）的载荷字段面：读 JS 键 tipName（新形状；该面为 JS 构造面、无旧字节来源，不做兜底——与 K6.2 的 cycleRecordOfJs 同口径），fieldNameAtCommit 不读，tipTextRef/tipTextDigest 可选读（缺省 None）；# 旧字节兼容在 FactCodec/Envelope 解码侧（K2），不在本面；EventStoreJournalWriter 的 blob ref/digest 采集点纳入两个新字段；bd-015 的两处 cycleRecord fixture（L111–122、L162–173，键 tipRuleId/fieldNameAtCommit）随记录字段收敛同步（JS 键 tipRuleId → tipName、删 fieldNameAtCommit；按该输入面的形状补 tipTextRef/tipTextDigest 或 None；F# 面字段名 TipName/TipTextRef/TipTextDigest），015 的 tipName 字符串配对与投影视图断言不动。
K6.1. [观察视图域面] Observation.fs:31 的 tipIdentities 从 tip.FieldName 改取 tip.TipName（decode 期 trim 后原串；与 G3 的展示/比较口径一致，不做大小写折叠）；observationsOf（37–41）与 observationsAfterSquash（45–52）的配对语义与顺序不变。
K6.2. [观察视图 JS 面] ObservationSurface.fs 的 cycleRecordOfJs（168–181）：tipRuleId 读法改读 JS 键 tipName（新形状；JS 面构造器无旧字节来源、不做兜底，fixture 同步见 D5.4）、fieldNameAtCommit 读取删除、按输入面补 tipTextRef/tipTextDigest 可选读法；tipToJs（99–103）输出收敛为 { tipName; cycleId }（ruleId/fieldName 键退役）；workLogToJs（39–43）的观察视图三键 { tipName; cycleId; frameDigest } 保持（015 的配对断言与 016 块 1/5/6 的观察断言因此保持原值）。
K7. 返回修改后文本。算法终止。

## S3.7 Algorithm P [Projection：RecentTip 字段收敛与 previousTips 链]

输入：C6.1 bd [014]；§2.6-c；Projection.fs 现状（EnforcementCycleRecord、RecentTip、RecentTipLimit = 8、fieldNameAtCommit、applyFromEntry）；previousTips 链现状（§2.6-k：Recovery.fs:105–112、ProjectionSurface.fs:131–169、BlogSurface.fs:712–737、Builder.fs:37/70–86、Prompt.fs:28–29、Toml.fs:117–125、TomlSurface.fs:53–54；cc-012/014/018 与 gd-008:34/41 的 field 键消费）。
输出：投影字段、previousTips 链与派生逻辑的修改后文本。

P1. [记录收敛] EnforcementCycleRecord：TipRuleId 改名 TipName；FieldNameAtCommit 删除；新增 TipTextRef: BlobRef option 与 TipTextDigest: BlobDigest option（承接 fact 的处置正文 ref）。承载 = 现状 ByProviderRun: Map<ProviderRunIdentity, EnforcementCycleRecord>（Projection.fsi:22–24；applySquash 只裁 RecentTips、不裁剪它，本任已复核，见 §2.6-i）；新字段随记录进入该 map。
P1.1. [裁决] ▷ 候选：投影不存 ref/digest，交付侧另寻数据源；存。结论：存。理由：交付读路径（R）以投影为唯一 occurrence 索引；None 值对应无处置正文的旧 occurrence。访问面（本任已复核，见 §2.6-i）：Projection.fsi 公开面含 tryFindByProviderRun（run → state → record option）与 recentTips；交付侧现状仅经 recentTips 访问（Tip.fs:61），深取机制落 R2。
P2. [RecentTip 收敛] RecentTip ← TipName、CycleId；删除 FieldName；容量 8 与追加语义保留；同名判定不在本层去重，由消费点承担（C3.5，见 Algorithm G）。 # 消费点全表（含 previousTips 链）见 P6–P10。
P3. [应用] applyFromEntry：由 fact 新字段面组装记录；重复 ProviderRun 语义不变（ENFORCER-154）；RecentTips 保留最近 8（ENFORCER-070）。
P4. [旧数据] 旧 fact 无 TipTextRef → 记录该字段为 None；TipName 来自 K2 兼容读。
P5. [测试] bd-014 锚点同步：RecentTip 形状与容量断言更新；bd-012 断言更新为 TipName 与新 ref/digest。
P6. [previousTips 生产映射] Recovery.fs:111 的 (tip.FieldName, tip.CycleId) 改取 (tip.TipName, tip.CycleId)；L105–112 定义体与 L189/L203–212 的传参位不变。 # RecentTip 删 FieldName 后的编译级必改点。
P7. [JS 输入面统一] ▷ 候选：统一读 tipName；保持两处现状（ProjectionSurface 用 field、BlogSurface 用 tipName）。结论：统一为 tipName。理由：tipName 为 canonical 名（C1.4）；K6.2 已把 ObservationSurface 输出收敛为 tipName；field 与退役的 FieldName 同源，残留即旧同义词漂移；改动为纯键名替换、断言逻辑不动。落地：ProjectionSurface.fs:169 的 `text tip?field` 改为 `text tip?tipName`；BlogSurface.fs:734–737 已是 tipName、不变。
P8. [透明层结论] Builder.fs:37 注释「(FieldName, CycleId)」更新为「(TipName, CycleId)」；Builder.fs:70–86 的 build 参数、Prompt.fs:28–29 的转发、Toml.fs:117–125 的渲染参数名与 TOML 输出键（kind/tip/cycle）不动。理由：TipName 值语义与旧值同形；TOML 键为面向模型的数据面命名、非身份字段名，改键将扰注入字节且无裁决依据。
P9. [测试面] 三套 cc 测试与 gd-008 的 previousTips 键字面替换（断言为序列 deepEqual / regex 包含 / 布尔全等，均非键名敏感逻辑）：cc-012:480/481/508/509/523/585、cc-014:113/114/184、cc-018:693 的 `field:` 改 `tipName:`；gd-008:41 的 `{ field, cycleId }` 改 `{ tipName, cycleId }`。
P10. [补录与条款核对] §9.2 触碰文件族与 §9.4 验收面随本算法同步补录；cc 套件 WHAT 无 previousTips 专条、最近条款为 [019]（冷边界辅助注入归零），本次改动不触 [019] 语义。
P11. 返回修改后文本。算法终止。

## S3.8 Algorithm I [composeBloggerSystemPromptFor 双文档注入]

输入：§8.2（单文档单语注入）；C6.1 bd [004]；§2.6-m（TipSurface 与真 journal 事实；I5.2）；§2.6-b（localizedFiles、loadFor、composeBloggerSystemPromptFor 28–43、RuntimeResourceAssembly、ProviderSystemTransform 101–108）。
输出：装载/合成/重拼修改后文本。

I1. [注入面] composeBloggerSystemPromptFor：每条注入两段元素——rule.Name（裸目录名）、LessonText.Trim()（EnforcerRule 以单一 LessonText 取代 EnforcerText/MainText）；排序仍按 LexicalOrder；渲染沿用 LlmFacing/SyntheticToml 既有机制（逐元素 `# ` 注释、元素间单换行、末尾单换行）。
I1.1. [裁决] ▷ 候选：两文本加来源标注；不加标注、逐元素相接。结论：不加标注。理由：库文本原样呈现（样板意图）；额外标注污染文本且无消费者；分隔由既有渲染机制（Name 元素、`# ` 注释与文本自带空行）承担、不插额外空元素。
I2. [单语保持] localizedFiles 单叶映射（English → lesson.md、zh-CN → lesson.zh-CN.md）与 loadFor 单叶装载（EnforcerRule.LessonText）；不合并双语（§8.2 淘汰 2b）。
I3. [预载] RuntimeResourceAssembly 的双语预载不变。
I4. [请求期重拼] ProviderSystemTransform 不动；重拼公式仅内容增长，签名不变。
I5. [测试] bd-004 注入内容断言按两段元素镜像更新（segment = comment(rule.name) + 单换行 + comment(LessonText.Trim())，与渲染器逐字节同构）。
I5.1. [gd-008 composer 面] L11–19：夹具与断言随两段元素（Name + LessonText）与 lesson 两节结构更新；L18 的 REMEDIATION SOURCE 断言按 Q1 双灌入裁决已翻转为正面包含，Q4 后 lesson 单文档同时承载检测与处置内容（Opinion/Advice 子节），正面包含断言保持；L16/L17 包含式保留。
I5.2. [gd-008 resolver/audience 面] L22–30 与 L49–68：L27 的 body('main') 读取删除（不再作正文来源）；L28/L60 改为断言包含夹具物化的撰写正文；support/journal.mjs 增补 blob 物化能力——本套件 journal 为真 journal 的不透明盒（TipSurface.createJournal → JournalSurface.boot；journalHandleOf 做 unbox，JS 假对象无法通过），port/替身路线类型层不可行（009/cc-016 的受控 port 由 xwire.candidateFromJournal 消费、属 wire 候选链，与本交付读链非同一条）；最小接线 = F# TipSurface 新增写入助手，与 Persistence JournalSurface.writePayload 同形（unbox JournalHandleBox → handle.Journal.Writer.BlobWriter.Write content → 回传 { ok; blobRef; blobDigest }；TipSurface.fs 与 TipSurface.fsi 同步新增），support 用它物化正文、将真实 ref/digest（blobs/<sha256> 形态）交给 observe；读回由生产读路径（Algorithm R2 的 journal.Writer.BlobWriter.Read）在同一真 journal 上完成；L29/L61 的检测正文不进交付的否定断言保留。依赖 Algorithm R/G 的读回形态，随 M4 落地。
I5.3. [gd-008 低信任面] L33–46 的 previousTips 键名随 Algorithm P 的 P9（tipName），随 M2 落地。
I5.4. [边界] bd-008 无装载/composer 面（本任 devops 复核零命中），本步不涉；§9.4 的 M3 标注同步修正（见 §9.4）。
I6. [体积异常] 若按 I1 组装后的 Blogger system prompt 超出所部署模型可支持的上下文上限，则停，把体积超限的事实与候选（维持全量 / 降级）呈报用户裁决，等待用户决定；不自行抽样、截断或降级（全量灌入为 A1+A2 裁决，降级须用户裁决，见 §10.2）。触发时装载与合成结果不变，不写任何持久事实。 # 执行者（S4 Manager）禁用 ask；呈报 = 停并明确呈现阻塞、等待用户裁决。
I7. 返回修改后文本。算法终止。

## S3.9 Algorithm B [blogger role 提示词改写]

输入：C5.7 改写要点；§2.6-b（en.md 50–52 约束句）；语言成对约定。
输出：resources/provider/role/blogger/ en.md 与 zh-CN.md 修改后文本。

B1. [删除] 删除「恰好一个 Rulebook TipName / Choose exactly one」类措辞及其因果脊句中的对应半句。
B2. [替换] 写入：tipName 自由命名（可自创新名；同一教训再次出现时沿用同名）；opinion 与 advice 两节自由撰写——评价与建议各司其职（库为 bootstrap 与格式样板，不是封闭值域）。
B3. [保留] tip 是可复用教训、具体字段描述这次具体转折的因果脊语义；原「the other four fields」措辞随字段拆分更新为现字段指代（opinion/advice 两节）。
B4. [成对] en 与 zh-CN 同步改写、语义等价；不改机制。
B5. [测试] bd-016 提示词面断言按 D5.4 口径处置：016 的 compose 断言为包含式，注入形态变化（双文档 → 两段元素单文档）后仍绿，不作改动；M3 面仅跑核对（§9.4）。
B6. 返回修改后文本。算法终止。

## S3.10 Algorithm R [交付读路径]

输入：§8.4-4a/4d；§10.1 假设 A；C6.1 gd [002]/[006]；投影记录（含 TipTextRef/TipTextDigest）；Journal 的 blob 读能力（IBlobWriter.Read，经 journal.Writer.BlobWriter.Read 既有可达、10+ 处先例；AgentJournal 本体无读成员）；readFrameBody/ensureFrameDigest 的 sha256 校验样例（Wire.fs:85–96）。
输出：读路径修改后文本。

R1. [接口] 投影侧：不新增公开符号，深取经既有 Projection 公开面——tryFindByProviderRun（Projection.fsi:39–40；消费点现状仅 Continuation.fs:627 / Commit.fs:242，本任已复核，见 §2.6-i）；交付侧现状已访问同一模块（EnforcementProjection.recentTips，Tip.fs:61），深取属同类既有访问、不构成新穿线。journal 侧：不新增成员、不新增穿线——读回经既有 journal.Writer.BlobWriter.Read（BlobRef → Task<Result<string, string>>；Tip.fs 已持有 journal；与 Enforcer/Cycle/Recovery.fs、Mission/WorkRecord/Materialize.fs 等 10+ 处先例同形）。理由：最小公开面（与 L4/R5.1 同调）；AgentJournal 本体保持只有 WriteBlob。
R2. [读路径] 交付解析（机制指名）：latestOwnerTip 链上已取到 session.Enforcement（Tip.fs:55–60）；按 CycleId 深取——ProviderRunIdentity.create cycleId（Foundation/Identity.fs:282 的透明构造；CycleId 即 ProviderRunIdentity.value，Projection.fs:71）→ Projection.tryFindByProviderRun run state（Projection.fsi:39–40）得 EnforcementCycleRecord → 取 TipName、TipTextRef、TipTextDigest → journal.Writer.BlobWriter.Read(ref) → 对读回字节求 sha256、与 TipTextDigest 比对（对照 Wire.fs:85–96 的 ensureFrameDigest 模式）→ 相符则返回正文文本。落点：guidanceForOwner（Tip.fs:163–168）已携带 tip.CycleId，深取与既有单次读取同段完成。
R3. [失败路径] 三种情形同归「无处置正文」：记录缺失；TipTextRef 为 None；读回失败或 digest 不符（不采用错字节，诊断记录）。
R4. [恢复路径裁决] ▷ 候选：blob 重读；anchor 回放。结论：blob 重读，且落在既有单次读取（latestTipGuidance 一次调用）内；已有 placement 的重放分支保持原字节回放、不重读（冻结点与重放不重渲染分支原样复用）。
R5. [条款] gd [006] 改写为「从 occurrence 载荷取处置正文 ref 读回」（C6.1）。
R5.1. [公开面] ▷ 候选：交付读路径新增 Surface 公开符号；不新增。结论：不新增。理由：最小公开面（与 L4 同调）；测试全走 JS surface 行为名断言（§2.4），正文读回结果经既有 resolve 行为面观测；R1 的读回为交付侧既有内部调用（journal.Writer.BlobWriter.Read）、不入 Surface。 # §9.2 边界待裁决点（Surface.fs 测试面是否需暴露新读路径）由此结断。
R6. 返回修改后文本。算法终止。

## S3.11 Algorithm G [Tip.fs Full 组装、语言选择退役、冻结交互]

输入：C6.1 gd [001]/[002]/[003]/[005]/[008]、C8.3；§8.4-4a；Tip.fs 现状（本任已读全文）；Algorithm R 读路径；provider-language [005]/[006]/[009]/[013] 原文（本任已复核）。
输出：Tip.fs 修改后文本。

G1. [组装] tipFullText ← 标题（tipHeading 现状常量，按会话语言）+「tip = "名字"」+ 处置正文（R2 读回字节）；正文为空则回退 identity 文本。Full 分支才记录交付（recordFullTipDelivered）；identity 分支不记录、不推进 Frontier。
G2. [退役] 删除 guidanceForField 的 tryFindByField 查库路径与 languageOf 的查库用途；正文来源改为 R2 读回；标题语言沿用 owner 会话语言。
G3. [身份] tipName 直接取 RecentTip.TipName（decode 期 trim 后原串；不再经规则名派生）；occurrence ← RecentTip.CycleId。
G4. [同名比较点] 交付同名比较的落点清单与处置（行号经本任 devops 核实）：
G4.1. [比较点] normalize 比较适用于：① TipDeliveryProjection.isIdentityOnly 的 tipName 包含判定（DeliveryProjection.fs:26–31）；② hasFullDelivered 的同名包含判定（:35–39）；③ CoveredTipNames 的写入与查询（:52–56 规范化入口、:71–73 写入点）；④ occurrence 轴（DeliveredOccurrences）保持精确、不规范化。 # C3.3 比较点对应：Coverage 判定 ①②③、交付同名判定 ①；RecentTip 层不去重（P2），同名判定统一落在 G4.1。
G4.2. [集合键形态] ▷ 候选：CoveredTipNames 存原串、查询时逐元素 normalize；存规范化键（写入与查询两侧都过 normalize）。结论：存规范化键。理由：Set 包含语义要求键与查询同构；C3.4「存原串」约束落库/投影/交付展示面（RecentTip.TipName 与 fact TipName），Coverage 集合为内部比较结构、不参与展示。
G4.3. [展示与比较拆分] 定 tipNameOf（Tip.fs:111–116）的名字派生路径随 G3 退役（名字一律取 RecentTip.TipName，即 decode 期 trim 后原串）；展示名不做大小写折叠、也不改写内部字节；normalize（trim + ToLowerInvariant；trim 为幂等防御）只在比较点调用；tipIdentityText（:134）与「tip = "<name>"」（:138）展示该原串，不因比较规范化而变小写。 # 比较键与显示名分离。
G4.4. [实现载体] normalize 单一实现于 Enforcer 层（C3.1/C3.2），供 DeliveryProjection 与 Tip.fs 消费；测试从消费面锚定。
G4.5. [测试锚定] gd-001 现仅 presentation 断言（本任已复核），同名去重的验收锚定由此扩展：同一 tipName 的大小写/首尾空白变体在第二次交付时必须仍为 IdentityOnly、不产生新 Full（并入 G8 的 gd-001 项）。
G5. [旧数据行为裁决] ▷ 候选：跳过（None）；identity-only；空体回退 Full。结论：identity-only（产出 identity 文本，不记录 Full 交付）。理由：Frontier 语义不应被无正文的伪 Full 推进；IdentityOnly 在投影上状态不变、幂等；既有冻结 marker 字节回放不受影响（C8.3）。
G6. [冻结交互] 重锚后恢复路径：R2 读回 + G1 组装，落在既有单次读取内；已有 placement 原字节回放不变（§2.6-d 冻结点与重放不重渲染分支原样复用）。
G7. [provider-language 裁定] ▷ 候选：交付 marker 文本属 Class A（改 [009]/[013]）；不属（整套保留）。结论：不属，整套保留。理由：Provider Prose 列举语境为资源族（提示词/角色规范/工具描述/执行后果/完成文案），交付标题与框架是代码常量（已核实不在资源机制内）、正文是 durable 撰写字节；[009] 的统一资源机制装载对动态字节不可满足；[013] 的全局语言即时生效与冻结回放（gd [011]、prefix-stability [010]）结构性冲突。裁定后 C6.2 转出项闭环。
G8. [测试] gd-002（Full 断言改按拼装形态：经 chronicle 提交的两节拼装正文（opinion/advice）与 tipName 出现于交付文本，含节标题）/003/005/006/008 断言面同步（§9.4）；gd-001 增补同名变体（大小写/首尾空白）去重断言（G4.5）。
G9. 返回修改后文本。算法终止。

## S3.12 Algorithm F [库重组工作包：确定性变换与自校验]

输入：C7（lesson 结构、子节搬移映射、拼装节名表、退役文件、变换性质）；M5 完成后的库基线（§2.2、§2.6-n：en 侧 101×enforcer 八节 + 101×main 七节零变体，zh 侧同构 canonical 中文）；§8.5 重组方向。
输出：404 份旧文件 → 202 份 lesson 文件的变换结果与自校验记录；旧 4 文件退役删除的确认。

F1. [变换规则] 每规则目录（101 个）生成两份 lesson 文件；变换为确定性结构搬移（C7.5），正文零改写：
F1.1. en lesson.md：H1 =「# <TipName> — Lesson」；「## Opinion」节按原序收 enforcer.md 的 Definition / Governing Principle / Trigger When / Do Not Trigger When / Distinguish From / Decision Procedure / Examples 七节——各节降级为 ### 子节（节标题与正文原样，仅 ## → ###）；「## Advice」节按原序收 main.md 的 What To Do Now / Why This Matters / Repair Strategy / Decision Branches / Common Wrong Fixes / Verification / Done When 七节（同降级搬移），殿后收 enforcer.md 的 Nudge（### Nudge）。
F1.2. zh lesson.zh-CN.md：同构——H1 =「# <TipName> — Lesson」；「## 评价」收 enforcer.zh-CN.md 的定义 / 支配原则 / 何时触发 / 不应触发 / 与相邻规则区分 / 判定程序 / 例子 七节（### 子节保留原中文名）；「## 建议」收 main.zh-CN.md 的现在该做什么 / 为什么重要 / 修复策略 / 决策分支 / 常见假修复 / 验证 / 完成条件 七节，殿后收 Nudge（### Nudge，英文名保留）。
F1.3. 节正文原样搬运、零改写、零丢失；子节顺序 = C7.2 映射序，不重排、不增删内容。
F2. [program 自校验] 变换以 program 执行并自校验，全部通过才提交：① 结构断言——每份 lesson 恰含 H1 + 两顶层节（en = Opinion/Advice，zh = 评价/建议），子节数与顺序 = C7.2 映射（en Opinion 7 子节、Advice 8 子节；zh 同构）；② 零丢失断言——对每份 lesson 逐子节比对源文件对应节的正文（节标题行之外逐字节全等；搬运的标题降级不触正文）；③ 202 份全部通过。 # 与仓库「严禁自动化程序批量增删改查程序代码」纪律的相容性见 C7.5：对象为资源文件、变换为确定性搬移、零改写由本条断言保证。
F3. [旧文件退役] 校验全绿后删除每目录旧 4 文件（enforcer.md / main.md / enforcer.zh-CN.md / main.zh-CN.md）；最终库 = 101 目录 × 2 文件 = 202 份。删除与装载面切换（Algorithm I2）须在同一验证循环内闭环——库先换、装载未切会红，装载先切、库未换也红；执行序：lesson 文件生成 → 自校验 → 装载面切换落地 → 旧文件删除 → 全链测试。
F4. [抽样复核] program 自校验之外，抽样若干规则人工比对 lesson 与源文件（抽样集须含 cyclic-dependency、unrecorded-lesson、duplicated-control-flow 等历史变体目录）——确认零改写、零丢失、子节序正确。
F5. [顺序] 与代码线（M2/M3）并行；V 前完成。
F6. [失败] 任一断言红 → 回该规则重变换；不放宽断言、不豁免文件、不留下四文件与两文件并存的中间态入库。
F7. 返回变换结果、自校验记录与删除确认。算法终止。

## S3.13 Algorithm V [收尾验收：全链测试与条款一致性复查]

输入：§9.4 验收面；C6.3 五类语义；两套测试命令与套件。
输出：验收执行记录与残留清单。

V1. [前置] 逐里程碑核查 M0–M5 的执行落地：M0 的 016 断言修改已改且绿；M1 契约（C1–C8）已由后续各步落实；M2–M4 的代码、文案、提示词与条款文本修改已实施（条款逐条对照 C6.6 落点表）且各自测试面已跑；M5 的库重组已完成——202 份 lesson 生成、program 自校验与抽样复核记录在案、旧 4 文件已删除（按 F2–F4）。凭执行记录核查（测试结果与产物），S3.0 索引仅作算法对照、不作落地凭据。任一未落地 → 停，报告缺口，不开始验收。
V2. [全链] 执行 bd 与 gd 两套测试及其 prefix-stability 相关锚点（§9.4 清单）。
V3. [条款扫描] 对五类语义（tip 归一化、TipName 三元组、Full 正文来源、owner 语言、冻结对象）做全仓只读扫描，确认落点仅在 bd 与 gd 两套（C6.3）。
V4. [残留清理] 检查退役残骸：arg-tip/missing-tip 旧键、arg-what-to-do-now 族七键及其 Path 常量（arg-why-this-matters / arg-repair-strategy / arg-decision-branches / arg-common-wrong-fixes / arg-verification / arg-done-when）、whatToDoNow 等七字段符号（ChronicleTool schema、Codec 载荷、renderTipSections 节名表、测试 fixture）、旧 404 份库文件（enforcer.md / main.md / enforcer.zh-CN.md / main.zh-CN.md 及其任何装载/文档/测试引用）、MissingTipError/missingTipError 符号、resolveField 符号、Levenshtein 族、FieldNameAtCommit、120 硬编码断言（除 016 第 184 行标题串外；见 A2.2/A4）；无残留生成物与临时文件。
V5. [失败] 任一红 → 定位到对应算法与里程碑，修复后重跑；不豁免、不跳过。
V6. [绿] 全绿且残留清零 → 返回验收记录。算法终止。

