# CUT-08 证据

返回 [卡片](../../cards/CUT-08.md)。

## 1. 两仓 SHA

| 项 | 值 |
|---|---|
| 库 capability 实现 | wanxiangli `735de19`，随 `71958aa`（0.18.0）发布 |
| 库全套 | `npm run build && node --test --test-force-exit <scripts.test 全部文件>`：582/582 pass |
| 主仓 base | master `587cd7b3c` 源码（gitlink `c9a943d`，0.16.0），完整构建 exit 0 |
| 主仓 candidate | base + 本卡接线 + gitlink `71958aa`，`node scripts/build.mjs` exit 0 |

## 2. 套件（node 22，`node --test --test-concurrency=2`）

| 套件 | base | candidate |
|---|---|---|
| capability-enforcement | 149 / 133 pass / 12 skip / 4 todo | 同 |
| office-capability | 23 / 14 pass / 9 todo | 同 |
| action-affordance | 21 / 2 pass / 6 skip / 13 todo | 同 |
| participant-identity | 52 / 51 pass / 1 todo | 同 |
| session-ontology | 28 / 26 pass / 2 todo | 同 |
| interaction-authority | 117 / 104 pass / 13 todo | 同 |
| managed-chat-execution | 88 / 82 pass / 2 skip / 4 todo | 同 |

全部 0 fail / 0 cancelled。

## 3. `node scripts/check.mjs`

base 与 candidate 都 exit 1（既有问题）。去掉 worktree 路径和行号后，发现集合相同，**无新增**。report-only 的 shard-edge-audit 从 253 降到 252。

## 4. 差分（[differential.mjs](differential.mjs)）

`node differential.mjs <base-root> <cand-root>` 加载两侧 `dist/Foundation/OfficeCapability.js`、`dist/Foundation/Roles.js`、`dist/Participant/Persona/OfficeCapabilitySurface.js`，双算以下内容：11 个角色 × 24 项能力的 `permissions` / `isAllowed`；surface 层在异常角色和异常能力标签上的结果；Manager 事实 2⁴ × 3 种清理摘要（含空串）下的门禁集合与逐能力判定。

```
compared 3950; differing 0
```
