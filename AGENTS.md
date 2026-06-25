---
import: 
  - README.md
---

- 允许改动 ../mux 代码，但最好只改 binding，对其他核心的修改要最小化。真正实现最好在本仓库，其次在 binding，最差在 mux 本体
- **oh-my-pi / OMP**：`@oh-my-pi` 宿主适配在 `src/Omp/`（入口 `Plugin.fs` → `kunweiExtension`），与 `src/Opencode/`（OpenCode/Mimocode）共享 `Kernel`+`Shell`；追赶 Opencode 能力时只改 Omp 层，禁止 `open Opencode`/`open Mux`
