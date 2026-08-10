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
