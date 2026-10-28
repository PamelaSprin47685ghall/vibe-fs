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
2. 待做：修复新版宿主输入、已退出接口等导致的失效测试；真实产品问题独立记录，不以改期望掩盖。
3. 待做：从完整文件生命周期定位主要成本，完成等覆盖的优化前后对照。

## 第一批：归属与有效证据

- 登记已经存在的 `latency-degeneration` 包，补齐 WHY、索引和消融节点。登记后暴露出的混合编号一并拆分；不修改规范检查器。
- Advisor 的送达与恢复测试分别归到现行 005、007；pending run 测试归到 delegation 025、026、031；Fission 单终态归到 009，provider 恢复分类归到 008。移动保留原行为断言。
- work 的宿主测试按 003、006、008、018 分组。移除 054 中扫描内部调用拼写的重复用例，其宿主行为由原有 011、039、051 及移动后的行为测试保护；将 055 的源码词形扫描换成公开 Work 状态迁移、完整事件序列与持久化重放测试，归到 001。
- 两项宿主屏障测试用真实临时目录路径，消除 macOS `/var` 与 `/private/var` 别名造成的提前失败；保留目录约束和屏障断言。
- Latency 的共享辅助代码只提取重复的受控端口与编解码组合。它不等于真实 PluginHooks 接线证据；物理 Sender 测试单列在 `host/009`。
- 验证：官方构建 generation 5 成功；规范、消融、Latency、Advisor、移动后的 delegation/Fission/provider/work 共 143 项，141 pass、0 fail、0 cancelled、1 skip、1 TODO。原有 skip/TODO 没有改为通过。日志为 `/private/tmp/vibe-fs-test-updates-ownership-verified-20261028.log`。

本批涉及用例归属和证据替换，不能把它与原失败基线的时间差直接解释为等覆盖提速。性能实验另建修复后的固定基线。

## 边界

完整 unit 不等于 format/check/build/unit/integration 的全流水线，也不等于真实宿主验收。后续结果仅适用于实际运行的版本与范围。
