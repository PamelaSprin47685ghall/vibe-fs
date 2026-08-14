# JS Tools — 理由

可编程文件系统面不能做成第五套独立 RPC 工具。`read` / `edit` / `write` / `glob` / `grep` 各自实现会把 path boundary、filesystem、result rendering、permissions、snapshot、string computation、transaction 重复七遍并漂移。一次 JS 工具调用 = 一个受 capability 约束的 program，批量读、批量变换、批量写入在一个事务内完成。

能力不是写进工具说明里的；工具本身就是能力的投影。LLM 只对准确生成的 SDK 编程：`If a method is present, the capability exists. If a method is absent, it does not.` 四层同构（capability → base-class method → description → example → runtime gate）保证模型看到的与可执行的完全一致，不需要读权限矩阵。

内置文件系统工具（`read` / `edit` / `write` / `glob` / `grep` / `patch`）是 LLM 训练中极强的工具选择 affordances，且既有 schema/实现已是正式合同。本 Change **不替换**它们：builtin 是兼容面，`js-*` 是推荐面，Tool Definition 钩子是引流面。三者不是 alias，不存在 schema takeover。

## 备选与被拒

**五套独立 js-* 实现 vs 单一 capability-projected SDK。** 拒前者：可编程路径共享全部基础设施，分五套必然重复并漂移（JS-001）。

**万能基类 + prose permission warning vs 精确生成。** 拒万能基类：看得到无权限方法本身就增加模型认知负担和误调用率；运行时再拒绝等于把错误留给调用之后（JS-002/004）。

**alias / clean break 替换 builtin vs additive coexistence。** 拒 alias：改名不改变执行语义，却破坏既有 schema 合同；拒 clean break：内置工具名是正式工具面，删除会迫使全部模型流量一次性迁移。共存 + description 钩子引流，迁移由模型流量自然完成（JS-003/017）。

**手写 role→JS 矩阵 vs 从唯一权威投影。** 拒手写矩阵：`AttemptExecutionProfile.ToolCapabilitySet` 已是权限唯一权威，任何第二份矩阵必然与它漂移。generator 不重新决定权限，runtime 不从 description 解析权限（JS-001/004）。

**模型 JavaScript 拥有 ambient OS authority vs sandbox。** 拒前者：任意 JavaScript 直接拿到 fs/network/process/env 等于把 Host 权限交给 prompt 注入。runner 只获得数据，不获得文件；deadline 可 kill；memory/output bounded（JS-011）。

**事务先写盘再执行 vs staged + all-or-nothing。** 拒半途可见：编辑结果在 commit 前必须不可见，否则崩溃后磁盘与 EventStore 事实分歧。durable prepare 只经统一 EventStore，禁止 `js-transaction.db` / feature store（JS-012/015）。

**结果在 commit 后才发现不可用 vs 先验证后提交。** 拒前者：result validation 必须在 commit 前，成功 return 与 commit 耦合（JS-013）。

**walk-then-filter + `**`→`.*` vs gitignore wildmatch。** 拒前者：DFS 字典序先进入 `.git`，硬上限打在枚举前缀而非匹配条数，`src/**/*.fs` 在真仓库变成空集；naive `**` 还要求额外目录段。有界必须打在匹配结果上；pattern 方言必须是 gitignore/wildmatch（含零段 `**`、无斜杠则任意深度、应用 `.gitignore`、永不进入 `.git`）。截断必须是返回值上的可见位，不能伪装成「无匹配」（JS-007）。

**Grep 仅作 `glob()+file()+RegExp` 组合 vs Host `grep()` member。** 原文 `#5` 以「可表达」否定 primitive。可表达 ≠ 实用：glob 假阴性让组合零命中；即便 glob 正确，沙箱内逐文件 `file()` 仍被 timeout / `RESULT_TOO_LARGE` / 二进制文件放大。修正案：`ToolPermission.Grep` 投影为 Host `grep()`（gitignore 选文件、跳过非 UTF-8、返回 path+line+column+匹配子串、匹配条数有界且截断可见）。builtin `grep` RPC 仍独立存在。Read+Glob 而无 Grep 时组合仍合法，不再是唯一搜索面（JS-020）。

**结果：JSON stringify 进 TOML 字符串字段 vs 值树进 SyntheticToml。** 拒前者：`status` / `result = "{...}"` / 逗号拼接 `written` 是两套语法叠信封；空字符串噪声；路径含逗号不可消歧。`run()` 的 JSON 兼容值仍是沙箱边界；LLM 看见的是 ARCH-010 文档。Host 事实与程序值分表，不用 `status` discriminator（JS-016）。

**可预见失败压成 `PROGRAM_FAILED` 且丢掉 throw message vs 结构化 sentinel。** 拒前者：`file()` 找不到锚点、目标缺失必须是 `ANCHOR_NOT_FOUND` / `FILE_NOT_FOUND`；普通 throw 的 `reason` 必须含 message。分类看 `__jsFailure.code`，不嗅探 exception 文本（JS-019）。

**锚点只能切声明 span vs `name±N` 临时 caret。** 拒只能声明：读一窗正文时不该再钉一个假 pattern。位移 clip 到闭区间 `[0, file_len]`，EOF 与 `$` 对齐（JS-005）。

**行号位移 vs 字符串下标位移。** 拒行号：`grep()` 的 `line` 是 1-based 行坐标，`text()` 切的是 `source.slice`。两套单位并存时，`h1+200` 会被读成「往下 200 行」。N 必须与 `String.length` 同单位，这样 `'hello world'` 上 `h+6` 得到 `'hello '`，而不是整文件（JS-005）。

**数组 `null` / 异构对象数组放行 vs commit 前 `INVALID_RETURN_VALUE`。** 拒放行：TOML 不能诚实表示 `null` 元素，对象与原始值混列也无法用一种 array 记法。对象字段 `null` 省略；顶层 `null` 无 data 体（JS-010）。
