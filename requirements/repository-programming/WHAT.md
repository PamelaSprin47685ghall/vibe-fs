# repository-programming — WHAT

## REPOSITORY-PROGRAMMING-001: Capability 投影面与单一权限源

对每次 provider request，`js-ROLE` 主工具必须从唯一权威 `AttemptExecutionProfile.ToolCapabilitySet` 机械投影生成；严禁存在第二份 role→JS 权限矩阵，生成器不得在接收 role 后自行重算权限。当文件系统 primitive capability 集为空时，不得生成任何 `js-*` 工具。

## REPOSITORY-PROGRAMMING-002: 四层同构应用

对当前 Attempt 的每个 JS filesystem capability 所投影出的每个成员，以下四层必须保持严格一致；
一个 capability 可以投影一个有确定顺序的同权成员族（例如 Edit → `edit` + `rewrite`），但不得
借此产生第二份权限判定：
1. 生成的 `JsProgram` 基类中声明对应成员；
2. `js-*` 工具 description 中包含该成员的说明与规范；
3. Canonical examples 中包含该成员族的使用范式；
4. 底层 runtime gate 实施严格门禁（即使调用被伪造，仍 fail closed）。
能力缺失时四层同步缺失，模型可见的方法集即为运行时真正可执行的完整能力集。

## REPOSITORY-PROGRAMMING-003: 确定性生成

同一 Attempt profile（相同 capability set 与相同 role）必须生成字节完全相同的 surface（工具名称、JSON Schema、描述文案、基类定义与示例代码）。Tier（如 fast/deep）不得进入工具名称，不同 tier 下同 role 的 surface 保持完全等价。

## REPOSITORY-PROGRAMMING-004: 生成工具名门禁

工具执行入口仅接受当前 Attempt 的生成 surface 中合法拥有的 `js-*` 主工具名。调用未授权角色对应的工具名或旧 Attempt 残留名字必须 fail closed，工具名合法不可作为脱离当前 Attempt 权限执行的依据。

## REPOSITORY-PROGRAMMING-005: 编程面与推荐诚实性

`JsProgram` 基类仅包含当前真正可执行的成员；能力缺失的方法严禁出现在公开基类、描述文案或示例代码中。所有向模型推荐内置工具的说明钩子不得推荐当前 provider 不可见的工具；公开基类严禁包含宿主内部私有接口或注入键。

## REPOSITORY-PROGRAMMING-006: 沙箱隔离与无 Ambient OS 权限

用户 JavaScript 必须在无 ambient OS authority 的沙箱中执行：底层文件系统、网络、进程与环境变量严禁直接暴露；运行上下文仅接收显式注入的纯数据与安全代理原语。程序必须受硬性超时与内存/输出上限约束，超时立即终止并清理资源。标准输出/标准错误不作为编辑产物，执行结果仅由 `run()` 返回的结构化值决定。

## REPOSITORY-PROGRAMMING-007: 不可变快照与 Anchor 代数

`file(path, matches)` 读取当前事务的不可变 UTF-8 快照（非法 UTF-8 字节立即拒绝为 `INVALID_UTF8`，禁止猜测或静默修复编码），按声明顺序匹配锚点并返回不可变视图；`text(from, to)` 用于截取原文切片。锚点位移 `name±N` 中的 N 严格定义为已解码 JS 字符串的下标偏移（UTF-16 代码单元），严禁解释为行号或 UTF-8 字节数。声明冲突、空模式或按序未命中的锚点必须返回具名失败 `ANCHOR_NOT_FOUND`。同一 program 内部后续发生的修改不影响已获取的文件视图。

## REPOSITORY-PROGRAMMING-008: 确定性路径枚举

`glob(pattern)` 采用确定性 gitignore/wildmatch 路径枚举：`*` 不跨越目录分隔符，`**` 匹配零段或多段目录；严禁进入 `.git` 目录；严格应用各级 `.gitignore` 与 exclude 规则，不跟随符号链接。枚举操作不在工具内部截断，超限由宿主结果留尾机制统一收敛。

## REPOSITORY-PROGRAMMING-009: Grep Capability 投影

`Grep` capability 投影为宿主环境的 `grep(needle, pattern)` 原语：needle 支持字面字符串或正则表达式，pattern 沿用 glob 规则过滤文件。搜索在选中的严格 UTF-8 文件上执行，不可读或非 UTF-8 文件静默跳过而不中断全局执行；返回包含行列位置与匹配文本的结构化结果。单独拥有 Read+Glob 而无 Grep capability 时不得生成该原语。

## REPOSITORY-PROGRAMMING-010: Rewrite 与 Write 分离

`rewrite(path, newText)` 仅允许修改已存在的文件（目标不存在返回 `FILE_NOT_FOUND`）；`write(path, text)` 仅允许创建原本不存在的新文件（目标已存在返回 `FILE_ALREADY_EXISTS`）。同一程序在单次事务内对同一路径仅允许声明一次修改意图，重复声明同一路径立即 fail closed 并返回 `DUPLICATE_MUTATION_TARGET`。

## REPOSITORY-PROGRAMMING-011: JSON 兼容返回值与 Commit 前校验

`run()` 的返回值必须为严格兼容 JSON 的结构化数据（允许 `null`、布尔值、有限数字、字符串、普通数组与对象）。包含 `undefined`、BigInt、NaN、Infinity、函数、Symbol、循环引用或数组内含 `null`/异构类型的返回值必须在磁盘提交发生前判定为 `INVALID_RETURN_VALUE` 并终止事务。

## REPOSITORY-PROGRAMMING-012: 事务 Staging 与单一 EventStore 提交

单次 `js-*` 调用对应恰好一个事务上下文。所有写操作必须先进入内存临时暂存区 (ephemeral staging)，真实文件系统在 `run()` 成功结束前保持不变。事务的持久化 Prepare 与 Commit 事实必须且仅能提交至统一 EventStore，严禁引入专有文件或独立存储。

## REPOSITORY-PROGRAMMING-013: 多文件 All-or-Nothing 提交

单个程序对多个文件的全部修改必须在单个事务中原子提交：预检全部通过 → 记录持久化 Prepare → 按规范路径顺序应用修改 → 记录持久化 Commit → 向模型暴露成功结果。任一文件写入失败必须触发全量回滚，确保工作区呈现零修改。

## REPOSITORY-PROGRAMMING-014: 冲突检测与无隐式重试

事务提交前的预检基于读取时记录的文件快照指纹：若任一读取过的文件或目标写入文件在快照生成后被外部进程修改，事务立即失败并返回 `FILE_CHANGED`。宿主严禁执行自动重读、自动重新解析锚点或自动重跑程序的隐式重试。

## REPOSITORY-PROGRAMMING-015: 进程内正常回滚与崩溃后不自动补提交

单次调用在进程内部发生正常失败时，必须按 CAS 原则将已写盘的临时修改还原为原始状态（若文件内容已被第三方改变则不覆盖）。若进程在持久化 Prepare 后、Commit 完成前意外崩溃，后续启动流程不得自动重放、回滚或补齐该事务，未完成的 Prepare 仅作为工具中断审计证据，保持失败状态。

## REPOSITORY-PROGRAMMING-016: Synthetic TOML 结果面

工具执行结果统一渲染为严格的 Synthetic TOML 格式：
1. **成功**：首行为 `# ok`，程序返回值位于 `[data]` 节（或 `data = ...` / `[[data]]`），有实际磁盘写操作时在文末追加 `[fs]` 节声明 `rewritten` 与 `created` 路径列表；
2. **失败**：首行为 `# failed`，根级别输出稳定错误代码 `code` 与可读解释 `reason`，严禁包含 `[data]` 或 `[fs]` 节。
成败状态由文档顶级结构严格自证，禁止引入 `status = "ok"` 等歧义判别字段。

## REPOSITORY-PROGRAMMING-017: 并行调用安全与确定性串行提交

模型在单次助理消息中发出的多个工具调用在宿主侧按确定性顺序逐个执行。每个调用构成独立事务，后一个调用基于前一个调用提交后的最新状态重新获取快照，保证同文件并行编辑表现为顺序叠加无更新丢失，异文件并行编辑保持独立原子性。

## REPOSITORY-PROGRAMMING-018: 稳定失败代数

系统失败使用小而稳定的错误码枚举（如 `INVALID_PROGRAM`, `PROGRAM_TIMEOUT`, `FILE_NOT_FOUND`, `FILE_ALREADY_EXISTS`, `INVALID_UTF8`, `ANCHOR_NOT_FOUND`, `DUPLICATE_MUTATION_TARGET`, `FILE_CHANGED`, `INVALID_RETURN_VALUE` 等）。业务预期内的失败严禁被笼统压缩为 `PROGRAM_FAILED`；错误信息仅回显受控摘要，严禁泄露沙箱内部代码、宿主路径或敏感环境信息。

## REPOSITORY-PROGRAMMING-019: 返回值与 Commit 耦合

包含写操作的程序必须在返回值校验通过且事务 Commit 成功后方可暴露返回值；Commit 失败时严禁向模型返回业务结果。纯查询程序无需提交事务，校验返回值后直接暴露。若 staged 内容与原文件完全一致，跳过无意义写盘，整体判定为成功。

## REPOSITORY-PROGRAMMING-020: 文件变换的 POSIX 语义

文件变换原语必须保持标准 POSIX 语义：`mv` 负责移动或重命名文件与目录（包含覆盖语义，源路径缺失报错）；`rm` 负责删除文件与空目录，且**严格拒绝删除非空目录**。变换工具提供参数验证与稳定可读的操作系统级错误信息。

## REPOSITORY-PROGRAMMING-021: 禁止手写 per-role 工具变体

生产代码中严禁硬编码特定角色的 `js-*` 工具变体（除静态权限矩阵声明的枚举之外）。所有角色的编程工具必须统一由 `JsToolGenerator` 在运行时动态根据角色名称与权限集合构造生成。

## REPOSITORY-PROGRAMMING-022: 高显著性工具选择与失败经验引导

生成的工具描述文档必须先给出可执行的 canonical shape 与原语选择阶梯，再用高显著性风险提示
和明确失败经验巩固选择；禁止让较弱模型读完长篇事故叙事后仍不知道下一行代码该写什么：
1. 已知当前文本与目标文本的普通替换、插入、删除或全匹配修改，默认使用 `edit(path, changes)`；
2. 同一文件的多个独立局部修改合并为一个 `edit` 数组；当 `Read` 同时可用时，结构切片、重排、计算式变换才使用不可变快照、ordered anchors、`text()` 与 `rewrite()`；否则文档只能教授当前 surface 实际存在的 `rewrite()`；
3. 严禁在具备高级原语时默认退化为手工 `indexOf`/`substring` 边界计算或大面积盲目替换；Grep 仅发现候选，不承担结构所有权；
4. 示例必须覆盖 replace / insert / delete / all 的最小 copy-ready 形态，并保留一个责任形状的 Ultra Example 展示多文件事务；
5. 编辑必须在一个快照上形成完整目标状态后单次 staging，禁止把错误残留留给第二、第三个清理 program；
6. 程序应在返回前对关键不变量与数据规模进行前置检查，并在异常时主动抛出异常取消事务提交。

## REPOSITORY-PROGRAMMING-023: Edit capability 的渐进式成员族

`Edit` capability 必须按固定顺序同时投影两个同权成员：

1. `edit(path, changes)` 是普通局部编辑的默认入口。`changes` 接受一个 change object 或非空数组；
   canonical change 为 `{ find, put, all? }`，其中 `find` 是非空 string 或非零宽 RegExp，`put`
   是完整目标文本，`all` 缺省为 `false`；
2. `rewrite(path, newText)` 保留为完整文件替换的无上限逃生舱，不得因新增 `edit` 而削弱、隐藏或
   改变既有事务语义；
3. 一个 `edit` 调用的所有 change 都在同一个不可变目标快照上定位。缺省 change 必须恰好匹配
   一处；`all: true` 必须匹配至少一处并替换全部非重叠命中；
4. 所有 change 均解析成功且彼此不重叠后，才允许产生恰好一个 `Rewrite` staging intent。
   任一 change 失败时，该调用产生零 staging；
5. 字符串模式允许把一致的 CRLF 文件与调用方书写的 LF 视为同一换行语义，最终文件必须保持
   原有一致换行风格。除换行规范化外，只有精确匹配可获得写权限；
6. 为降低常见模型格式错误，单个 object 可自动包成数组，并可无歧义接受
   `oldText/newText` 或 `search/replace` 作为 `find/put` 别名；文档与生成示例只教授一套
   canonical `find/put` 形态；
7. change 必须是 plain object，除 canonical 字段与上述恢复别名外的未知字段必须拒绝为
   `INVALID_EDIT`，避免把 `al: true` 等拼写错误静默解释为默认值；所有纯参数规范化必须先于
   文件读取，使 malformed change 不被 `FILE_NOT_FOUND` 掩盖，也不污染 ReadSet；
8. edit 成功返回冻结的 `{ path, changed, operations, replacements }` 摘要。完整结果与原文相同
   时 `changed: false`、零 staging，仍视为成功；
9. edit 对目标的内部读取必须进入既有 ReadSet/快照冲突检测；外部修改发生在规划与 commit 之间时，
   仍按 `FILE_CHANGED` fail closed，绝不覆盖第三方新内容；该内部读取是 Edit 成员族的私有实现细节，
   不要求公开 `Read` capability，也不得让 `file()` 出现在 Edit-only surface。

## REPOSITORY-PROGRAMMING-024: 编辑失败恢复协议与保守近似

`edit()` 的可预期失败必须进入稳定失败代数，而非压缩为 `PROGRAM_FAILED`：至少包括
`INVALID_EDIT`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS` 与 `EDIT_OVERLAP`。这些失败必须满足：

1. 在任何 staging 发生前返回，reason 明确包含受控长度的 path、change ordinal、尝试的 find、
   失败种类与“本调用零修改”的原子性后果；
2. `EDIT_NOT_FOUND` 在 string 模式下返回最接近当前文本的有限带行号窗口；若唯一近似候选达到
   保守置信阈值且完整建议未超过诊断预算，还应给出只修正 `find`、保留原 `put` 的 copy-ready
   change。修正后的 `find` 必须是目标文件中真实存在的精确子串，不得以整行近似代替原 span；
3. `EDIT_AMBIGUOUS` 返回有限个候选行号/窗口，并明确给出两个合法下一步：扩充只有目标位置拥有的
   上下文，或仅在所有命中都应修改时设置 `all: true`；
4. 近似、编辑距离或标点容错只用于诊断与建议，严禁自动落盘。没有唯一证据时必须 fail closed；
5. 多个 change 在原快照上出现重叠时返回 `EDIT_OVERLAP`，要求合并成一个声明最终文本的 change，
   不得按数组顺序猜测覆盖优先级；
6. 所有窗口、字段名、候选数与 copy-ready payload 均必须有独立于文件/put 大小的上界；诊断预算
   不足时宁可省略建议，也不得让失败 reason 退化为超大输出或资源故障；
7. provider-visible 控制语必须完整本地化；稳定 code、API 字段名和 path 等协议 token 可保持原样，
   候选、行号、原子性后果与修复动作不得混入另一语言的说明句。

## REPOSITORY-PROGRAMMING-025: transaction fatal先settle cut-tail再经注入fuse执行

JS transaction invariant failure必须先完成CAS-preserving rollback或durable semantic cut-tail并取得committed/unknown settlement evidence，再构造typed incident。TransactionStore只接受composition注入的mandatory fatal capability，不得直接引用physical adapter、optional/default/global fallback。同一incident只允许一次report与kill；stale snapshot、第三方change与普通edit rejection保持typed nonfatal，fatal不得覆盖working tree。
