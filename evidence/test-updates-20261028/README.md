# 最新上游上的测试更新

本轮以 upstream `89ab1f39960e8338535e64f17ea51516b9913c93`、wanxiangli `87de56a6c203be4a79b71cb2386244e016b1e135` 为基线，保留 PR #68 的测试规则与读取支持。工作分支为 `codex/test-updates-20261028`，不扩大 PR #68，也不合并主工作树的 master。

用户选择：先修复现有失败，再优化主要慢点。按 `verification-system/AXIOMS.md`，保留现行合同的有效保护；归属调整、过时测试修订和等覆盖提速分别记录。

## 基线

- `node scripts/build.mjs` 成功，150 个 JS surface、753 个产物模块链接检查通过。此前缺三个产物的失败已消失。
- 规范相关 30 项：28 通过、2 失败。失败涉及包漏登记及测试归属，不修改检查器来豁免它们。
- 完整 unit：423.103 秒，793/793 文件完成，4073 pass、81 fail；另有 skip/TODO 与 cancelled，不能称全绿。源码输入摘要前后相同。
- 完整 unit 固定 Node 22.23.4、npm 11.12.1、并发 2、静默预算 30000 ms、总后备预算 900000 ms，沿用前次实验环境。没有跳过产物新鲜度检查。
- 原始记录：主工作树 `.Herdchestra/test-optimization/20261027/upstream-20261028-before.{json,log}`。记录中的 `WXS_ACCEPT_TODO=1` 仅容许原有 TODO，不把 TODO 算通过；不用于发布验收。

## 工作项

1. 已完成：按真实行为修复包登记、编号与归属，保留有效断言；替换无合同依据的源码词形断言。
2. 局部验证已完成，等待完整复测：修复新版宿主输入、已退出接口等导致的失效测试；真实产品问题独立记录，不以改期望掩盖。
3. 待做：从完整文件生命周期定位主要成本，完成等覆盖的优化前后对照。

## 第一批：归属与有效证据

- 登记已经存在的 `latency-degeneration` 包，补齐 WHY、索引和消融节点。登记后暴露出的混合编号一并拆分；不修改规范检查器。
- Advisor 的送达与恢复测试分别归到现行 005、007；pending run 测试归到 delegation 025、026、031；Fission 单终态归到 009，provider 恢复分类归到 008。移动保留原行为断言。
- work 的宿主测试按 003、006、008、018 分组。移除 054 中扫描内部调用拼写的重复用例，其宿主行为由原有 011、039、051 及移动后的行为测试保护；将 055 的源码词形扫描换成公开 Work 状态迁移、完整事件序列与持久化重放测试，归到 001。
- 两项宿主屏障测试用真实临时目录路径，消除 macOS `/var` 与 `/private/var` 别名造成的提前失败；保留目录约束和屏障断言。
- Latency 的共享辅助代码只提取重复的受控端口与编解码组合。它不等于真实 PluginHooks 接线证据；物理 Sender 测试单列在 `host/009`。
- 验证：官方构建 generation 5 成功；规范、消融、Latency、Advisor、移动后的 delegation/Fission/provider/work 共 143 项，141 pass、0 fail、0 cancelled、1 skip、1 TODO。原有 skip/TODO 没有改为通过。日志为 `/private/tmp/vibe-fs-test-updates-ownership-verified-20261028.log`。

本批涉及用例归属和证据替换，不能把它与原失败基线的时间差直接解释为等覆盖提速。性能实验另建修复后的固定基线。

## 第二批：现行输入、行为与资源

| 改动 | 原因与保留的保护 |
| --- | --- |
| 给有效 `chat.message` 夹具补齐宿主模型；共享 admission surface 显式接收目标 | 上游已改为使用 Host 选择。保留缺模型拒绝、同一物理输入的目标冲突、重复受理、旧租约及容量隔离。没有给产品加默认模型。 |
| 退役 MJS 调度加载、备用目标池和构建报告测试 | 用户按[处理说明](retired-tests-review.md)批准；有效容量保护移到 004/017，真实插件验证旧配置被忽略。报告工具的四项测试退出后，012 的其余编译测试从加载失败中恢复。 |
| 两个 delegation 恢复夹具先保留在途调用，观察物理接受，再等待 handler | 旧夹具先等待必须由下一步物理事件才能结束的 handler，每项耗尽约 10 秒。十种进程恢复情形均通过，每项约 0.45–0.64 秒；保留重复消息、旧调用不能结算新调用和返回前未完成断言。属于纠正因果次序，不作为独立等覆盖性能结论。 |
| Join 探针使用真实 PendingRunStore | Dictionary 已不能履行 claim/register 接口。保留实际自然完成和父级 join 保护，不修改生产委托流程。 |
| 万象理 UTF-8 尾部扫描改为迭代 | 大文本触发真实栈溢出；不缩小宿主原有大输入。新增十万字符 ASCII、中文与代理对回归，完整比较最长合法后缀和字节上限；纠正两个原本没触发截断的夹具与误把 row-10 当成 row-1 的断言。核侧提交 `9c0230b`。 |
| Plan 提示夹具使用真实自有首段 | 现行 transform 只处理自有提示。保留三个里程碑、草稿路径、幂等及替换断言，增加普通宿主提示不被改写的检查。 |
| classifier 诊断文本和工具目录更新 | abort/error 分类、错误来源及完结条件不变；固定无错误名称时的新文本。目录包含上游已经注册的 todowrite。 |

## 第三批：公开边界与检查归属

- 日志根路径、Scope 记忆和 scope-progress 改用已有公开接口；没有重建产品模型。
- 为现有 ToolRuntimeScope、Dispatch、HostSignalSubscribe 和 Planning surface 补齐原生 JS 调用；Result 转换由所属 F# 边界完成，测试仍调用真实 sender、观察实际 PhysicalAccepted，并保留未知送达、迟到拒绝、Scope 退役和资源清理检查。没有新建跨领域 facade。
- 登记 LatencyWatchdog 已有的稳定工厂与不透明句柄，不暴露内部状态表示。修正辅助代码错误读取 `case/fields`，改读 HostSignalSurface 实际返回的 `kind/sessionId`。
- 三个原始宿主边界用例登记为精确路径的 canary：delegation 037 检查投影前原始历史；managed-session 029 检查真实事件到 typed observer 的运行身份；work/host 008 检查 OpenCode 与 OMP 的实际提示屏障。相邻语义用例及辅助文件继续受检，新增子进程正反例确认没有目录级豁免。
- `durable-events-010` 允许不同职责采用不同物理格式；`advisor-007` 已明确其 SQLite owner。旧检查错误拒绝两个宿主适配器中的 `advisor-process.sqlite`。只认可这两处的该字面量，其余 SQLite、私有库、旁路写入及相邻文件仍拒绝，并用受控反例验证。不豁免整个适配器，也不改变 EventStore 折叠规则。
- 为现有可变回调、取消登记、订阅游标、试验标识和 watchdog 代次补充准确的资源用途注释；没有改弱可变状态检查器。

局部验证：公开接口与调用方 87/87 通过；检查器最后一轮 23/23 通过；durable-events 019 的 18 项通过。前面的试跑失败和修正分别保留在 `/private/tmp/vibe-fs-test-updates-*.log`。完整套件状态以随后固定输入的运行记录为准，这些局部通过不代替整套结果。

完整试跑 `contracts-20261028-before-split`（`c589d8223`）：341.540 秒，800/800 文件完成，4166 pass、1 fail、110 skip、261 TODO，输入前后稳定。唯一失败是 host-boundary 021 仍要求调用已退出的模型改写端口；改为检查现行目标比较与事务接线，并拒绝旧改写端口。对应结构测试和真实宿主模型测试 21/21 通过。此轮保留为失败记录；随后重建固定基线，不将它与原 81 项失败基线的差值算作等覆盖收益。

## 边界

完整 unit 不等于 format/check/build/unit/integration 的全流水线，也不等于真实宿主验收。后续结果仅适用于实际运行的版本与范围。
