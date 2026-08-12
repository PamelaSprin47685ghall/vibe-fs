# Agent — 理由

Role / Persona / Execution Binding 三层分离：职责、自我模型、物理执行者若绑成一词，fast/deep 会演化成两套产品，Peer Fallback 换模型时半途换人，Strength replica 也会漂出独立自我。Role 定 office；Persona session-bound 一次绑定不可变；Execution Binding 可随 fallback/Strength 变，不穿过 provider horizon——`fast-*`/`deep-*` 是机器路由身份，不是模型可见自称。

双层权限（Host schema + ToolRegistry）是因为 Host 配置可漂：只信一层会在配置异常时漏工具或越权执行。

`external_directory` 固定 allow 是对 Host 默认 ask 的显式覆盖，不是把路径逃逸塞进角色工具矩阵——后者会污染 AGENT-006 的语义边界，并诱导「工具白名单 = 一切权限」的错误心智。

内部 Agent 从 public enum 消失，是防止模型调用本应仅由运行时合成的 Blogger/Distiller/Bookkeeper 路径。

## Inquiry：inspect + Sphinx MCP + epistemic style（G3 / GrandRewrite / Sphinx）

**工具面：SyncDelegate `inspect` + Host MCP `sphinx_*`；删除 Inquiry 的 read/glob/grep。**  
若保留 filesystem 直读，产品无法回答「何时自己读、何时叫 Inspector」，必然退化成「便宜证据自己看、复杂再委派」——重复扫库、Inspector 上下文无法积累、角色边界靠 prompt 自觉。正确分层是 `Inquiry = reasoning`，`Inspector = evidence`（AGENT-025；边见 AGENT-024）。Sphinx 承接认识状态求解（SPHINX-001），不是第二套扫库工具。

**认知姿态：吸收 Student epistemic style，删除 Student workflow protocol。**  
保留：形成理解、主动反例、委派取证、证据/推论/不确定性区分、收敛前不草率终止。拒绝：LearningState、QA、Compile、MeditatorLearn/Compile、Student 式 final `return`。学习不再是特殊 Agent 程序，而是普通推理 + 同步证据委派；需要分形 co-yield 时走 Sphinx handle 会话（AGENT-030）。

**命名：Meditator → Inquiry。**  
旧名强迫模型解释「我不是你以为的那个坐着冥想的人」——坏 self-model。新名直指系统性认识过程。V1 只清坏 prior；不假装 Kernel/Sphinx 已存在。

**Student/Teacher：G3 已删除（AGENT-020…022 空缺）。**  
Catalog / Role DU 基线随 GrandRewrite 重命名后对齐；旧名 fail closed、无 alias。不得再写「pending deletion / 仍存在于生产」。

## Distiller：蒸馏非执行

**Executor → Distiller。**  
旧名事实错误：该 office 不执行命令、不改世界、不判 acceptance——它从过大输出中保留仍值得看见的观察。名错则 prompt 被迫道歉式纠偏，自我模型从入口就裂。map/reduce、chunk、session id 属机器 Assignment，不进 Role Law。

## 备选与被拒

**权限维度：Role 与 Tier/Binding 分离 vs 随模型分叉。** 拒分叉：fast/deep 随「换模型」演化成两套产品，Peer Fallback 丢对称前提（AGENT-001 不变角色）。GrandRewrite 再拆 Persona：换执行者 ≠ 换人；`fast-*`/`deep-*` 不得进 horizon 自称。

**身份轴：Role=Persona=Binding 合一 vs 三层。** 拒合一：Bookkeeper 用 `fast-inspector` 创建却收 Inspector prompt，是合一的病理实例。Persona 一次绑定；Binding 可变；Role 不变。

**权限载体：双层（Host schema + ToolRegistry）vs 单层可信。** 拒单层：Host 配置可漂；只信一层会在配置异常时漏工具或越权执行（AGENT-006 语义边界）。

**external_directory：固定 allow 元权限 vs 塞进角色工具矩阵。** 拒塞矩阵：污染 AGENT-006 边并诱导「工具白名单=一切权限」的错心智（AGENT-019）。显式覆盖 Host 默认 ask 即可。

**内部路径暴露：Agent enum 留内部 vs 隐去。** 拒保留：模型须无法合成 Blogger/Distiller/Bookkeeper 等运行时专用路径，只余外部可寻 office。

**Inquiry 工具：inspect + Sphinx MCP vs 保留 read/glob/grep + inspect。** 拒双持 filesystem：会重新变成「小 Inspector + 推理」，破坏证据获取与推理分层（AGENT-025）。Sphinx 是 Host MCP 认识机，不是直读面（AGENT-030）。

**Student 价值迁移：epistemic style → Inquiry prompt vs 整套 Learn/Compile/QA/return 改名留存。** 拒改名留存：那只是换皮的 Student 状态机；G3 删除 workflow、保留认知纪律。GrandRewrite 删除 SyncDelegate `return` 通道，彻底关掉 final-return 伪装。

**Student/Teacher 兼容 alias vs clean-break。** 拒 alias：`student`→Inquiry / `teacher`→Inspector 会永久污染身份边界。GrandRewrite 同样拒 Meditator/Executor 旧名 alias 与渐进双轨。

**角色命名：Meditator/Executor 保留 vs Inquiry/Distiller。** 拒保留：名与职责矛盾时，模型先学会自我否定再学 craft。clean break，无别名窗口。

**工具名：fork-manager/list/inspector(tool) 保留 vs commission/horizon/inspect。** 拒保留与别名并存：同名不同义、DTO 名冒充动词，强迫模型解码机器拓扑。People=nouns，Tools=verbs；同一动词全局一个 contract。

**Provider 迁移：alias / 渐进双轨 vs clean break。** 拒 alias 与渐进：双轨期间测试与 prompt 永远对齐旧面，机器 DTO 会借「过渡」留在 horizon。一次断，旧符号删。

**Browser 网络面：stealth-browser MCP vs 插件 `network` 工具 vs Host webfetch/websearch。**  
拒插件 `network`：没有真实 executor，schema 撒谎。拒全局 webfetch/websearch：那是 Host 通用网关，不是 Browser 专用隐身浏览面，且会漏给其它角色。选 OpenCode MCP `stealth-browser-mcp`：工具由 MCP 服务器定义，Host 负责 spawn/schema，Wanxiangshu 只注入服务器并按角色锁 `stealth-browser-mcp_*`。测试默认 disabled，避免 `uvx` 打真实 git。

**Sphinx 认识面：正交 Host MCP vs 内嵌万象术闭包 vs 无状态单次工具。**  
拒内嵌：闭包与 Agent domain 缠死，无法独立证明 Phase 0。拒无状态：无法表达 yield/resume 与 handle 会话。选 `src/sphinx` + `mcp.sphinx` 自动注入；仅 Inquiry allow `sphinx_*`；测试默认 disabled / fixture（AGENT-030、SPHINX-005）。Sphinx 允许 `@modelcontextprotocol/sdk`；Semble 仍禁止（AGENT-027）。

**内部 Semble：能力保留 vs Host 接线 / Strength 注入。**  
拒 Host mcp：语义搜索会漏进所有角色 schema。拒 Strength 注入：STRENGTH-004 Replica 只允许真实 `read/glob/grep`；假 read 污染 primary 可见历史。选进程内 stdio client：调用方显式 `search`，当前无调用者。测试默认 Disabled，避免 `uvx` 打真实 git。不引入 MCP SDK。
