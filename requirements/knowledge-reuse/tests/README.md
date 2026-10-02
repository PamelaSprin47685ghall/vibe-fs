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

GAP-160 汇总整链缺证。GAP-161 的 005/015/016 回归走真实 fetch、Bookkeeper 和 EventStore：维护读取不可变旧 payload，一次物理目标捕获同时提供差异与待提交基线；读取到 C 后立即把物理文件改成 D 的受控切点，要求本次只维护 B→C 并保存 C，下次才维护 C→D。无关的 tracked Git 修改不进入差异；空文本、Missing、新增、删除分别取证。多个分离变更形成独立 hunk，不携带中间未变正文；005 用225对小域输入的独立动态规划 oracle 核对最短增删数和精确重建，并单独覆盖 BOM、CRLF 和 EOF。未取得完整旧材料时不得凭旧 hash 猜正文。

016 对合法 UTF-8 比较完整原始字节，包括 BOM 和换行，保存原始 bytes，并核对其 SHA-256；旧 payload 缺失、不可读、内容损坏、添加 BOM 或非法 UTF-8 均须阻止维护。非法 UTF-8 的当前文件直接失败，不替换成 U+FFFD，也不冒充 Missing；这些测试不证明二进制 diff 支持。历史冻结已经丢失的 BOM 无法从旧 payload 还原，本次不自动改写历史 completionFileState。JSON 重复成员的完整拒绝仍不在现有解码证据内。

005 分别完整断言公开捕获结果的 diffSummary 和实际 Bookkeeper prompt，两者接收值逐字相等。字符串保真由公共 TOML writer 的 [GAP-081](../../GAP.md) 修复承载；Casebook 不用 trim 或复制编码规则补偿呈现错误。
