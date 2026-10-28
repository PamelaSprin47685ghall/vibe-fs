# obligation-ledger

本包规定待办义务的边界：宿主原生 `todowrite` 退役与「不维护第二份待办账本」。规则正文见 `WHAT.md`。上下文重置唯一由 `resetScope` 管理。

## 测试对应

- 001：`tests/001.test.mjs` 验证 `todowrite` 不在 `StaticTools.knownToolNames`、角色权限规则与 `resources/ablation/tool-map.json` 中，`todowrite-for-*` 保持注册；集成用例验证 `todowrite-for-*` 调用不提交 checkpoint、不注入压缩字段。
- 002：`tests/002.test.mjs` 验证跨角色编程面调用不产生 checkpoint 窗口或第二份待办投影。
- 003–006：不维护第二份待办投影、失败执行不形成持久事实、重放/并发/WriterUnavailable 重试、声明无裁决权。
- 007：`tests/007.test.mjs` 验证 legacy Magic Todo envelope decoder 明确拒绝。

## 局部运行

`node --test requirements/obligation-ledger/tests/*.test.mjs`（构建须 `node scripts/build.mjs`；集成用例需 `WXS_TIER_INTEGRATION=1`）。
