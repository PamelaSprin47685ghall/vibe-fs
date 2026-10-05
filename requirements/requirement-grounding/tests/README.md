# requirement-grounding 测试范围

本包已有实际领域、文件、日志与编程面的测试，本轮保留并补强，不把实现选择搬进 WHAT。

2026-10-05 N05-A/T386当前读取版本修复已验收：gen131正式相关212/212、1547pass/0fail/31skip/94TODO，166.97s，group回收通过；native006 integration3/0、无skip/TODO。新`RequirementGroundingReadObserved`保存实际返回版本与CompleteFile/PartialFile，旧`MaterialObserved`按原codec回放、不改写或追认历史。JS实际file/read捕获读时resolved path与返回UTF-8字符串，外root拒绝；grep保留freshness，不算完整读取。观察层不按路径重读新磁盘，partial不抑制全文，reanchor清空当前资格。见[本批记录](../../../proposals/archive/2026-10-05/实际读取版本与答案来源-2026-10-05.md)。

安装版Host1.18.29的七项真实read可捕获完整after output/title/metadata、SDK completed结果及下一次实际provider POST，三处保持一致；LF、CRLF、无末尾LF却在公开正文与metadata中坍缩，长行截断仍报告`truncated:false`，字节上限的totalLines也不表示完整文件。因此native返回一律PartialFile，无offset、EOF或该metadata均不授完整文件资格。正式integration入口位于006，沿原5s readiness/30s观察/45s子进程预算，并用实际晚到HTTP500验证失败不会被成功summary遮蔽。此协议观察不证明生产插件的全部Host业务接线，也不解决T388/GAP-086原Markdown表示问题。

- 001—004 检查真实目录发现及路径匹配。001 补嵌套包和伪装成 WHAT.md 的目录；002 补真实软链接、尚不存在的目标、断链与合法新路径。原测试只传普通外部路径，没有真的构造链接逃逸。
- 002 的自身目录子树声明有实际 TODO：当前检查器只试探 WHAT.md，漏掉仅匹配 tests 子树的自声明。完整包内覆盖和整个自身目录声明拒绝另有通过样例。
- 005 检查真实注入结果，只含包根普通 Markdown，来源按文件名排序；子目录中的 README.md 与名为 DIRECTORY.md 的目录不进入正文。
- 006 原返回v1却抑制磁盘v2、partial误作full、grep误登记、read后alias换目标与外rootsymlink反例均保留有效红证据；新公开registered JS、exact digest、重复/新版本、冷重启、reanchor及旧日志不重写已纳入gen131验收。native协议证明不冒称无损原字节，T388保留。
- 007 实际调用 pair/grounding 两个变换，核验三个 Provider 的 result-only、NUL+BOM、真实调用保持、候选发现豁免。测试手工规定调用顺序，不能证明生产插件所有路径都按这个顺序调用。原始 CRLF 正文被注释化/规范化的 TODO 见 GAP-086。
- 008 保留真实 journal 的 mutationDecision、投影后已读状态及损坏映射的弱观察断言；另经 withExecutablePlugin 的真实注册 before/after hook，检查受覆盖但未 grounding 的路径与确实无法解析的 APPLIES-TO 都允许受控 Host 写入继续，参数身份、值、键序及写后文件保持不变。已移除失效的源码位置检查；新增用例只证明 Hook 边界，不代表安装版 OpenCode 原生工具执行整链或任意时延上限。009 执行真实双文件程序，观察失败后提交仍保留。
- 010 执行真实可编程读取并检查同版本规范去重；和 006 一样，尚不覆盖读取后磁盘发生变化的版本一致性。
- 011/T389已验收：三provider知识交付后实际越权调用无文件写入，合法读取可用，未绑定会话无执行权；真实ToolRegistry错误放行变异可红。native5/0和gen127正式137选集证据见[本批记录](../../../proposals/archive/2026-10-05/Grounding权限与跨进程重放-2026-10-05.md)。
- 012/T390已验收：五个独立OS进程覆盖实际producer、删WHAT重放、追加v2、删整包重放及真实NDJSON损坏拒绝，核对原inline bytes/call id/After锚点；native7/0及gen127正式137选集见同批记录。当前payload_refs为空，不虚构Grounding blob合同。

正式构建后运行本包和 requirement-system；关联编译修复按 structured-workflow 012 分开验证真实编译与受控单元用例。GAP-085/086 不计作通过，发布节点保留 TODO。
