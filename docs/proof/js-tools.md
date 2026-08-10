# JS Tools — 证明义务

## 证明义务清单

| 义务 | 证明方式 |
|---|---|
| **Generator equivalence** | 枚举所有 canonical profile（capability 子集 × 角色），证明生成 surface 与 profile 完全同构；fast/deep 相同；deterministic（同 profile 同字节） |
| **Lying generator counterexample** | 构造"说谎生成器"（生成方法但 runtime 无绑定 / 描述含不存在方法），证明 gate 拒绝 |
| **Four-layer exactness** | capability absent → member absent → description absent → example absent → runtime gate fail closed（每层独立测试） |
| **Builtin coexistence + hook** | read/edit/write/glob/grep/patch 原 schema/实现可执行；hook 文案只改 description；js-ROLE 名同时 provider-visible；无 alias takeover |
| **Anchor/regex** | 有序匹配、消歧、零宽、`^`/`$` 绝对语义、5 类拒绝 |
| **read/glob/grep** | 快照隔离；UTF-8 拒绝；有界确定枚举；capability 边界外不可见 |
| **write/rewrite 区分** | 目标缺失/存在的双向失败 |
| **Structured return** | golden 形状稳定；query 零 mutation |
| **Multi-file transaction** | preflight 全过才动；任一失败全部零提交；同路径单意图；无 lost update |
| **Conflict** | 快照后外部修改 → FILE_CHANGED fail closed |
| **Rollback** | normal 失败路径 staged 效果归零 |
| **Crash recovery** | 只从 EventStore facts/payloads 重建；未 commit 无效果；已 commit 重放一致；禁 js-transaction.db（static gate） |
| **Sandbox** | 无 ambient authority（fs/network/process/env 不可得）；escape RED 测试；deadline kill；memory/output bounded |
| **Parallel** | 同消息串行确定性；同文件无 lost update；异文件独立 all-or-nothing |
| **G3 rebase debt** | production 无 js-student / js-teacher / StudentLearn / StudentCompile（static ratchet） |
| **Static gates** | spec / architecture / unified-store-gate（no feature store）/ g4r-freeze / lint 全绿 |

## 门禁

```text
node scripts/checks/spec.mjs
node scripts/checks/architecture.mjs
node scripts/checks/unified-store-gate.mjs
npm run build
node tests/unit/run.mjs
node tests/integration/run.mjs
npm run test:e2e（Long Stroke 单一入口）
npm run check
环境允许时：npm run check:release
```

Long Stroke 不得因 G5 注册新工具而提升超时（G4R 时间边界）；js-* 相关回归走 unit + Long Stroke 受影响路径。
