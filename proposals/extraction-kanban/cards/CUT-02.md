
**2026-10-15 修复轮：** 当前已接入完整 Capture、保留组长身份的监管及相对 PATH 修复；0.6.0 精确包、真实反例和剩余边界见[修复记录](../REPAIR-PROGRESS-2026-10-15.md)。下方保留历史接线和验证记录，不用它们替代本轮验收。

- **正控**：`010.test.mjs`（真实命令执行收集 `stdout`/`stderr` 与非零退出码）、`013.test.mjs`—`015.test.mjs`（大输出 Spool 留尾、TOML 数据面隔离、UTF-8 多字节码点对齐）。
- **反例**：`004.test.mjs`（慢进程超时强杀、不存在的二进制返回 `SpawnFailed`、Host 抛错返回 `ExecutionFailed`、`KillAckGraceMs` 未响应返回 `-1`）、`005.test.mjs` & `011.test.mjs`（非法预算在 spawn 前拒绝且零副作用）、`006.test.mjs`（取消立即返回且触发一次 Kill）、`016.test.mjs`（`LargeGate` FIFO 与 spawn 失败后释放许可）。
- **接线证明**：`010.test.mjs` 新增对 `NodeProcessHost.fs` 接入 `wanxiangli/process` 且移除 `node:child_process` 直接导入的验证，并通过真实 `ExecutorToolSurface.run` 与 `Process/Surface.run` 证明端到端连通。


## 4. 执行验证记录（2026-10-15，执行人：固定 DevOps）

验证对象是当前工作树（含本次未提交的切换改动），不是 git HEAD。

| 步骤 | 命令 | 结果 | 关键输出 |
|---|---|---|---|
| 构建 | `node scripts/build.mjs` | ✅ exit 0 | build up-to-date（产物已含工作树改动） |
| 静态门禁 | `npm run check` | ✅ exit 0（4.2s） | tsc 与静态门禁零错误；输出仅为既有跨工程命名冲突 lint 提示，与本卡无关 |
| process-execution | `node --test requirements/process-execution/tests/` | ✅ exit 0 | 186 tests：181 pass、0 fail、5 todo；`CUT_02_non_pty_host_wires_wanxiangli_process_session_and_retires_direct_child_process_spawn` 绿 |
| session 契约 | `node --test requirements/work/tests/009.test.mjs requirements/work/tests/044.test.mjs requirements/managed-session-lifecycle/tests/009.test.mjs requirements/managed-session-lifecycle/tests/025.test.mjs` | ✅ exit 0 | 25 tests：23 pass、0 fail、2 todo（均为预先声明的 GAP-133） |

已知 GAP 亮灯（均为测试标题预先声明的 todo 断言，不构成失败，与 CUT-02 无关）：
- GAP-091：`process-processrequest` 工程仍把请求词汇与 process/PTY runtime 编译在同一处（留给后续结构卡）。
- GAP-092：预算目前只作用于 raw output，不含信封元数据。
- GAP-133：受控 PTY port 无法在本测试环境观测真实进程退出与 KILL 升级（managed-session-lifecycle 009/025 各一处 todo）。

结论：CUT-02 的交接条件（构建零错误、check 零错误、process-execution 与 session/managed-session-lifecycle 相关断言全绿）在工作树上成立。git 提交留待协调集成队列，本次未执行。
