# requirement-grounding — WHAT

## REQUIREMENT-GROUNDING-001: Workspace-local package 发现

Grounding 仅从当前工作区根目录下的 `requirements/` 目录发现 packages。直接子目录中存在 `WHAT.md` 即视为合法可接入的 requirement package；严禁在代码中硬编码特定的包名列表、源码根路径或固定包数量。

## REQUIREMENT-GROUNDING-002: Package 自身目录天然覆盖

`requirements/<package>/**` 路径天然归属于 `<package>` 自身覆盖范围，无需也不能通过 `APPLIES-TO` 进行声明。在 `APPLIES-TO` 中声明自身目录属于配置错误；排除规则（`!`）不得取消天然的 self coverage。

## REQUIREMENT-GROUNDING-003: APPLIES-TO 包外正向路径集合

`requirements/<package>/APPLIES-TO` 为可选的包外路径映射文件。其规则相对工作区根目录，遵循 gitignore wildmatch 模式语法：空行与 `#` 注释行忽略；普通匹配行将包外路径纳入范围，前导 `!` 的规则仅用于排除先前的匹配项。规则按文件声明顺序求值；缺失该文件时仅覆盖 package 自身目录。

## REQUIREMENT-GROUNDING-004: 多 Package 路径重叠解析

对给定的工作区相对规范路径，解析器必须返回所有匹配 package 的集合，严禁以“最后匹配”、“就近匹配”或单 owner 假设丢弃任何命中的包。解析结果按 package 名称升序进行确定性排序后进入后续流程。

## REQUIREMENT-GROUNDING-005: APPLIES-TO 外部命中只注入同层 Markdown

当 package 仅因包外路径命中 `APPLIES-TO` 而被触发时，自动载入的材料集合**严格限制**为 `requirements/<package>/` 根目录下直接存在的 `*.md` 普通文件，按文件名升序排列。严禁递归进入子目录（`tests/**` 绝不自动注入），`APPLIES-TO` 文件本身作为元数据亦不得注入。若触碰的是 package 自身目录，则允许使用完整的 package-owned 材料闭包。

## REQUIREMENT-GROUNDING-006: Grounding identity 按内容版本去重

自动接入的身份由工作区标识、package 名称与材料集合的确定性 content digest 共同构成。同一执行者在当前 provider horizon 内已完成某 identity 的自动读取后，后续触碰相同路径不得重复读取；package 内容变更导致 digest 改变时产生新 identity，允许重新接入。发生上下文重锚 (`ContextReanchored`) 时，当前 horizon 的覆盖记录被清空，后续再次触碰需重新执行接入流程。

## REQUIREMENT-GROUNDING-007: 自动 Grounding 为普通 Read 投影与 Cursor 补充

当直接 `read` 操作将代码暴露给执行者时，未 grounding package 的材料以完全等同于执行者主动调用 `read` 的普通 tool call/result 形式进入当前视界（同一能力权限、路径与范围规则、输出格式）。禁止引入私有的 bundle 格式或特殊 system 文本。
在同一轮次中若同时存在 pair-programming 伪技能与规范读取，固定顺序为：伪技能 → requirement reads。`grep`、`glob`、`list` 等候选发现工具不触发 `APPLIES-TO` 规范注入。
在 Cursor 环境下，规范读取结果以 `NUL+BOM` 分隔符追加于终端工具结果之后，并在外层携带工作区相对路径属性作为来源证明，正文内容保持原始读取字节不变。

## REQUIREMENT-GROUNDING-008: 首次 Mutation 先 Grounding 后新意图

当编辑、写入、移动或删除操作准备触碰受保护路径，而当前上下文尚未接入对应的 package identity 时，本次修改**严禁产生任何文件副作用**。系统必须拒绝当前修改并先行注入规范读取；仅在执行者获取规范事实后由其自主发出的下一次新修改调用才允许真正执行，严禁自动重放或静默继续被拦截的旧调用。

## REQUIREMENT-GROUNDING-009: 批量与动态目标按完整 Effect Set 准入

对于可能涉及多路径的批量操作或事务性程序，准入集合取全部 source/target 路径命中的 package 并集。动态生成目标的事务必须先在内存完成 staging 并解析完整 effect set；若检测到缺失 grounding，必须废弃未提交的 staging 并触发规范读取，严禁部分提交或自动重跑程序。

## REQUIREMENT-GROUNDING-010: 跨工具源统一 Grounding Policy

Grounding 策略统一作用于所有明确的文件读取与修改行为。原生工具与可编程编程面（如 `repository-programming`）只要产生同类文件后果，必须经过完全相同的解析器、去重逻辑与修改拦截门禁，不得因工具名称或实现形态不同而绕过规则。

## REQUIREMENT-GROUNDING-011: Grounding 为认知知识而非 Authority

自动注入的规范文档仅作为观察事实扩充执行者的认识视界；严禁伪造用户指令、严禁创建或延续 Authority Root、严禁改变角色/身份绑定、严禁扩大工具权限。规范文档只能约束认知，不能自动赋予执行特权。

## REQUIREMENT-GROUNDING-012: Grounding Occurrence 语义历史与 Prefix 稳定性

成功完成的自动规范读取必须作为带类型的 occurrence 记录进执行者的语义历史中，保存调用标识、参数、原始结果字节及关联锚点；重试、重启与上下文续期必须从该事实原样重放相同字节，严禁重新读取文件或改变位置。内容变更产生的新规范读取仅追加于当前 wire 的末尾，历史 occurrence 永不改写，严格满足 append-only prefix 稳定性。
