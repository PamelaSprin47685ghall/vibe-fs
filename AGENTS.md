---
import: 
  - README.md
  - docs/
models:
  agents:
    build:
      - google/antigravity-gemini-3-flash-agent
      - omniroute-ai/qwen3.7-plus:fast
      - ark/minimax-m3:fast
    browser:
      - omniroute-ai/qwen3.7-plus:fast
      - ark/minimax-m3:fast
      - google/antigravity-gemini-3-flash-agent
    compaction:
      - ark/deepseek-v4-flash:fast
      - ark/minimax-m3:fast
      - google/antigravity-gemini-3-flash-agent
    coder:
      - stepfun/step-3.5-flash-2603:fast
      - ark/minimax-m3:fast
      - ark/deepseek-v4-flash:fast
    investigator:
      - stepfun/step-3.5-flash-2603:fast
      - ark/deepseek-v4-flash:fast
    meditator:
      - stepfun/step-3.5-flash-2603:fast
      - ark/minimax-m3:fast
      - ark/deepseek-v4-flash:fast
    reviewer:
      - ark/minimax-m3:fast
      - ark/deepseek-v4-flash:fast
    executor:
      - stepfun/step-3.5-flash-2603:fast
      - ark/deepseek-v4-flash:fast
    title:
      - agnes/agnes-2.0-flash:fast
      - ark/deepseek-v4-flash:fast
---

- Mux 端允许改动 ../mux 代码，但最好只改 binding，对其他核心的修改要最小化。真正实现最好在本仓库，其次在 binding，最差在 mux 本体
- Omp 端不允许改动 ../oh-my-pi 代码，但可以参考
- Opencode 端参见 ../opencode 代码，不允许改上游
- 本项目编译测试需要 20s 请合理设置超时
- Opencode 的大部分 hook 需要原地修改字段而不是换引用，否则不工作
- durable 状态（review/todo/nudge）SSOT = 工作区 `.wanxiangshu.ndjson`（见 `docs/05-event-sourcing.md`）；实施顺序：文档 → 测试 → 代码；勿用宿主对话历史或 compaction 补锚点作真相
