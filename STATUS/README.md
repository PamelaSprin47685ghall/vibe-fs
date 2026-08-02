# Current Repository Status

## 当前基线

- 分支：`refactor/ssot-shock-anneal`
- 最后验证 commit：`38cc1882`（SSOT/14-16 纯领域内核合入后，退火三完成）
- 工作区干净；归档时退火分支无未提交内容

## 当前产品状态

生产可用。canary 森林 16/16 全绿，`test:release`（gate:static → build → unit → harness →
P0×3）完整通过。生产代码与测试整体迁移到 `SSOT/` 条款；测试体系为直接消费 `build/next`
发布产物的 `tests-mjs`。

## 当前开发阶段

SSOT/14-16（Strength / Enforcer / Student&Teacher）纯领域内核已合入并测试，生产接线被
各方案自设的 Host canary 门禁阻断（STRENGTH-078 / ENFORCER-180 / LEARN-082…088）。下一步
是建共享 Host capability canary 证明 transform 挂起/取消/身份绑定，再逐纵向接线（推荐
顺序：SatelliteRuntime → Projection DSL → Strength shadow → Enforcer → Student/Teacher）。

## 活跃阻塞

见 `STATUS/blockers/README.md`。当前仅一项：HOST-006 次生风险——Host 第二个 compaction
实现的运行时探测尚未完成。

## 下一步

1. 建共享 Host capability canary（transform 挂起/取消/身份绑定），解锁 SSOT/14-16 接线
2. 包 X10：`XPrefixProjection` / `AttemptPlanner` / `PrefixProbeSelection` 生产接线
   （当前 X 恢复链零生产调用点）
3. 包 K8f：X-A–X-D 剧本（依赖 X10 接线，否则剧本只能证明 mock 自己）
4. HOST-006 次生风险运行时探测（见 blockers/README.md）
5. `CompanionDelta.jsonDelta` 替换为包 X3 的 TOML delta（当前仍在 Submit 路径）

## 事实入口

- 正式规范：`SSOT/`
- 当前合规：`STATUS/conformance.md`
- 历史归档：`docs/archive/shock-anneal-2026/`（FINAL-REPORT.md + 原始证据）
- 发布证据：`docs/evidence/`
- 版本历史：`CHANGELOG.md`
