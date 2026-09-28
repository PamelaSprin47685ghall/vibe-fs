# Casebook 测试及证明范围

WHAT 是权威；本文不增加规则。先构建当前产物，再用 verification-system/tests/run.mjs 指定本目录编号文件并启用 WXS_TIER_INTEGRATION=1。TODO 不计通过。

- 001/005/011/015 运行实际 fetch、EventStore 和 Bookkeeper 事务，Host 端口受控。覆盖未变文件、缺项、成功刷新、失败保留双基线，以及受控暂停下并发请求共用一次维护；不是实际模型质量证明。
- 002/004/007/008/016 运行实际事实存储、冻结、内容寻址、重开、双基线推进及访问/淘汰。显式调用 finalize 不证明生产终结回调已接线；显式追加淘汰不证明容量压力自动触发它。
- 003 驱动真实实质访问记录器，证明提交/失败和路径归属；尚未覆盖每个实际文件工具的收集链。旧 grep/glob observation 编码和 shell 字符串识别不再冒充实质访问证明。
- 006 保留真实 js-bookkeeper staging、重复 setter/异常回滚、无绑定拒绝、受控 Host 拒绝。prompt/schema 只能说明传入了什么，不证明权限执行；旧 needsRefresh helper 的 replay 结果不作为现行维护证明。
- 009 证明无 marker 时实际 fetch 拒绝、显式 lifecycle 不归档；所有插件描述/索引/事件零影响待证。
- 010 保留显式 lifecycle 清理、重复 finalization 和同步委托组合；其中测试手工 notePrompt/noteAnswer 后归档，不能证明真实 resume 分段、Fission 汇合或生产 terminal 自动归档。
- 012 证明公开索引字段与寻址；epoch 是局部缓存证据，不证明完整 provider 低信任包装。
- 013 原成功写入/重开已由007承接；这些不是 crash cut。自建 fatal descriptor 与源码词形不能证明结算、报告和进程退出，改为 TODO。
- 014 证明字符预算截断器保留末尾及声明；实际请求的整体预算、所有变更路径保留和小预算后果待证。

GAP-160 汇总整链缺证。GAP-161 是可执行失败：持久基线含 payloadRef，但 computeMaintenanceDiff 用旧哈希代替旧正文；Map 基线的局部正例不能替代此路径。生产还分别计算 diff 和冻结目标，缺少一次捕获同时供二者消费的证据。删除的 singlePassDiffRefresh/applyExternalChangeToCase 只返回固定标记或改字段，没有执行维护；新用例走真实入口。
