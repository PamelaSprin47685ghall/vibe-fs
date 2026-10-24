# advisor 测试与证明范围

本目录不定义额外规则；条款正文在 `../WHAT.md`，用例标题以 `WHAT[advisor-NNN]` 锚定条款。当前只有 002、003、006 三个条款有可执行用例，其余条款的证明缺口见文末。

- `002.test.mjs`（WHAT[advisor-002]）：原 carry 局部锚点与 TOML 记录测试（carry 与 VIEW_DECLARATION_INVALID 已退役，待重写适配 post-scope full-view 语义）。
- `003.test.mjs`（WHAT[advisor-003]）：原 delta 游标与增量材料跟踪测试（delta 游标已退役，待重写适配每次全量 post-scope view 与自然停机返回语义）。
- `006.test.mjs`（WHAT[advisor-006]）：生成成员的能力投影、`this.advisor(intent)` 真实编程执行中的数据返回及宿主缺席拒绝（待移除 carry 参数测试）。

## 证明缺口

- `001`（专席归属与家族根父级）：需要真实 advisor child 的建立与父子关系观察口，尚无本包用例；`Seat.consult` 的复用路径也没有断言。
- `004`（无工具、全量上下文返回、空回答按失败）：仅由 `006` 间接覆盖成员路径；"顾问没有工具"与"空回答即失败"尚无直接用例。
- `005`（无授权副作用）：运行时 capability 与 journal/authority 的观察口不在本包用例内。

`plan`/游标的用例只证明纯规划与进程内游标：它们不证明单一请求文档真的到达顾问，也不证明完整 post-scope 视图真的从真实 terminal 读回——这两件事归 001/004 的缺口。002 覆盖 en 与 zh-CN 的实际记录渲染；资源成对性仍由 language parity 门禁与 provider-language 承担。

纯规则本身（材料身份、增量、账本、读回）另由 wanxiangli `src/advisor/tests/001.test.mjs` 证明。本包 002/003 经主仓 dist 走的是同一个库实现（CUT-39 接线变异证据与限制见根 [AGENTS.md](../../../AGENTS.md) 的迁移章节）。
