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

见 §4（落地后补写）。
