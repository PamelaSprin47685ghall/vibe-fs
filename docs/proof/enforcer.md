# Enforcer / Blogger — 证明

行为见 `what/enforcer.md`，所有权见 `shape/enforcer.md`，程序见 `how/enforcer.md`。  
规则实例实现面：`resources/enforcer/catalog.json`。

## 资源与启动

| 检查 | 期望 |
|------|------|
| catalog 缺失 / JSON 非法 / Domain 校验失败 | 插件启动 fail fast，无代码内 fallback catalog |
| `package.json` files 含 `resources/` | pack 后仍可加载 |
| `id` / `field` | 发布后稳定；ordinal 连续 `1..N` |

integration：resources / package 套件覆盖加载失败路径。

## 领域与 codec

| 性质 | 落点（代表） |
|------|----------------|
| tip schema / 缺 tip / 未知 tip | `tests/unit/enforcer/codec.test.mjs` |
| cycle 归并 / nudge | `tests/unit/enforcer/cycle-nudge.test.mjs` |
| catalog 形状与字段 | `tests/unit/enforcer/catalog.test.mjs` |
| 收敛缺口回归 | `tests/unit/enforcer/blogger-convergence-gaps.test.mjs` |

## 端到端

Blogger 路径 canary：工具必须调用、busy skip 不推进 coverage、成功提交推进 RecordCoverage。  
与 CTX/HOST 交叉：compaction 后 reanchor 不把 Host 摘要当 BlogFrame。

## 发布面

规则正文变更 = 数据变更 + 对应测试；不得只改文档不改 catalog。  
`ScoreVector` 路径不得复活（ENFORCER-072/073）。
