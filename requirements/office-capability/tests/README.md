# office-capability 测试说明

行为规范见 [../WHAT.md](../WHAT.md)。本目录验证每个 Office 的后果定义如何投影成权限集合与工具面，以及职责边界判别器对样例的判别。

## 目录结构

- `NNN.test.mjs`：与 WHAT 条款同名的用例。文件里的 `test.todo(...)` 表示该行为尚无自动化断言，只标记缺口，不代表通过。
- `eval/provider-office-boundary/`：合成样例判别器，`corpus.mjs` 是样例与期望轨迹，`oracles.mjs` 是判定函数；只覆盖列明的样例，不是生产门禁，也不接真实 Provider 轨迹。

## 各用例覆盖范围

| 文件 | 看什么 |
|---|---|
| 003 | 角色 × 权限矩阵：编辑返回的权限投影不能反向修改权能；未知、组合与已废除角色名一律空集。 |
| 006 | 判别器对正反样例的判定，含 Manager 一边委派一边亲自修改、旧 coder 被当作 Engineer、DevOps 只运行不修复等反例。 |
| 007 | Manager 权能含评审取证与委派、不含改写执行与 Fission，公开可新建的 Office 只有 engineer；DevOps 模型绑定接受同一 target、拒绝漂移，拒绝后原绑定仍在。 |
| 012 | Orchestrator 投影给出编排、不给直接执行与 Fission；只接 Manager 目标的准入仍是缺口。 |
| 015 | 强度降级选模型的名字无公开权能投影，也不在 Manager 的 fork 候选里。 |
| 016 | Engineer 拥有本地源码工作与独占 Fission，没有真实执行权。 |
| 017 | DevOps 有源码修复与真实执行权限，没有委派与 Fission；现场修复流程仍是缺口。 |
| 018 | Sphinx 与历史上的 Inquiry 无公开权能；标准 Engineer 调用不获执行或 DevOps 调度权。 |
| 001/004/005/011 | 语义所有权、投影来源与全任期权能，需要人工审阅与真实运行追踪，目前为 TODO。 |

## 运行方式

先执行标准构建，再使用验证系统运行器：

```sh
TESTS_MJS_FILES="$(rg --files requirements/office-capability/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
