# CUT-07 证据

返回 [卡片](../../cards/CUT-07.md)。

## 1. 两仓 SHA

| 项 | 值 |
|---|---|
| 库 identity 实现 | wanxiangli `b196749`，随 `54dcc34`（0.11.0）发布 |
| 库全套 | `npm run build && node --test --test-force-exit <scripts.test 列出的全部文件>`：504/504 pass（`npm test` 在测试全绿后子进程不退出挂起，为既有现象，故用 `--test-force-exit`） |
| 库 identity 套件 | 32/32 pass（001/002/005 更新，006 新增：宿主算法逐分支移植） |
| 主仓 base | `959f05643`（独立 worktree，完整构建 exit 0） |
| 主仓 candidate | 本卡两提交 rebase 到 `1c90ecf69` 之上，gitlink `c9a943d`（0.16.0，包含 0.11.0 identity） |

## 2. 套件（base `959f05643` vs candidate，同一 node 22，`node --test --test-concurrency=2`）

| 套件 | base | candidate |
|---|---|---|
| participant-identity | 52 / 51 pass / 1 todo | 同 |
| capability-enforcement | 149 / 133 pass / 12 skip / 4 todo | 同 |
| session-ontology | 28 / 26 pass / 2 todo | 同 |
| interaction-authority | 117 / 104 pass / 13 todo | 同 |
| action-affordance | 21 / 2 pass / 6 skip / 13 todo | 同 |
| office-capability | 23 / 14 pass / 9 todo | 同 |
| crash-reconciliation | 69 / 52 pass / 2 skip / 15 todo | 同 |
| provider-attempt-recovery | 75 / 61 pass / 14 todo | 同 |
| execution-model-routing | 120 / 112 pass / 8 todo | 同 |
| managed-chat-execution | 88 / 82 pass / 2 skip / 4 todo | 同 |
| delegation | 140 / 63 pass / **23 fail / 33 cancelled** | 同；`not ok` 列表 diff 为空 |
| managed-session-lifecycle | 175 / 92 pass / **1 fail / 69 cancelled** | 同；diff 为空 |
| work | 186 / 176 pass / **9 fail** | 同；diff 为空 |
| dispatch-protocol | 108 / 99 pass / **1 cancelled** | 同；diff 为空 |

粗体失败均为 base 既有，candidate 不新增也不消除。rebase 到最新 master 后在 candidate 上重跑前 10 个套件，计数与上表一致（见卡片 §11 末尾的复核行）。

## 3. `node scripts/check.mjs`

base 与 candidate 都 exit 1（既有问题）。去掉 worktree 路径与行号后，gate 结果集合相同（fsharp-control-pyramid 87、js-boundary-gate 67），**无新增发现**。report-only 的 shard-edge-audit 从 255 降到 253：`Catalog.fs` 中 `Orchestrator`、`Identity.fs` 中 `Value` 这两条跨 shard 类型名重复随旧 owner 一起消失。

## 4. 差分（[differential.mjs](differential.mjs)）

`node differential.mjs <base-root> <cand-root>` 加载两侧 `dist/Participant/Persona/Surface.js`、`dist/Foundation/RolesSurface.js`、`dist/Participant/Persona/OfficeCapabilitySurface.js`，在名称/角色/人格/目录版本/来源/owner 的笛卡尔积上双算纯决策（resolve、inherit、rehydrate、fromInput、legacy 判定、角色/人格解析、权限）。

```
compared 509617; differing 1
{"label":"office.permissions null","base":{"threw":"Cannot read properties of null (reading 'toLowerCase')"},"cand":[]}
```

唯一差异：旧实现对 `null` 角色名抛 TypeError，新实现返回空权限集，属于 fail-closed，不放宽任何授权。
