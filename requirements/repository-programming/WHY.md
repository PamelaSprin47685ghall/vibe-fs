# WHY — 为什么 repository-programming 必须独立存在

## 不可替代的存在理由

> 可编程 repository mutation 若拆成多套独立 RPC（`read` 一套、`edit` 一套、`write` 一套、`glob` 一套、`grep` 一套），path boundary、filesystem、result rendering、permissions、snapshot、string computation、transaction 会被**重复实现七遍并各自漂移**。一次 `js-*` 调用 = 一个受 capability 约束的 program：批量读、批量变换、批量写入在**一个事务**内完成。模型看到的编程 surface 必须与 runtime 真能执行的能力**同构**——这是本包不可替代的 WHY。

能力不是写进工具说明里的散文；**工具本身就是能力的投影**（`docs/why/js-tools.md`）：

```text
If a method is present, the capability exists.
If a method is absent, it does not.
```

模型不需要读权限矩阵，不需要记「文档里有但你不能用」。

## 为什么不能并入其它包

- 不是 `office-capability`：本包**不裁决权限**，只消费已经建立的 `AttemptExecutionProfile.ToolCapabilitySet`。权限从哪来（office consequence）是 office-capability 的事。
- 不是 `capability-enforcement`：capability → schema → runtime gate 的**同构/同源律本身**归 capability-enforcement；本包只把该律**应用**到可编程 SDK 面（四层 exactness、js-surface-gate）。去掉本包后，编程面可以合法退化成五套漂移 RPC 而不违反 enforcement。
- 不是 `durable-events`/`effect-accounting`：EventStore substrate 与效果分型归它们；本包保证的是「一个 program 的 staged mutation 是 all-or-nothing 且 durable prepare 只经统一 EventStore」这个**编程面合同**。
- 不是 `process-execution`：本包跑的是**模型 program**（数据），不是任意进程；sandbox 的「无 ambient authority + bounded」语义与真实 PTY execution 是不同 failure meaning。

独立 change 测试：把嵌入语言从 JavaScript 换成另一 embedded language/IR（只要 capability projection 与 transaction semantics 不变），本包命题全部成立——WHY 与具体语言无关（`17-repository.md` INDEPENDENT CHANGE）。

## RED 是什么样（失败模式）

```text
RED = 模型看到无权方法（surface 与 capability 脱钩）
    ∨ program 获得 ambient OS authority（sandbox 被绕过）
    ∨ multi-file mutation 半途落盘留下不一致世界（事务被破坏）
    ∨ 旧答案/伪造调用被当作已授权执行（gate 失效）
```

具体可观察形态：

| 形态 | 违反 |
|---|---|
| 生成 surface 出现 `rewrite()` 但 Attempt 没有 Edit capability（说谎生成器） | 001/002/005 |
| 手写一份 per-role js-* 工具名（`js-coder` 字面量出现在生产源码，非生成器） | 021 |
| 伪造 `js-coder` 调用被 ToolRegistry 接受（名字合法即执行） | 004 |
| model program 里能 `require('fs')` / 访问网络 / 拿环境变量 | 006 |
| `while(true){}` 挂死工具（无 deadline）或超大 return 打爆结果 | 006 |
| 程序 `rewrite()` 后 crash，磁盘停在半提交状态且无法从 EventStore 恢复 | 012/015 |
| 两个 mutation，第二个失败，第一个已写盘（非 all-or-nothing） | 013 |
| 外部进程改了文件，事务仍按旧快照覆盖（lost update） | 014 |
| 结果文档靠 `status = "ok"` 判别、程序值被 `JSON.stringify` 塞进字符串 | 016 |
| 锚点未找到被压成 `PROGRAM_FAILED`、或从 exception message 反推错误种类 | 018 |

## 历史失败（为什么这些命题不是纸上谈兵）

- **五套漂移 RPC 风险**（`changes/completed/js-capability-projected-tools.md`）：手写 `read/edit/write/glob/grep` 各一套实现会把基础设施重复七遍；generator 若接收 `role` 自行重算权限，会形成第二张漂移矩阵。
- **结果面曾经固化缺陷**（`changes/completed/js-tools-toml-result.md`）：JS-016 声称走 Synthetic TOML，实际把 `run()` 的 JSON `stringify` 进一个字符串字段——`status = "ok"` / `result = "{...}"` / 逗号拼接 `written`，两套语法叠信封，路径含逗号不可消歧。`requirements/repository-programming/tests/js-workflow.test.mjs` 的旧 golden 曾把缺陷锁成正确形状。本 Change 重做结果面（REPOSITORY-PROGRAMMING-016），`status`/`result`/`written`/`created` 是已拒绝方向。
- **Grep 曾被误判为「可表达即不需要 primitive」**（`docs/why/js-tools.md`）：`glob()+file()+RegExp` 可表达 grep，但 glob 假阴性让组合零命中；修正为 `ToolPermission.Grep` 投影为 Host `grep()` member（REPOSITORY-PROGRAMMING-009）。
- **Student/Teacher 已删**：`js-student`/`js-teacher` 是 G3 rebase debt，`FORBIDDEN_TOKENS` 保证它们永不复活（REPOSITORY-PROGRAMMING-021；DELETE 类 ratchet，`changes/completed/js-capability-projected-tools.md` §6）。

## 历史拒绝方案（被拒 ≠ 永久命题，记录 WHY）

| 被拒方案 | 拒绝理由 | 现行命题 |
|---|---|---|
| 五套独立 js-* RPC | 可编程路径共享全部基础设施，分五套必漂移 | 001 |
| 万能基类 + prose permission warning | 看得到无权方法增加认知负担；运行时才拒绝把错误留给调用之后 | 005 |
| alias / clean break 替换 builtin | 改名不改变执行语义却破坏 schema 合同；删除 builtin 迫使全量流量一次性迁移 | HOW（当前共存是现状，非永久合同） |
| 手写 role→JS 矩阵 | `ToolCapabilitySet` 已是唯一权威，第二份矩阵必漂移 | 001/021 |
| 模型 JS 拥有 ambient OS authority | 任意 JS 拿 fs/network/process/env 等于把 Host 权限交给 prompt 注入 | 006 |
| 事务先写盘再执行 | 崩溃后磁盘与 EventStore 事实分歧；编辑结果 commit 前必须不可见 | 012/013 |
| 结果 commit 后才验证 | 成功 return 与 commit 必须耦合；先验证后提交 | 011/019 |
| walk-then-filter + `**`→`.*` | DFS 字典序先进入 `.git`；硬上限打在枚举前缀而非匹配条数 | 008 |
| JSON 美化塞回字符串 / `status` 信封 / 逗号拼接路径 | 两套语法叠信封；路径含逗号不可消歧 | 016 |
| 可预见失败压成 `PROGRAM_FAILED` | `file()` 找不到锚点必须可区分；不嗅探 exception 文本 | 018 |
