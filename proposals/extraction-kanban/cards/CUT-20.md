# CUT-20：PTY 与完整输出外存

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-20) / [证据](../evidence/CUT-20/README.md)。

## 1. 认领

- Owner：`eng-cut20-goose`；Reviewer：无独立人工 reviewer（仓主授权自治集成，见 PROTOCOL）；认领提交 `bb4ddf318`。
- Base：主仓 `48dff9349`（落地时 rebase 到最新 master），wanxiangli `a8b89a4`（落地时 rebase 到 `a0f0de9` / 0.12.0）。
- 工作树 `wt/CUT-20-main`、`wt/CUT-20-wxl`，分支 `cut/CUT-20`。
- 卡内按"先 spool，再 PTY"两步交付（CARDS 的顺序），每步独立落地。

## 2. 第一步：spool（完整输出外存）

### A. 范围

一句职责：一次运行的 stdout/stderr 在显式内存上限内留在内存，超过即把全部字节按到达顺序转入一个文件，失败如实报告。

旧 owner：主仓 `Process/Spool.fs`（StreamingSpool、`appendFileSync`、尾部读取）、`Process/ProcessOutput.fs`（三张 `List<byte[]>`、切换阈值）、`Process/NodeProcessHost.fs` 中的 spool 文件 I/O（tempPath/writeFile/appendFile/readFile*Chunks/deleteFile）。
调用者：`ProcessRunner`（唯一生产 run 路径）→ `Change/Host/GitAdapter.fs`（`OrchestratorGit.run`，生产全部 git 命令）；`Process/Surface.fs` 的测试面。

### B. 契约（wanxiangli/spool 0.13.0，对象 `OutputSpool`）

`new OutputSpool({ directory, memoryBytes })`；`append(channel, bytes)` → `Memory|File|Failed|Finished`；`inspect()`；`finish()` → `Memory{stdout,stderr}` / `File{path,bytes}` / `Failed{observed,written,path,error}`；`discard()` 只删本实例自己的文件；静态 `readChunks(path, chunkBytes, consume)`、`readTail(path, limitBytes)`。规则与证明见库 `src/spool/AGENTS.md`。

行为裁决：
- 切换点、合并流顺序、0600 文件与旧实现一致；旧实现的 `alignUtf8Tail` 会吞掉任意多个续字节，新 `readTail` 最多跳过 3 个（二进制尾部不再被大段吞掉）。主仓当前没有 `readLatestTail` 的生产调用者，故未接 `readTail`。
- 打开/写入失败：旧实现在数据回调里抛异常（结果不可知）；新实现返回 `Failed`，主仓映射为 `ProcessError.ExecutionFailed`（含已写/观察字节与真实退出码），并删除部分文件，绝不报完整。
- 封口后迟到的输出：旧实现继续写进已交出的文件；新实现返回 `Finished`、不计数。

### C. 反例（真实失败，base 上为红）

`OrchestratorGit.run` 把 `ProcessOutcome.Spooled` 映射成 `(code, "", "output spooled")`。生产 `finalizeWorktree` 用 `git grep` 扫冲突标记、用 `git diff --name-only --diff-filter=U` 查未合并路径；输出一旦超过 64 KiB 进入 spool，扫描结果就成了空串，带冲突标记的工作树会被提交，spool 文件还泄漏在 tmp 里。

base 实测（`evidence/CUT-20/base-red-git-grep-spooled.mjs`，6000 行冲突标记）：`{"kind":"Spooled","totalBytes":225783,"exitCode":0,"leakedSpoolFiles":1}`。超时/取消路径同样泄漏 spool 文件（旧代码无人删除）。

### D. 独立实现

库 `20e0372`（实现 + 15 项测试，含 ulimit 产生的真实 EFBIG、EACCES、ENOENT，真实子进程 3 MiB 字节比对，10 MiB 分块期间 `held ≤ 1024`），发布 `6bf65d3`（0.13.0）。隔离构建 `SPEC[build-001] spool` 通过。全量库测试 531/531（Node 22.21.1，`--test-force-exit`，因 `*-package.test.mjs` 在本机测完后不退出事件循环，属既有测试夹具问题）。

### E. 消费

仓规为 submodule + `file:wanxiangli`（禁止 tgz）：主仓 gitlink → `6bf65d3`，`package-lock.json` 中 wanxiangli 版本 0.9.0 → 0.13.0。该 gitlink 同时带入他人已发布的 0.10–0.12（prefix、identity、degeneration）；主仓不导入这三个入口（`rg '"wanxiangli/(identity|prefix|degeneration)"' src` 为空），不改变主仓行为。

### F. 真实链

- `ProcessOutput` 改为持有一个 `wanxiangli/spool` 实例的组合者；`ProcessRunner` 在超时、取消、spawn 失败时 `abandon`（删除文件），正常结束交给结果持有人。
- `OrchestratorGit.run` 对 Spooled 读出完整文件作为 stdout 并删除文件。
- 新测试 `WHAT[change-integration-003] real git finalization refuses conflict markers even when the scan output is spooled`：真实 git、真实 ProcessRunner、真实 spool，拒绝提交且 spool 目录为空。`process-execution-009` 增加大流逐字节、迟到输出两项。

### G. 旧 owner 退出

删除：`StreamingSpool`、`startStreamingSpool/appendStreamingSpool/readChunksSync/chunkBytes/spoolBytesToTempFile/retainLatestBytes/alignUtf8Tail/readLatestTail`，`OutputCollector` 的三张可变列表，`NodeProcessHost` 的全部 spool 文件 I/O 与 `consumeStreamAsync`，Surface 的 `spoolStart/spoolAppend/spoolPath/spoolBytesWritten/spoolRead/spoolBytesToTempFile/spoolChunkBytes` 及其旧测试（由库测试承接）。主仓留下：内存上限选择（`min(估计输出, 200 KiB)`）、结果映射、路径删除。

### H. 验收

- 主仓 `node scripts/build.mjs` 通过；`process-execution` 180 项：177 pass / 0 fail / 3 todo（todo 为既有 GAP-091 等预声明）；`change-integration` 158 项：144 pass / 0 fail / 9 skip / 5 todo。
- `scripts/check.mjs`：与同期 base 相比不新增问题（新增的两处 pyramid 已拆为私有函数）。
- 未跑全量 `scripts/verify.mjs` 与集成 run.mjs（共享机资源；见 PROTOCOL heavy 约定）。
- 已知不足：spool 模式下两路合并，GitAdapter 得到的是合并文本（stderr 混入 stdout）；之前是空串，严格更好，但不是两路分离。没有 fsync，不承诺掉电持久。macOS/Windows 未验证。
- 回退：显式 revert 主仓提交即恢复旧路径（无持久数据格式变化）；库 0.13.0 只新增模块。

## 3. 第二步：PTY

### A. 范围

一句职责：一个真实伪终端会话的打开、写入、读取、信号、尺寸与关闭（含整个进程组回收），如实报告每个动作结果。

旧 owner：主仓 `Process/PtySupervisor.fs`（bun-pty 句柄、`onData` 字符串缓冲、首字节读、`process.kill(-pid)`、孤儿 KILL）与 `PtySession` 的 `OutputBuffer/AwaitingFirstByte`。
留在主仓（组合与路由，不是物理）：`PtyPort`（id、parked read、exit 监听、完成邮箱）、`PtySupervisor` 的会话表与 pending 队列、bun-pty 驱动的加载（驱动选择属于组合根）、`HostForkPty`/委派侧适配。

调查结论（如实）：生产路径会构造 `PtyBackend.createPort()` 并在 teardown `CloseAll`，但当前没有生产调用者 `Fork` 一个 PTY（模型面的四个 pty 工具与 devops/run 已按 repository-programming[327] 退役，`js.call` 的 spawn/send/watch/signal 走 `Process/JsCallLoop.fs` 自己的管道）。但 process-execution、delegation、managed-session-lifecycle[025] 仍规定 PTY 合同，看板规则是"不用删功能换接线"，因此本卡抽取而不删除；是否整体退役 PTY 属产品裁决，不在本卡。

### B. 契约（wanxiangli/pty 0.15.0，对象 `Terminal`）

`Terminal.open({ driver, command, cwd, cols, rows })` → `Opened{terminal}` / `Failed{error}`；`write` → `Written|Closed|Failed`；`read(waitMs)` → `Read{output, closed}|Busy`；`signal(name)` → `Sent|Closed|Failed`；`resize` → `Resized|Closed|Failed`；`wait()`；`close(graceMs)` → `Exited{escalated}|AlreadyExited|Unconfirmed{pid}`；`inspect()`。规则与证明见库 `src/pty/AGENTS.md`。驱动（bun-pty `spawn`）由组合者注入，库零运行时依赖。

行为裁决（与旧实现的差异）：
- 不存在的 `cwd`：旧实现交给 bun-pty，实测 bun-pty 会**在别处照常运行命令**；新实现在调用驱动前返回 `Failed{operation:'cwd', code:'ENOENT'|'ENOTDIR'}`。
- 退出后写入：bun-pty 静默"成功"；新实现返回 `Closed`，主仓映射为 `Error "PTY exited before command was applied"`。
- 关闭：旧 `cancelAll`/孤儿路径只对组发一次 KILL；新 `close(0)` 先 TERM，未退出再 KILL，确认退出后再对组补发 KILL，忽略 TERM 的后代也被回收；无法确认时报 `Unconfirmed`。
- 退出时挂起的库读取：由库在退出时用剩余输出应答（`closed: true`）；主仓仍对端口上挂起的读执行 `FailRead`，残余输出进入完成结果，行为与旧实现一致。

### C. 反例（真实）

在 bun-pty 0.4.10 上实测：`spawn('sh',['-lc','echo hi'],{cwd:'/nonexistent/...'})` 照常输出 `hi`、退出码 0——命令在错误目录执行而无任何报错。新实现拒绝（库 `SPEC[pty-002] a missing cwd fails instead of running the command elsewhere`，主仓 `SUPERVISOR_spawn_reports_a_missing_cwd_instead_of_running_elsewhere`）。另：忽略 TERM 的后代在 `close` 后不存活（库 `SPEC[pty-003]`）。

### D. 独立实现

库 `e383e93`（实现 + 12 项测试，其中 8 项在 Bun 子进程中驱动真实 bun-pty：`test -t 0` 为真、写后读、UTF-8、首字节唤醒、沉默应答、Busy、resize 后 `stty size`、组回收、TERM 升级 KILL、INT），发布 `4f8e316`（0.15.0）。库全量 560 项：559 pass / 1 fail，失败项为 `SPEC[eventstore-001] two real processes cannot both win revision zero`（`database is locked`，共享机满载时的 SQLite 锁竞争），单独重跑 eventstore 15/15 通过，与本模块无关。

### E. 消费

候选验证时主仓 gitlink 为 `4f8e316`（0.15.0）；落地前 master 已由 CUT-19 把 gitlink 推进到 `c9a943d`（0.16.0，包含 `4f8e316`），rebase 后沿用 master 的 gitlink 与锁版本，并在其上重建复验。主仓 `PtySupervisor` 以 `import { Terminal } from 'wanxiangli/pty'` 使用；bun-pty 依赖仍在主仓（驱动由组合根加载并注入）。

### F. 真实链

`PtyBackend.createPort` → `PtySupervisor.spawn` → `Terminal.open(driver=bun-pty spawn)` → `attach` 以 `terminal.wait()` 驱动退出结算，`Read` 走 `terminal.read(250)` 后 `PtyPort.ReadResult`，`Signal/Write/Resize` 直达库。主仓测试在 Node 下运行（bun-pty 只在 Bun 下可加载），因此主仓测试用真实库 `Terminal` 包装一个脚本化的驱动句柄来检验路由；终端物理语义只由库在真实 PTY 上证明。

### G. 旧 owner 退出

删除：`killProcessTree`、`writeBackend/readBackend` 的直接 bun-pty 调用、`armFirstByteWait/resolveSilentRead/drainAwaitingFirstByte/appendLiveData/onSessionData`、`killOrphanTerm/killUnlessClosed`，`PtySession.OutputBuffer/AwaitingFirstByte`，Surface 的 `sessionAppendOutput` 与 `sessionView.output/awaitingFirstByte`。对应白盒测试改写为经库 `Terminal` 的路由测试。

### H. 验收

- 主仓 build OK；`process-execution` 182 项：179 pass / 0 fail / 3 todo。
- 其余引用 PTY 的 36 个测试文件：结果与同期 base 对照，见 `evidence/CUT-20/README.md`。
- `scripts/check.mjs`：154 issues，与 base 相同，无 PTY 相关新增。
- 已知边界：只在 Linux x64 + Bun 1.x 实测；bun-pty 不报告终止信号（`signal` 恒为 null）；`setsid` 脱离进程组的后代不回收；输出为驱动给出的字符串，不做字节级外存。
- 交接复验（eng-cut20b-goose，2026-10-20）：eng-cut20-goose 中断后接手，PTY 接线提交 rebase 到 master `1c90ecf69`、再 rebase 到 `587cd7b3c`（CUT-07 落地，gitlink 仍为 `c9a943d` / 0.16.0，已含 0.15.0 pty）后两次重新全量 `node scripts/build.mjs`：OK（1310 项，js-surface-manifest 151 / js-module-linkage 725 OK）；`process-execution` 182 项 179 pass / 0 fail / 3 todo（连跑两次一致）；`durable-convergence-011` 2/2；`scripts/check.mjs` 154 issues，与 base 相同。如实记录：加 `--test-force-exit` 跑全目录时 001 文件会被提前结束（122 项、10 cancelled），在未改动的 base 62e07b0c4 上三次复现完全相同，属既有运行器现象，非本卡引入。
