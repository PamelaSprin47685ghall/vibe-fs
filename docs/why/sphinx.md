# Sphinx — 理由

可观察语义见 `what/sphinx.md`。本页只解释权衡。

## 为何是认识状态求解器，不是一次生成答案

自由文答案把「知道什么 / 未知什么 / 该不该再问」压成黑箱。Sphinx 把充分统计量显式化为 EpistemicState，使闭包、判重、停止与 Canonical Answer 可审计、可重放。LLM 只做 language → structured observation；控制权留在 Kernel（SPHINX-001）。

## 为何 handle 有状态

无 handle 的无状态工具会把续作语义推给调用方 transcript，Kernel 失去唯一权威状态。不透明 handle 绑定进程内 EpistemicState：缺柄即错，同柄续作，V1 不跨进程持久化——先证明 co-yield 骨架，再谈 durable journal（SPHINX-002）。

## 为何 Kernel 拥有 continuation

若 LLM 与 Kernel 平权决定下一步，方法选择、停止与报告内容会漂回 prompt 自觉。固定「fixed point → 选动作 → 必要时 yield → absorb → Closure」使 LLM 无法跳过闭包或自封 answered（SPHINX-001/004）。

## 为何 Phase 0 不做 A* / Bayes / MCTS

它们是统一认识模型在特定约束下的退化求解器，不是认识论本体。Phase 0 只证明 Core Reduction + co-yield + Stop + Canonical Answer 能跑；过早嵌入完整搜索/概率图/蒙特卡洛会把表示优化与控制论缠死，阻塞骨架验收（SPHINX-004）。

## 为何正交 SDK 产品

Sphinx 内核是独立认识机，不应绑进万象术 domain 生命周期。`src/sphinx` + MCP stdio 使闭包可单测；万象术只做 identity / launch / Meditator 权限。允许 `@modelcontextprotocol/sdk`：Sphinx 是真 Host MCP 服务器，SDK 是标准货币。AGENT-027 仍禁止 Semble 路径引 MCP SDK——Semble 是进程内搜索 client，不是 Host MCP 面（SPHINX-005）。

## 备选与被拒

**无状态单次 ask vs handle 会话。** 拒无状态：无法表达 yield/resume 与闭包不变量。

**LLM 写最终答案 vs Kernel Canonical Answer。** 拒 LLM 终稿：认识内容会逃出状态机。

**内嵌万象术 vs 正交 MCP。** 拒内嵌：污染 Agent 矩阵与测试边界；闭包无法独立证明。

**Phase 0 先做完整 A*/Bayes/MCTS vs 先骨架。** 拒先增强：验收无法区分「求解器插件」与「认识内核」。
