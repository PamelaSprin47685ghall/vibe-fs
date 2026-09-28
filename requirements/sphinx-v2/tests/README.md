# sphinx-v2 测试证明范围

本目录是取代旧 epistemic-reasoning 的活动测试。WHAT 保持新上游 36 条合同，旧价格公式、阶段工具和内核不得通过测试迁回复活。历史对应见 [SUPERSEDES](../SUPERSEDES.md)。

本轮按当前条款修正错配：provider usage 归004，旧011的意图恢复分类归010，旧012的空工作分类归017，旧022的abstain/tie解码归025，Bayes局部模型计算归026。选择用例调用真实 Decision 选择路径；旧021的 Surface 自行排名/数值判别不再充当两阶段解释证明，也不保留其错误参数签名。

- `001/002`：目标与profile局部边界，不证明用户授权经过真实入口。
- `004/010/017/021/034`：用量分类、恢复动作与停止原因，分别保留真正运行/持久化/取消链的TODO。分类器返回“等待终止”不表示已经等待过子工作。
- `013/029`：给定候选与估值后的实际选择；不证明计划生成、Agenda、依赖成功或渲染预算已贯通。
- `014/023/025/026`：实际数值和协议函数的局部正反例；不把后验归一、标签拒绝、票型解码或数值稳定当作完整实验与保证传播。Bayes新增不完整/非法likelihood和零partition反例。
- `036`：生产工具名单允许七件套并拒绝旧阶段别名；实际 stdio 进程完成 initialize 和 tools/list，验证七件套 schema 可以注册。启动成功不证明业务请求已到达 Runtime。
- `011/012/022`：真实结果幂等、同轮乱序与measurement/intervention留为TODO，没有用无关分类器填补。

实际源码中仍有未接通边界：MCP注册的七工具handler均忽略参数并查询空inquiry的status；OpenCode adapter的ReadStatus、ReadResult、Reconcile仍为固定结果；Wire Surface的start/submit/status返回模板，不能作为真实运行证明。未在本轮扩大实现，更不能据此恢复旧内核。历史数据拒绝、原子batch、schema真实哈希、三哈希、全链cancel及数学保证仍需正式证据。

旧 Persistence Surface 已清退：它没有调用者，独立拼造导出声明，也不调用真实 Export；其登记的020实际要求三哈希，不能由这条接缝证明。真实 Export、Codec、Integrator 保留，清退无效接缝不代表持久化与导出行为已获验证。

v2 MCP是独立接入端口；Host原生`/sphinx question`适配器不启动/注入MCP，二者边界可以并存。工具名单存在不代表Host配置需要注入MCP。

局部：`node --test requirements/sphinx-v2/tests/*.test.mjs`。兼容修复后，真实 integration owner 的1020源文件Fable编译通过；17份原测试文件在字节相同的隔离目录中使用真实临时产物运行，27通过、12 TODO、0失败。正式dist与全局manifest未由此诊断生成，不能据此宣称全局门禁完成。详见 [兼容修复与验证记录](../../../proposals/20模块兼容修复-Sphinx集成-2026-09-28.md)；完整实施缺口见 [迁移记录](../../../proposals/20模块迁移-Sphinx与Manager-2026-09-28.md)。
