# attention-regulation — WHAT（唯一 normative 合同）

命题前缀 `ATTENTION-REGULATION-`。每条都是当前世界必须同时成立的事实；证据落点见 `HOW.md`。

## ATTENTION-REGULATION-001：`enough` 是 decision investigation 的主动吸收态

`enough(decision)` 接受一段非空自然语言，表示 participant 已判断：当前已取得的信息足够支撑这个 decision 的下一步行动。成功 return 必须明确：停止继续为“更多证据本身”而搜寻或重开同一判断；只有能指出一个新的、尚未消费且足以改变 action 的事实时才有资格重新调查。

`enough` 不证明 decision 正确、不产生事实、不扩大 authority、不替代后续执行/验证，也不建立持久 epistemic state。没有新信息时重复调用同一个未变化 decision 不增加任何语义。

## ATTENTION-REGULATION-002：`abandon` 是无约束 decommit ceremony，不是真实义务取消

`abandon(commitment)` 接受一段非空自然语言，表示 participant 主动允许自己不再继续背负某条自我生成的计划、方向、承诺或心理债。成功 return 必须明确：这条内容不再因为“之前想过/说过/计划过”而自动获得未来注意力。

Host 不要求 reason/evidence/approval，不验证其“是否值得放弃”，也不持久化心理状态。`abandon` 不得取消 `obligation-ledger` 中真实 obligation、撤销用户 authority、删除 repository work、终止 child/session 或改变任何外部事实；若存在真实 obligation，必须经其 owner 的正式动作处理。

## ATTENTION-REGULATION-003：`defer` = not now ≠ never ≠ owed now

`defer(new_work)` 接受一段非空自然语言，把当前 participant 新发现、真实但非 blocking 的工作记录为 DeferredWork。成功意味着它已经离开 working memory 的“必须一直惦记”状态；participant 应立即回到当前主线。

DeferredWork 不是 active obligation、todo、promise、background job 或 authority。`defer` 不执行它、不自动委派它、不改变当前 work frontier；调用方不得因为 DeferredWork 存在就宣称当前 mission 仍欠它。

## ATTENTION-REGULATION-004：DeferredWork 按 participant life 隔离并可重放

每个被接受的 DeferredWork 必须有内部稳定 occurrence identity，归属精确 participant life；restart/replay 后不得丢失、跨 participant 泄漏或因同一 tool occurrence 重放而产生第二条 deferred item。用户可见 schema 保持一个自然语言参数；内部 identity 不泄漏到 provider 形成状态管理负担。

若 participant life 在 celebrate resurfacing 前终止，尚未 resurfaced 的 DeferredWork 随该 life 退休：不转移给 replacement/child/同 persona 的另一个 execution，不阻塞 finality，也不升级为 durable mission debt。`defer` 本来就只承诺“这段 life 稍后提醒我”，不是“世界永远欠着这件事”。

## ATTENTION-REGULATION-005：只有 `celebrate` 尾部统一 resurfacing，且 resurfacing 不自动激活

`institutional-learning` 的一次成功 `celebrate` 在完成本次经验处理后，必须把该 participant 当前尚未 resurface 的 DeferredWork 放在 tool result 最后统一返回；同一批 item 随该 celebration occurrence 标记为已 resurfaced，replay 同一 celebration 只重放同一结果，不再次 drain 新批次。

resurfaced item 仍不是 obligation。模型可以现在处理、再次 `defer`、显式写入正式 obligation，或 `abandon`；系统不得自动选任何一种。

## ATTENTION-REGULATION-006：三个动作不形成 planner / workflow engine

本包不得拥有 stage、priority、deadline、dependency graph、auto-resume、background executor、confidence score 或 generic cognitive state machine。允许的持久状态只有 DeferredWork 的最小 append/projection 与 celebrate resurfacing receipt；`enough` / `abandon` 保持近乎纯 return reinforcement。

## 边界

- Pair Hint 如何提示这些动作 → `cognitive-environment`。
- tool description 的五问合同 → `action-affordance`。
- tool schema/runtime 可见性与 office gate → `capability-enforcement`。
- `celebrate` 的学习/Enhancer 语义 → `institutional-learning`。
- mission 当前真正欠什么 → `obligation-ledger`。
