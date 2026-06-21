---
import: 
  - README.md
  - ../mux/src/node/services/vibeMeMuxBinding.ts
---

- 允许改动 ../mux 代码，但最好只改 binding，对其他核心的修改要最小化。真正实现最好在本仓库，其次在 binding，最差在 mux 本体
- opencode/mimocode 是相似的，mux 和他俩无关，是完全不同, 不能靠几个重命名解决
- opencode/mimo 可以通过不同入口点支持，而 mux 完全就是格外不同的支持方式