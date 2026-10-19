# SM-07 施工证据

候选进入REVIEW，以[计划](../../proposals/extraction-kanban/UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)为准。SM-01至SM-06按各自限定范围验收，完整发行未通过，未部署。最终构建、实际包摘要、代码提交与普通推送的各自事实见同目录delivery.json和末轮日志；不将这些事实互相替代。

本包收束手工格式、同一候选的正式相关回归、最低Node22.13、独立安装消费、源码子模块可达性及正式发行入口。历史TODO仍按失败记录；不使用WXS_ACCEPT_TODO。旧活动work/PTY未确认前不切换运行，旧状态不清空、不自动重播。

- 格式检查01列出34个路径，均按单文件预览差异手工编辑，没有自动批改源码。检查02中的Fantomas已通过，但Wireit写工作树缓存被沙箱拒绝；允许缓存写入后的检查03整体退出0。格式展开暴露的13处控制嵌套和Scope另3处嵌套按结果分类、输出发布、取消和资源释放职责拆开，static-check-03退出0。既有shard-edge低置信度提示仍为report-only，不记作销项。
- 最低版本运行时取自[Node.js官方22.13.0清单](https://nodejs.org/download/release/v22.13.0/SHASUMS256.txt)：darwin-arm64.tar.gz的SHA256为bc1e374e7393e2f4b20e5bbc157d02e9b1fb2c634b2f992136b38fb8ca2023b7，实际下载匹配。归档5929项、3个内部符号链接，未发现绝对路径、父目录逃逸或越界链接；只解到/tmp/vibe-fs-sm07-node22.h1udgm，实际版本v22.13.0。系统Node v26.6.0和Bun1.3.14未替换，最低版本测试尚待记录。

- 最低库原生初测505pass/1fail/6skip，唯一失败为测试全局Readable钩子混入Node22测试运行器的内部读取。改为识别确切ready字节及唯一流，保留真实stdout的EIO、等待者失败和无伪造exit断言。该文件Node22/26各8pass/1skip，Bun9pass/0skip。完整最低库native-02因npm缓存沙箱权限产生5个打包失败；正常权限下native-03为566项559pass/0fail/7skip/0TODO，含真实npm包、独立编译和Node/Bun包外消费。库05f6b5acf63a29d758e08e3162bba4c547dfda8d已普通推送，主仓gitlink精确固定；运行实现未因此变化。
- generation18最低主仓正式物理集205pass/1fail，8/8排空并回收accepted=true。唯一失败使用Node22尚无的DatabaseSync.isOpen；改为直接查询已关闭的原生连接并要求ERR_INVALID_STATE，继续检查close恰好一次、原始初始化错误和零spawn。Node22/26单项复验均1/0；完整集仍待重跑。打包初测因active角色白名单遗漏Advisor失败，按现行八角色合同补齐；没有放宽隐藏角色或旧工具限制。

- 正式发行01正确拒绝构建期间的证据文档变化；冻结后的02格式、静态、干净Fable generation19通过，171表面、825模块真实加载。单元运行416/870文件完成后触发既有5秒静默门禁，未形成权威计数，回收accepted=false，发行不通过；没有进入该入口后续集成/e2e/package阶段。原生旁证不代替此失败。
- 全集另定位5个普通失败：Cognition源码形状断言未容纳共享墓碑分支、Concern退休条款未声明旧MailboxSubscribed只读例外、两处同步委派夹具漏显式交付，以及Host adapter经大programming owner编译399个源文件越过392 ratchet。前四处按既有实际合同修正；最后一处将DeliveredValues原文件移归窄编译owner，Host只引用该owner，未提高预算或复制状态。合同原生复验10pass/0fail/2TODO，实际编译及正式复验待补。
- generation19真实主仓包在Node22和Bun均完成npm pack、隔离安装、离工作区导入和bundled库消费：1721项、14367538字节，issues为空；分别见host-package日志。库16入口另在工作区外从真实tgz安装后由最低Node运行TypeScript严格编译，退出0；HTTPS全新clone可干净检出05f6b5a。上述证据不包含Host部署。

- generation20实际编译和171表面/825模块加载通过；窄owner将Host adapter闭包从399降到381，交付值owner仅4个源文件，392预算不变。随后跨运行时物理选集与整仓源码扫描并跑，Node26组选集触发5秒静默，Node22为205pass/1fail（一个1500ms真实期限下的第二进程timeout），8/8排空；不计通过。无其他选集并跑时，同一最低Node期限反例2/0，断言和时限未改，后续物理验收串行。
- 发行超时的js-semantic-surface原生检查实际通过；重启Fission原生反例则先暴露未触发新版延迟Host激活，旧child仍处于Active。夹具补真实Manager chat.message并等待实际持久结算后，再显式resume、deliver、join，1pass/0fail/1TODO；未恢复启动自动重播，也不关闭GAP-149/158父项。第一次误用已禁用的general selector被如实拒绝，原日志保留。
- 格式/源码依赖/静态正式检查04退出0。此后统一构建、串行物理与相关正式复验、最终包闭包仍待完成。

- generation21串行复验：最低Node的physical-formal-03和Node26的physical-formal-02均206pass/0fail/0skip/0TODO，8/8排空、回收accepted=true。相关formal-02为242pass/0fail/9skip/9TODO；启用真实Git和源码pin/归档检查后formal-03为25文件255pass/0fail/0skip/10TODO，完整排空、回收accepted=true，严格退出1。十项TODO均保留，不构成完整发行通过。
- 本次只清理逐个核对过的独立施工工作树旧测试残留，TERM后未退出的确切9个PTY scope再KILL；原运行目录中已有的进程没有处理。后续旧活动资源由旧版本原协议收束，确认前不切换。原始清单和动作见own-residue-cleanup-01。

剩余：正式全集门禁、整个Long Stroke、通用异步CPU隔离、B2及其他旧TODO/CUT/GAP、Advisor晚到/并发/耐久归属、非作者复审和旧运行资源收束均未完成。Blogger/Chronicle全退役与旧GAP清零不在本批。最终仅继续核对并行master文档、冻结候选构建、正式入口和包闭包，结果如实写delivery.json；不再扩大产品施工范围。
