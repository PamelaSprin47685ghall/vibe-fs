# CUT-10　模型路由与精确容量许可

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-10)

## 1. 当前状态

- **列**：`DONE`（eng-cut10g-goose，接手 eng-cut10f/e/d/c/b-goose、eng-cut10-goose）。
- **前置**：CUT-07 `DONE`。

## 2. 已合入的子范围

| # | 规则 | 新 owner | 主仓提交 | 证据 |
|---|---|---|---|---|
| ① | 调度器 ABI：协议版本、导出形状、target 解码、同步调用、理论容量查询 | wanxiangli/routing | f982f8070 | 提交说明 |
| ② | 精确执行许可：签发、Pending→Committed→Released、两种互斥释放、精确身份核对 | wanxiangli/permit 0.33.0 | 9b4b64dad | 差分 3×3000×40 次 0 处不同，3 个变异全抓住 |
| ③ | 容量不变式核对：七项检查与固定报告顺序 | wanxiangli/capacity 0.35.0 | 807a3db6a | 差分 3×20000 快照 0 处不同，3 个变异全抓住 |
| ④ | 执行准入等待队列：FIFO、32 条上限、同逻辑操作复用、新物理代替代、精确取出、撤回结果 | wanxiangli/admission-queue 0.37.0 | 57fbca343 | [diff-queue.txt](../evidence/CUT-10/diff-queue.txt)、[mutants](../evidence/CUT-10/diff-queue-mutants.txt) |
| ⑤ | 容量账本、执行托管与 provider-step 借用：真实 token 的获取/退休、lineage 内借用及单 provider 归属、托管迁移、provider-step 排队、复用、回收与授予次序 | wanxiangli/borrowing 0.43.0（wanxiangli e3c9f9c） | 1601da81c | [diff-borrowing.txt](../evidence/CUT-10/diff-borrowing.txt)、[mutants](../evidence/CUT-10/diff-borrowing-mutants.txt)、[touched tests](../evidence/CUT-10/borrowing-touched-tests.txt) |
| ⑥a | provider-run 精确观察：每个会话只保留最新 run 的 target 见证，一次性取走；run 到（会话，物理用户消息）的关系只来自显式记录；确认失败的原样重试绑定只给所属会话，且只消费一次；会话退休、物理终结时按精确范围清除 | wanxiangli/run-witness 0.47.0（wanxiangli 0be7455） | 本次提交 | [diff-run-witness.txt](../evidence/CUT-10/diff-run-witness.txt)、[mutants](../evidence/CUT-10/diff-run-witness-mutants.txt)、[touched tests](../evidence/CUT-10/run-witness-touched-tests.txt) |

⑤ 之后宿主 `ModelCapacity/Ledger.fs(.fsi)` 已删除；`Borrowing.fs` 只剩 F# 类型（credit id、target、option、fence 集合）到库 plain JS 边界的适配，以及把不变式快照映射回宿主记录。`execution-model-routing-010` 改为要求账本、token 状态和路由决策只出现在库里，宿主出现 `CapacityLedger`、`CapacityCreditState` 等旧标识即失败。

## 3. 验收（§9），针对 ⑤

- 构建：candidate `node scripts/build.mjs` 通过，724 个模块链接并加载。
- 差分：旧 master 宿主 BorrowingCapacity+CapacityLedger 对比新适配器+库，3 个种子 × 2000 次 × 60 步，共 360,000 步，0 处不同。每一步都比较返回值、抛出信息、promise 结算、scheduler running 快照和完整不变式快照，所有操作类型都覆盖到了（含 throw 分支）。
- 变异：4 个种子变异里 3 个被抓住，另 1 个是等价变异（唯一调用方传入的 distance 恒为 1）。M3（单 provider 归属的捷径）只在 300 次里抓住 1 次，检测偏弱，已如实记录。
- 套件：execution-model-routing 全部测试，加上提到 ModelCapacity/AdmissionQueue/borrowing 的需求测试，共 29 个文件：173 通过，0 失败（与 ④ 合入时计数相同）。
- 净改进：借用与账本规则原先在宿主手写约 600 行（Borrowing.fs 528 行 + Ledger.fs 40 行），现在只有库一个 owner，并有库内独立测试和隔离构建测试；宿主净减约 650 行。代价：多了一层 plain JS 适配（约 90 行）。
- 回退：revert 本卡主仓提交即可，库 0.43.0 留在 main 不影响旧 master。

## 4. ⑥a 验收（§9）

- 差分对象：被删除的宿主 F# 代码逐行转写成 JS 参照模型，与 wanxiangli/run-witness 对比。被删的是 ModelRouting.fs 里四张表、它们的 let 函数，以及 retireCurrentExecution / ReleasePhysicalExecution 里的清理块。
- 差分结果：3 个种子 × 3000 次 × 80 步，共 720,000 步，0 处不同。每一步比较返回值、全部 run 的 stepOf 探测和四张表的计数，每次结束再把剩余见证和重试绑定全部取尽比较。
- 局限：参照模型是人工转写，不是旧 dist 直跑。宿主一侧的接线由下列套件覆盖。
- 变异：7 个种子变异全部被抓住（172–992 处不同）。
- 套件：见 [run-witness-touched-tests.txt](../evidence/CUT-10/run-witness-touched-tests.txt)。`execution-model-routing-010` 新增断言：宿主必须 import `wanxiangli/run-witness`，且不得再出现那四张表。
- 净改进：宿主少了四张手写表和九个函数（ModelRouting.fs 净减约 20 行，适配多出约 25 行）；这些规则现在有独立的 oracle 测试、隔离构建和变异证据。

## 5. 关卡（DONE）与刻意留在宿主的部分

退出条件是“旧容量集合/Queue 的已迁策略不再独立维护”，现在已满足：调度器 ABI、精确许可、不变式核对、等待队列、容量账本与借用、provider-run 见证，各自只有 wanxiangli 一个 owner。用户 MJS 配置位置、协议版本和语义都没变（① 守住）。`ModelRoutingRuntime` 里还剩下的部分不再算本卡的刀：

- **DevOps 目标绑定**（`enforceDevopsBindingNormal`、`BindDevopsTarget`、`SeedBoundDevOpsModel`）：规则本身有争议。execution-model-routing-019 的 todo GAP-129 写明“可用性检查允许改动本应不可变的绑定，34-D1 待裁决”。现在抽出去只会把有争议的语义冻进库里，所以留在宿主，等 GAP-129 / 34-D1 裁决后再处理（交 CUT-38 记账）。
- **continuation input 保留、held physical release、superseded/stopped physical、lease 用途记录**：这些是一次 chat execution 的合法变化，属于 CUT-12（会话关联与执行事实）和 CUT-14（恢复与结算）的写集合，不是路由/容量规则。交给那两张卡。
- **准入 drain 次序、lock gate、poison**：这是 Host 把上面这些库 owner 串起来的接线（卡片“顺序”最后一步就是“Host before-provider 接线”），本身没有可独立验证的新规则。

## 6. 历史余项（已按 §5 处理）

- ⑥ `ModelRouting.fs` 的 `ModelRoutingRuntime`（约 1560 行）仍在宿主手写编排规则：devops 绑定不可变、continuation input 保留/重定向、superseded/stopped physical 记账、provider-run → target 记忆与恢复重试 target、准入 drain 次序。只有把这些拆成可独立验证的规则交给库，或者逐条论证它们属于 Host 接线，才算满足退出条件。MJS 策略的位置、协议版本和语义不变（①已守住）。
