# 上游语义更新整合计划

2026-10-19。用户已授权按本计划逐包执行：先落盘计划和语义处置表，再按依赖推进；每包记录验收证据与剩余边界，保住本地已验证保证。本次不执行 Blogger/Chronicle 全退役，不清零历史 GAP，不清空运行数据。

## 固定输入

| 对象 | 本地输入 | 上游输入 | 共同祖先 |
|---|---|---|---|
| 主仓 | `b436802758c25b5c33e4a99f5349f57c812005b8` | `2f23502c60bac0abed11ebdd39d8555cc1d90457` | `7d8feff030adfdeae511bdc0d8bb77f3c00b2a1f` |
| wanxiangli | `8bb1021fc3519b0846e70af3319be9822d4b6b80`；主仓实际依赖 `vendor/wanxiangli-0.6.12.tgz` | 主仓固定的 `71c155f3a2d5e9d559beea602ce11694904c4e5f`，包版本 0.8.0 | `46d9c072af50389d069709e847d5772af3ec160a` |

初始两个工作区均干净。主仓分别独有 65/53 个提交，库分别独有 20/9 个提交。临时仓库 `git merge-tree --write-tree --name-only` 预演主仓得到 76 个冲突路径：48 源码、19 requirements、6 resources、3 其他；冲突类型为 51 content、24 modify/delete、1 add/add。这只是文本合并证据，不代表语义兼容或生产验收。

后续发现上游新提交时先登记为下一批，不在施工中漂移以上输入。原始调查依据为[前四模块差异](LOCAL-UPSTREAM-FIRST-FOUR-DIFF-2026-10-18.md)、[resources 和 time/wait 接收评估](UPSTREAM-INTAKE-ASSESSMENT-2026-10-18.md)、[现行看板](BOARD.md)。本批唯一语义处置索引为[处置表](UPSTREAM-SEMANTIC-DISPOSITION-2026-10-19.md)。

## 目标与施工纪律

目标是接收固定上游已经实现的产品模型，同时保住仍适用的原件、物理执行、资源归属、持久化及授权保证。旧工具或流程退役只解除其专属产品义务，不自动证明相邻保证已经迁入。

每包先读现行与目标 WHAT，确定条款归属及反例，再调整测试、实现和调用者，最后删除旧路径。生产源码逐处手工编辑，不用自动改写程序解决冲突。公共类型先定契约，再改调用方。锁文件、gitlink、构建图、插件接线和看板由同一施工者串行处理。

整合使用独立分支，保留双方提交历史；不 rebase 已发布 master，不 force push。库先交付可取得的固定 SHA，主仓再引用。每个可独立验收的小包可以提交；只有完成本批相关门禁的候选才能合入主仓 master。提交、推送、验证和部署分别记录。

本计划是施工记录，不替代 WHAT。处置表中的目标决策必须在对应包落入正式合同及测试后才成为已实现行为。

## 工作包与依赖

| 包 | 范围 | 依赖 | 状态 | 验收记录 |
|---|---|---|---|---|
| SM-01 | 固定版本、语义处置、条款与测试锚点冲突、基线证据 | — | DONE | 68a6785f1 已落盘；20项职责和5项冲突均有目标、责任包与反例 |
| SM-02A | 库历史整合、Journal/Process/Supervision/Trace 本地保证与上游 Work 规则并存 | SM-01 | LIB_READY | 核心 Node315/0/6skip，Bun273/0，独立编译及真实包外组合43/0；库提交待记录，未非作者复审 |
| SM-02B | 新增模块接收：resources、time/wait、grounding、identity/events/fs 的实际边界 | SM-02A | LIB_READY | 七模块Node/Bun各191/0；独立编译及包外20/0；静止候选重包3/0，主仓未接线 |
| SM-02C | 库构建、声明、隔离安装、可获取提交；正式测试监督器保留 | SM-02B | DOING | 待固定全部候选；运行依赖归档退出与15份历史证据归档的规则冲突已定位 |
| SM-03 | 主仓依赖、源码 submodule、状态根、生命周期、退役 Git 状态同步 | SM-02C | BACKLOG | 未运行 |
| SM-04 | 新 js.run 接入物理执行、输出预算、取消、资源归属和真实收尾 | SM-03 | BACKLOG | 未运行 |
| SM-05 | Trace/ANCHOR/前缀/显式子工作终值与现存历史消费者 | SM-03 | BACKLOG | 未运行 |
| SM-06 | 身份和双侧门禁、失败预算与发送去重、结构化委派、Advisor/horizon | SM-04、SM-05 | BACKLOG | 未运行 |
| SM-07 | 同一候选的相关回归、真实 Host、包外消费及切换准备 | SM-06 | BACKLOG | 未运行 |

SM-04 与 SM-05 的独立调查和测试可交错推进，公共接线串行。每次更新表中状态时同步下面的证据和剩余边界；不另建与 BOARD 竞争的状态表，BOARD 只链接本批工作包。

### SM-01 契约与基线

- 区分上游已经实现、仅有研究方案、当前进程实现和尚未有冷恢复保证的部分。
- `process-execution-017` 两侧编号冲突：本地二进制 Git 与上游退役时限政策分别定归属，禁止覆盖同编号测试掩盖义务丢失。
- 核对 `context-compression-017/031` 用户原文保留与 ANCHOR 替换的边界，以及 `work-008/034` 所述视图交棒与进程内摘要之间的恢复关系。
- 保留现行正式监督器、测试入口及精确输入；已有 TODO/skip/未归因失败单列。
- 完成条件：每项冲突有目标处置、责任包、反例和明确剩余范围；不要求此包实现全部合同。

### SM-02 库整合与接收

- 保留本地 statement 释放、业务封存/物理 close 分离、完整 Capture、Supervision、逐路 extent、Bun 背压、stdin 有序排空及原件协议。
- 接收上游连续未评审接力门、Reopened 配对、三字段 Reviewed 与 fence-owner 重放冲突；不以本地旧 Work 整文件替换。
- 修正既有接收反例：resources 模板与注入值裁剪顺序；time/wait 声明与实际导出、冻结 deadline 输入、窄能力与实现归属。其他新模块按实际调用者与风险核对。
- GAP-085 算法移到新的 grounding owner；Predictor 私有身份与公开角色集合的处理进入身份接收边界。
- 验收：受影响库套件、Node/Bun 物理用例、独立编译及实际安装包消费；检查 `.d.ts`、ESM、监督增强可拆除、声明依赖完整。真实 Host 证明留给后续包。

### SM-03 主仓接线与持久状态

- 固定包含双方修复的库 gitlink，采用 `file:wanxiangli`，让全新 clone 能取得和构建同一源码；检查生产包 bundle 闭包。
- 统一 `.Herdchestra` 的状态定位，Work/Ownership SQLite 与事件流各有唯一 owner；验证 linked worktree 共用状态根而工作 root 仍区分。
- 接收生命周期 replay-before-gate、开立闭集、派发窗口及终态结算修复。
- 删除运行数据 Git transport、hooks、ProcessGitRawStore/WriterStreamSync 等退役路径。保留通用 Git 消费者所需的二进制输入和真实物理结果。
- 验收：durable-events、durable-convergence、work、crash-reconciliation、change-integration、distribution 相关条款及实际冷读；新根不触碰旧运行目录。

### SM-04 物理执行

- 新 js.run 保留 sanity/next/watch、前后台、send、waitFor、park、timeout 协议；物理能力显式注入，不恢复旧 ExecutorTool 注册。
- 原始字节采集、真实目标终结、停止请求、监管范围结束、输出呈现分别保留。按新产品头尾规则呈现，同时满足完整 UTF-8 结果及真实 Host 窗口预算。
- 验证预取消、排队取消、启动握手、stdin EOF、signal 与高退出码、双路大输出、背压、spool 故障及后代收尾。pipe 交互不代证 PTY。
- 跨 Host 恢复按实际承诺单列证据；旧持久资源不能因工具改名直接清账。

### SM-05 历史与视图

- 物理捕获先于摘要/指导呈现；保留大 Opening、晚到原件、稳定身份、冻结 cut 与冷恢复，不把 summary 当原始事实。
- 子工作改读 js.deliver 终值，撤掉旧 DelegationHandoff、child LWR 物化和 LWR_prev 注入；未交付明确 undelivered。
- 对仍存在的 WorkRecord、inspect、Resume、Casebook 等逐入口验证范围、完成因果边界及暖冷一致，不靠单个 materializer 代证。
- ANCHOR 累计替换、原文保留、前缀稳定、checkpoint 与接力恢复使用同一份已裁定合同。

### SM-06 身份与恢复

- Predictor 私有身份及公开准入、office reset、真实 Fission 授权保留；接入 Advisor 无工具 office 与 horizon 数据投影。
- 持久失败预算和成功根绑定、同 live Journal 恢复发送去重在新派发链成立。
- GAP-140 A/B1 已验范围保留；B2 跨 OS 事实竞争和发送权不在本次自动销项。Advisor 专席与增量游标当前只在进程内；不宣称重启复用已经完成。

### SM-07 整体验收与切换

- 每包先定向 requirement 回归，全部接线后对同一固定候选运行 Fable 构建、相关集成、真实 Host 和独立安装包消费，再运行正式发布入口。
- 构建仅使用 `node scripts/build.mjs` 或仓库正式 verify；禁止 dotnet build。最低 Node 22.13、当前 Node 和 Bun 证据按各自实际入口记录，Bun 原生通过不代替尚未闭合的正式监督。
- 活动 work/PTY 由旧版本按原协议收束；未确认资源阻止运行切换。旧状态保留备查，不清空、不自动重播到新根。
- 最终记录提交、推送、验证、部署各自事实；未部署就明确未部署。

## 验收记录

### SM-01 开始

- 输入：上表两个固定本地提交；两个正式工作区 `git status --porcelain=v1` 均为空。
- 调查：固定上游源码、WHAT、库历史和临时三方预演；未运行产品测试。
- 当前交付：计划、处置表和现行入口链接。
- 剩余：契约冲突的正式去向、库候选及所有生产接线。既有看板中的 REVIEW/DOING/READY 不改变。

### SM-01 交付与 SM-02A 核心候选

- 计划与语义处置表提交 `68a6785f1`。origin/master 的并行方法文档提交 `522e942bc` 已包含它；独立整合分支正常快进保留对方改动，未强推。
- 两仓整合工作树已隔离。库从 8bb1021 合并固定 71c155f；五个文本冲突手工处理，候选版本0.8.1保留 supervision 导出、声明、说明和测试。原核心生产代码只有上游 lifecycle 规则发生变化。
- `node scripts/build.mjs` 十四入口通过；Node核心321项315pass/0fail/6skip/0TODO；Bun正确核心范围273pass/0fail；独立编译、可拆监管增强和真实Node/Bun安装包组合43pass/0fail/0skip/0TODO。
- 首轮Bun误纳入静态node:sqlite专用测试的6个加载错误保留，不改成通过。Journal的Bun实际消费由包外测试证明。库长期原始证据在 `evidence/semantic-merge-sm02a/README.md` 及同目录日志。
- 边界：这只证明已接入核心的限定范围，尚未完成新模块接收、最终依赖交付、主仓接线或非作者复审；原卡不销项。

### SM-02B 接收修正

- 库SM-02A提交为`152719c`。SM-02B保留原核心，修资源替换次序、冻结deadline、窄observer、声明与实际对象；时间拆为time/time-node/time-testing三个真实编译/发行单元，生产没有虚拟工厂。
- GAP-085的完整子树算法进入fs公开能力并由grounding调用；首个右括号和未来路径保证保留。新接收fs还复现并修正macOS逻辑根与realpath别名误拒，增加symlink根正反用例。
- 七个新增模块Node/Bun各191pass/0fail，独立编译/真实安装消费20pass/0fail；停止构建后重包3pass/0fail。库长期原始证据见`evidence/semantic-merge-sm02b/README.md`。
- 旧红、错误扫描前提和Bun调用范围错误均保留，不计通过。全库最终候选、所有导出闭包、非作者复审和主仓实际接线仍待。
- 新发现的源码归档规则冲突进入SM-02C：上游拒绝任何tgz，但本地有15份固定历史红/绿取证归档；必须保留证据身份并禁止它们变成运行依赖，不通过删证据或改名取得绿色。

## 本次保留的未完成边界

GAP-140 B2、CUT-03-R7/R8/R9、CUT-06-R5、CUT-04-R7/R8、CUT-00-R2/R3、R12-U1 及其它历史 TODO 保留原编号、证据与状态。迁移中发现其旧消费者退出时逐项登记适用性，不据此把父 GAP 改 DONE。Blogger 全退役方案、其状态清空建议和替代架构均不进入本批。
