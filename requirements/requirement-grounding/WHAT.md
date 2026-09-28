# requirement-grounding — WHAT

## [001] Workspace-local package 发现

只从当前工作区根目录的 `requirements/` 发现包：直接子目录含 `WHAT.md` 文件即为合法包。不得硬编码包名、源码根路径或包数量。

## [002] Package 自身目录天然覆盖

`requirements/<package>/**` 天然由该包覆盖；`APPLIES-TO` 不得声明或取消自身目录覆盖，声明自身目录属于配置错误。

## [003] APPLIES-TO 包外正向路径集合

可选的 `requirements/<package>/APPLIES-TO` 定义包外路径，采用相对工作区根目录的 gitignore wildmatch 语法。忽略空行与 `#` 注释，普通行纳入匹配项，前导 `!` 排除先前匹配项，按声明顺序求值。文件缺失时仅覆盖包自身目录。

## [004] 多 Package 路径重叠解析

返回路径匹配的全部包，按包名升序排列；不得按最后匹配、最近位置或单一所有者丢弃命中。

## [005] 规范材料严格限制为包根目录 Markdown

无论包内或包外路径触发，自动载入只包含包根目录直接存在的 `*.md` 普通文件，按文件名升序排列。不得注入 `APPLIES-TO` 或任何子目录内容，包括 tests。

## [006] 可见材料按内容版本统一去重

以当前视界实际看见的材料为去重依据。所有返回文件内容的读取均登记工作区路径与内容摘要；主动读取与自动注入共享记录，同一规范材料内容版本不重复注入，未读或已变更版本可补齐。`ContextReanchored` 清空当前可见记录，后续触碰重新接入。

## [007] 自动 Grounding 为终端结果的 result-only 字节补充

明确文件读取将代码交给执行者时，补入尚未看见的适用规范。所有 Provider 均在真实终端工具结果后以 `NUL+BOM` 分隔追加，每份材料带 `requirement_source_path = "requirements/<package>/<file>"`，正文保持原始读取字节。

不得生成 synthetic read 调用/结果对、私有 bundle 或特殊 system 文本。同轮顺序固定为 guidance → requirement reads；grep、glob、list 等候选发现不触发注入。原生与可编程文件读取均适用。

## [008] Mutation 不受 Grounding 阻断

修改触碰受覆盖路径而尚缺规范时，可以在同轮或紧随其后的投影补入。Grounding 不得阻止、延期、回滚、改写或要求重发原操作，不拥有修改准入，也不得把未读规范作为工具失败。

## [009] 批量与动态目标只用于 Grounding 发现

批量或事务操作取实际读取及 source/target effect 路径匹配的包并集。动态目标只按已明确路径补充知识，不为 Grounding 建立 staging barrier，不参与提交、回滚或重试决策。

## [010] 跨工具源统一 Grounding Policy

同类文件读取与修改使用相同的路径解析、读取登记和去重规则，不因工具名称、来源或实现形态漏接入，也不得在某个工具族恢复 mutation barrier。

## [011] Grounding 为认知知识而非 Authority

规范注入只补充知识与约束，不伪造用户指令，不创建或延续 Authority Root，不改变角色或身份绑定，不扩大工具权限。

## [012] Grounding Occurrence 语义历史与 Prefix 稳定性

成功的自动读取以类型化 occurrence 进入语义历史，保存调用标识、参数、原始结果字节与锚点。重试、重启和续期原位重放，不重读文件或改写历史；内容变更产生的新读取只追加到当前 wire 末尾，保持 append-only prefix。
