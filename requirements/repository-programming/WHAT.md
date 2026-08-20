# repository-programming — WHAT

## REPOSITORY-PROGRAMMING-001: Capability 投影面与单一权限源

对每次 provider request，`js-ROLE` 主工具必须从唯一权威 `AttemptExecutionProfile.ToolCapabilitySet` 机械投影生成；严禁存在第二份 role→JS 权限矩阵，生成器不得在接收 role 后自行重算权限。当文件系统 primitive capability 集为空时，不得生成任何 `js-*` 工具。

## REPOSITORY-PROGRAMMING-002: 四层同构应用

对当前 Attempt 的每个 JS filesystem capability，以下四层必须保持严格一致：
1. 生成的 `JsProgram` 基类中声明对应方法；
2. `js-*` 工具 description 中包含该方法的说明与规范；
3. Canonical examples 中包含该方法的使用范式；
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

生成的工具描述文档必须通过高显著性风险提示与明确的失败经验引导模型合理选择原语：
1. 优先使用不可变快照、ordered anchors 与结构化切片，严禁在具备高级原语时默认退化为手工 `indexOf`/`substring` 字符串计算或大面积盲目替换；
2. Grep 仅作为候选发现工具，不承担文件切片与重组语义；
3. 编辑操作必须在内存快照中完整构造目标状态后单次提交，禁止将初次错误编辑产生的残留留作多轮修补的工作流；
4. 程序应在返回前对关键不变量与数据规模进行前置检查，并在异常时主动抛出异常取消事务提交。
