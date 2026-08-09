# DSL 结构化程序规则 — 证明

行为见 `what/dsl-structured-program.md`；边界见 `shape/dsl-structured-program.md`；
算法见 `how/dsl-structured-program.md`。

## 静态义务

| 门 | 必须判红的反例 |
|---|---|
| `scripts/checks/dsl-ownership.mjs --threshold=0` | 业务 Interpreter/Command-Reply 第二运行时、程序计数字段、未声明 mutable、record `ref` 可变存储、跨文件同构 DU、未分类的大 DU、未登记 Infrastructure leak、record 中≥2 个独立状态轴且无 `DSL-state-combination` 分类、无结构化理由的 `ControlState` |
| `scripts/checks/architecture.mjs` | Domain 向上层依赖、源码根/fsproj 不一致、资源越界读取 |
| `scripts/checks/spec.mjs` | DSL Clause 重复、悬空或 Change 影子定义 |

每项新增静态规则必须有永久 fixture，并曾用故意反例证明仓库入口会失败。

## 正交组合证明（引用 DSL-005，人工）

> 本节约 DSL-005（定义见 `what/dsl-structured-program.md`）的人工证明。
> 自动化下限现已含结构化 `state-product` 门禁：`scripts/checks/dsl-ownership.mjs` 解析
> record 的字段类型结构（本地 DU/`option`/`bool`），识别 ≥2 个独立状态轴并要求显式
> `/// DSL-state-combination: domain|physical` 分类；判定与字段名无关。`ControlState`
> 分类要求机器可校验的 `/// DSL-control-state-reason:` 理由。下表仍是架构级语义枚举，
> 门禁只守卫「未分类即红」，不替代 DSL-002/DSL-005 的人工语义判断。

### 正交轴与物理归属（当前生产）

| 轴 | 物理归属 / 类型 | 说明 |
|---|---|---|
| busy / current request | `IParkedTransformHost` flight registry（`HasFlight` / `bloggerFlights`） | 唯一 writer 与读取来源；不再用 `BloggerRuntimeState` DU |
| parked waiter | physical parked registry / `HasParked` | 与 flight 分离 |
| pending offer | pending-offer 物理槽（与 current request 分离） | 见收敛测试 C0 断言 |
| drain | `DrainWindow`（`Closed \| Open of DrainPermit`） | 单轴；permit 不可伪造 |
| tool recovery | `BloggerToolRecovery`（由 durable evidence 派生） | 非长期 cell 程序计数 |
| material 路由 | 纯函数 `BloggerRuntime.decideMaterial` | 由物理事实 + 请求上下文派生，不持久化流程位置 |

### 可表示组合与业务意义

当前 Blogger 运行时**不**将 State + Pending/Offer + Recovery/Repair + Drain 编码进同一长期 record/DU。
可观察“组合”由**独立物理槽位的存在性**构成，而非组合状态机 case：

1. 无 flight / 无 parked / drain Closed：可接受新 material（空闲路径）。
2. 有 flight：busy；新 material 由 `decideMaterial`/`blocksNewRequest` 跳过或排队策略处理，不另写 Idle|InFlight 镜像。
3. 有 parked（无或有关联 offer 槽）：parked 等待；与 flight 正交，不合并为单一程序计数 DU。
4. drain Open：仅 reactivation 路径可 mint；与 busy 由物理槽位分别表示，不合成 `InFlightAndDraining` 一类 case。
5. recovery 需要：由 journal/durable evidence 派生 `BloggerToolRecovery`，不写入 runtime cell 位置字段。

因此：DSL-005 要求的“组合总数”在当前架构下为**槽位笛卡尔积的可观测子集**，每种可达组合均对应上表真实物理语义；不可达组合（例如“用 cell.State 表示下一步”）已通过删除 `BloggerRuntimeState`/`BloggerRuntimeCell` 与 C0 永久测试禁止。

### 自动化下限

以下永久门禁防止**重新引入**程序计数字段与影子状态，并守卫「未分类即红」的组合与理由义务：

- `scripts/checks/dsl-ownership.mjs --threshold=0`（program-counter / large-DU / ControlState 理由 / `state-product` 组合轴等结构门）
- `scripts/checks/dsl-ownership-ratchet.mjs`（基线防回归）
- `tests/unit/enforcer/blogger-convergence-gaps.test.mjs`（`HasFlight` 唯一 busy、无 shadow state API）
- `tests/unit/verify/dsl-ownership.test.mjs`（含 `state-axes-{illegal,domain,physical}.fs` 与 `ControlState` reason fixtures）与 `dsl-ownership-ratchet.test.mjs`

`state-product` 门禁在字段名无关的结构层面识别 record 状态轴乘积；它不替代上表人工枚举
的架构级语义，只把「未分类组合」变成构建期失败。

## 动态义务

- 进程等待分别覆盖自然退出、deadline、kill acknowledgement 超时和等待中取消。
- Companion 恢复机会覆盖注册、单次消费、无机会 no-op 与重启不恢复 waiter。
- Blogger single-flight 覆盖 busy、parked、完成、取消与恢复，不从流程位置字段推断事实。
- Journal recovery 覆盖 evidence 不足时 fail closed，并证明重入公共 workflow。
- family fold 与迁移前 wire/Journal 兼容性按对应领域 proof 证明。

测试必须走公共契约面并断言可观察结果或端口调用；不得只断言内部 tag。

## 完成判据

1. Active Change 所列完成条件全部满足，并在同一文件追加 Final outcome 后移入 Completed。
2. 静态门禁无阈值上调或永久豁免逃逸。
3. 相关 unit、integration 与 canary 按 `proof/verify.md` 通过。
4. 删除旧状态后不存在双写、adapter facade 或仅为旧测试保留的旁路。
