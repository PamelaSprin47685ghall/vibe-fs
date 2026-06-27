---
import: 
  - README.md
  - PRD-FB.md
  - ../opencode-auto-fallback/README.md
  - ../opencode-auto-resume/README.md
---

- 提交仓库时候带上 kg/ 的变化
- Mux 端允许改动 ../mux 代码，但最好只改 binding，对其他核心的修改要最小化。真正实现最好在本仓库，其次在 binding，最差在 mux 本体
- Omp 端不允许改动 ../oh-my-pi 代码，但可以参考
- Opencode 端参见 ../opencode 代码，不允许改上游
- 本项目编译测试需要 20s 请合理设置超时
- Opencode 的大部分 hook 需要原地修改字段而不是换引用，否则不工作
