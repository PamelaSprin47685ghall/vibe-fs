# requirement-grounding — WHAT

## REQUIREMENT-GROUNDING-001: Workspace-local package 发现

Grounding 仅从当前工作区根目录下的 `requirements/` 目录发现 packages。直接子目录中存在 `WHAT.md` 即视为合法可接入的 requirement package；严禁在代码中硬编码特定的包名列表、源码根路径或固定包数量。

## REQUIREMENT-GROUNDING-002: Package 自身目录天然覆盖

`requirements/<package>/**` 路径天然归属于 `<package>` 自身覆盖范围，无需也不能通过 `APPLIES-TO` 进行声明。在 `APPLIES-TO` 中声明自身目录属于配置错误；排除规则（`!`）不得取消天然的 self coverage。

## REQUIREMENT-GROUNDING-003: APPLIES-TO 包外正向路径集合

`requirements/<package>/APPLIES-TO` 为可选的包外路径映射文件。其规则相对工作区根目录，遵循 gitignore wildmatch 模式语法：空行与 `#` 注释行忽略；普通匹配行将包外路径纳入范围，前导 `!` 的规则仅用于排除先前的匹配项。规则按文件声明顺序求值；缺失该文件时仅覆盖 package 自身目录。

## REQUIREMENT-GROUNDING-004: 多 Package 路径重叠解析

对给定的工作区相对规范路径，解析器必须返回所有匹配 package 的集合，严禁以“最后匹配”、“就近匹配”或单 owner 假设丢弃任何命中的包。解析结果按 package 名称升序进行确定性排序后进入后续流程。

## REQUIREMENT-GROUNDING-005: 规范材料严格限制为包根目录 Markdown

无论触发路径属于包外 `APPLIES-TO` 匹配还是包内自身目录（self coverage），规范接地自动载入的材料集合**严格限制**为 `requirements/<package>/` 根目录下直接存在的 `*.md` 普通文件，按文件名升序排列。任何时候**严禁**注入 `tests/**`（测试代码为可执行证明，非规范文本）以及 `APPLIES-TO`（元数据清单）或任何子目录文件。

## REQUIREMENT-GROUNDING-006: 可见材料按内容版本统一去重

Grounding 去重必须以当前 provider horizon 内执行者已经实际看见的规范材料为事实来源，而不是只记录“自动注入过哪些 package”。原生 `read` 与 `repository-programming` 的 `js-*` 文件读取只要返回了文件内容，都必须登记对应工作区路径与 content digest。若主动读取命中某 package 的根目录 grounding Markdown，该材料视为已经 grounding，当前轮与后续轮自动接入不得再次注入同一内容版本；未读材料仍可按需补齐。材料内容变更导致 digest 改变时允许读取新版本。发生上下文重锚 (`ContextReanchored`) 时，当前 horizon 的可见材料记录被清空，后续再次触碰需重新接入。

## REQUIREMENT-GROUNDING-007: 自动 Grounding 为普通 Read 的弱投影与 Cursor 补充

当直接 `read` 操作将代码暴露给执行者时，未 grounding package 的材料以完全等同于执行者主动调用 `read` 的普通 tool call/result 形式进入当前视界（同一能力权限、路径与范围规则、输出格式）。禁止引入私有的 bundle 格式或特殊 system 文本。
`repository-programming` 中任何 `js-*` 操作只要实际读取并返回文件内容，与原生 `read` 具有完全相同的 grounding 触发语义；工具名、宿主实现或是否通过 JavaScript 编程面不得造成旁路。
在同一轮次中若同时存在 pair-programming 伪技能与规范读取，固定顺序为：伪技能 → requirement reads。`grep`、`glob`、`list` 等候选发现工具不触发 `APPLIES-TO` 规范注入。
在 Cursor 环境下，规范读取结果以 `NUL+BOM` 分隔符追加于终端工具结果之后，并在外层携带工作区相对路径属性作为来源证明，正文内容保持原始读取字节不变。

## REQUIREMENT-GROUNDING-008: Mutation 不受 Grounding 阻断

当编辑、写入、移动或删除操作触碰受覆盖路径，而当前上下文尚缺相关 grounding 材料时，系统可以在同轮或紧随其后的 provider projection 中自动补入规范读取，但**不得阻止、延期、回滚、改写或要求重发原 mutation**。Grounding 不拥有 mutation admission，也不得把“尚未读规范”编码成工具失败。

## REQUIREMENT-GROUNDING-009: 批量与动态目标只用于 Grounding 发现

对于可能涉及多路径的批量操作或事务性程序，grounding 发现集合取实际读取或 source/target effect 路径命中的 package 并集。动态生成目标无需为了 grounding 建立 staging barrier；grounding 只观察已经明确的 read/effect set 并补充规范知识，不参与事务提交、回滚或重试决策。

## REQUIREMENT-GROUNDING-010: 跨工具源统一 Grounding Policy

Grounding 策略统一作用于所有明确的文件读取与修改行为。原生工具与可编程编程面（如 `repository-programming`）只要产生同类文件读取或文件后果，必须经过完全相同的路径解析、读取事实登记与去重逻辑；不得因工具名称或实现形态不同而漏触发 grounding，也不得在任一工具族上恢复 mutation barrier。

## REQUIREMENT-GROUNDING-011: Grounding 为认知知识而非 Authority

自动注入的规范文档仅作为观察事实扩充执行者的认识视界；严禁伪造用户指令、严禁创建或延续 Authority Root、严禁改变角色/身份绑定、严禁扩大工具权限。规范文档只能约束认知，不能自动赋予执行特权。

## REQUIREMENT-GROUNDING-012: Grounding Occurrence 语义历史与 Prefix 稳定性

成功完成的自动规范读取必须作为带类型的 occurrence 记录进执行者的语义历史中，保存调用标识、参数、原始结果字节及关联锚点；重试、重启与上下文续期必须从该事实原样重放相同字节，严禁重新读取文件或改变位置。内容变更产生的新规范读取仅追加于当前 wire 的末尾，历史 occurrence 永不改写，严格满足 append-only prefix 稳定性。
