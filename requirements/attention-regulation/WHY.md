# attention-regulation — 为什么必须独立存在

## 1. 一个不可替代的存在理由

LLM 很容易把已经足够的判断重新打开、把自己已经决定不做的方向继续背在身上、或因为害怕忘记一个非阻塞旁支而当场扩张 scope。三种表象不同，根因相同：模型缺少几个明确、局部、可主动选择的退出 speech act。

本包只解决这个问题：给 participant 三个极小动作，分别结束继续推理、解除自我承诺、延后非阻塞工作。

```text
enough(x)   当前信息已经足够支撑下一步；停止重开同一判断
abandon(x)  我允许自己不再继续背负这条自我承诺/方向
defer(x)    这件新工作真实存在，但现在不做，也不把它伪装成当前 obligation
```

`enough` / `abandon` 与既有 `assume` 同型：Host 不替模型判断真假，不授予 authority，不维护心理状态机；价值来自 tool-call boundary + 局部 return 把一次认知转折钉住。`defer` 只有一个额外事实：既然它承诺“稍后再提醒”，就必须拥有一个不依赖 working memory 的小队列，并在 `celebrate` 的尾部重新露出。

## 2. 为什么不并入其它包

- 不并入 `cognitive-environment`：后者拥有长期 self-model / craft payload；本包拥有运行中一次性的 attention lifecycle act，可独立改掉全部实现而不动 Role Law。
- 不并入 `obligation-ledger`：deferred work 不是 mission debt；把它写成 obligation 会把 scope-creep 从“现在做”变成“以后欠”。
- 不并入 `epistemic-reasoning`：`enough` 不替 controller 计算最优停止；它是 participant 主动声明“decision-relevant evidence burden 已满足”的 speech act。
- 不并入 `institutional-learning`：后者消费一次经历；本包只管理眼前 attention 的继续/解除/延后。

## 3. FAILURE MEANING

RED = 已经没有 decision-changing 新信息仍不断重开同一判断；已明确放弃的自我承诺仍因前文心理重量自动复活；非阻塞旁支因为“怕忘记”而立即进入当前工作或被伪造成 mission obligation；defer 成功后却在 celebrate 时丢失、重复激活或自动变成 owed work。

## 4. 被拒方案

- generic `epistemic_state_manager`：schema tax + 状态同步 + 新错误面，违背微工具杠杆点。
- `abandon` 真实取消 mission obligation：把心理支点偷换成 authority mutation；明确拒绝。
- `defer` 直接调用 `todowrite`：把“以后可能看”变成“当前正式欠”。
- 自动在 background 执行 deferred work：`defer` 的含义恰好是现在不做。
- 给 `enough` 计算置信度阈值：工具不替模型推理，只钉住模型已做出的停止判断。

## DEPENDS ON

- `participant-identity`：deferred queue 必须属于精确 participant life，不能跨人串线。
- `durable-events`：已接受的 deferred item 与其一次性 resurfacing 不能依赖易失 working memory。
