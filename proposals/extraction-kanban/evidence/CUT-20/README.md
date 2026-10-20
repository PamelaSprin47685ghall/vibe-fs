# CUT-20 证据

- `base-red-git-grep-spooled.mjs` / `base-red-output.json`：base master dist 上真实 `git grep` 6000 行冲突标记 → Spooled，GitAdapter 映射为空 stdout，spool 文件泄漏 1 个。
- `wxl-full-test.summary`：wanxiangli `20e0372`（rebase 到 0.12.0 后）全量 `node --test --test-force-exit`：531/531，Node 22.21.1，Bun 1.4.2 存在。
- `host-process-execution.summary`、`host-change-integration.summary`：主仓候选（gitlink 6bf65d3）重建后的结果。
- 环境：Linux x64，Node 22.21.1（`/workspace/vfs-cut/tools/node22`），npm 10.x，Fable 5.13.0（dotnet tool），共享 8 核机。

## 第二步 PTY

- `wxl-pty-full-test.summary`：wanxiangli `e383e93` 全量（含 `src/pty/tests` 12 项）：560 项 559 pass、1 fail；失败项 `SPEC[eventstore-001] two real processes cannot both win revision zero`（`database is locked`，共享机满载下的 SQLite 锁竞争），单独重跑 `src/eventstore/tests` 15/15 通过。
- `host-pty-process-execution.summary`：主仓候选（gitlink 4f8e316）process-execution 182 项：179 pass / 0 fail / 3 todo。
- `host-pty-other-suites.summary`：其余 36 个引用 PTY 的测试文件（delegation、managed-session-lifecycle、work、host-boundary 等），候选与同期 base 逐项 ok/not-ok 完全一致（均为 153 pass / 7 fail / 48 cancelled）；这些失败与 cancelled 在 base 上已存在（如 `delegation-029` 词法审计、`host-boundary-026` 编译闭包、`managed-session-lifecycle-006` 的 `withForkRuntime` 结算悬挂），不是本卡引入。
- bun-pty 反例（不存在的 cwd 照常执行）：在 Bun 1.x + bun-pty 0.4.10 上直接复现，见 cards/CUT-20.md §3 C。
- `scripts/check.mjs`：154 issues（与 base 相同）。
