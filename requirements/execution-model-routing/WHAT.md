# execution-model-routing — WHAT

本文件是 `execution-model-routing` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。证据指针 → `PROOF.md`。

## EMR-001：managed model 唯一配置 authority = `~/.config/opencode/wanxiangshu.toml`

Wanxiangshu 的 managed/system model routing 只能从 `~/.config/opencode/wanxiangshu.toml` 读取。禁止从 `opencode.json`、环境变量、Host-final agent inventory 或内置 model default 补齐/覆盖。文件缺失、TOML 不可解析、schema 非法或必需 lane 缺失时，plugin load 必须 fail closed；不得回退旧配置。

该文件可在 Host Load Phase 读取并做纯结构校验；不得为读取模型配置反向调用 Host。

## EMR-002：七个 lane 是闭集；EffectiveAgent → lane 是固定产品映射

lane 闭集与成员如下：

| Lane | 成员 |
|---|---|
| `fastest` | Host `title`、Host `compaction`、`fast-distiller`、`fast-blogger` |
| `fastest_ii` | `deep-distiller`、`deep-blogger` |
| `faster` | `fast-inspector`、`fast-bookkeeper` |
| `medium` | `deep-inspector`、`deep-bookkeeper`、`fast-manager`、`fast-orchestrator`、`fast-coder`、`fast-devops`、`fast-inquiry`、`fast-reviewer` |
| `higher` | `deep-manager`、`deep-orchestrator`、`deep-coder`、`deep-devops`、`deep-inquiry`、`deep-reviewer` |
| `fast_browser` | `fast-browser` |
| `deep_browser` | `deep-browser` |

TOML 只能配置每个 lane 的模型资源，不能重写 agent→lane 映射。`fast-browser` 与 `deep-browser` 分别拥有独立第 6 / 第 7 lane，均独立于 medium/higher；fast side 目标是低成本、具图像理解能力的非推理模型，deep side 可配置更强视觉模型。具体 provider/model 由用户决定。

## EMR-003：每个 lane 是非空有序 ModelTarget 列表；每个物理模型有正整数容量

每个 lane 至少配置一个候选。每个候选至少包含完整 `providerID/modelID` 与 `max_sessions > 0`，可选 `variant`。候选顺序有语义：越靠前优先级越高。

物理容量身份只取 `(providerID, modelID)`；`variant` 不产生独立容量池。同一物理模型允许出现在不同 lane，因此 fast/deep 最终使用同一 model 是合法世界；但该物理模型在所有出现位置声明的 `max_sessions` 必须一致，否则配置非法。单个 lane 内不得重复同一物理模型。

## EMR-004：新 lease 永远选择本 lane 中最早未满候选；全满则等待

当 `(SessionId, EffectiveAgent)` 第一次需要 managed model lease 时，allocator 按 lane 配置顺序扫描，选择第一个当前占用 `< max_sessions` 的物理模型。

若 lane 内所有候选均满：

- 不跨 lane spill；
- 不超过任何 `max_sessions`；
- 不把资源竞争改写成 provider/business failure；
- 当前请求阻塞，直到 lane 中出现余量，然后从第一个候选重新按顺序扫描。

等待按 lane 内 FIFO 排队；容量释放后从最早仍有效 waiter 开始唤醒，每个被唤醒请求重新按候选顺序扫描。等待必须可由当前请求取消、session abort/retire 或 plugin shutdown 打断；取消后的 waiter 立即从队列移除，不得继续占用或抢占容量。

## EMR-005：容量按 live SessionId 聚合，并在同一 Host 进程跨角色、跨 lane、跨插件实例共享

一个物理模型的占用量 = 当前进程中持有该模型 lease 的不同 live `SessionId` 数量，而不是 agent 数、lane 数或 plugin instance 数。

因此：

- 不同角色映射到同一物理模型时共同消耗同一 `max_sessions`；
- 同一 SessionId 即使因不同 EffectiveAgent 持有多个指向同一物理模型的 lease，也只计一个 session 占用；
- root workspace 与 worktree 会产生不同 plugin instance，但同一 Host 进程内必须看到同一容量 registry；
- 不要求跨不同 OS/OpenCode 进程共享本地计数。

## EMR-006：模型 lease 绑定 `(SessionId, EffectiveAgent)`；AABB/peer 只换 EffectiveAgent

成功分配后，`(SessionId, EffectiveAgent) → ModelTarget` 在当前 OpenCode process epoch 内、该 session live 生命周期中稳定。普通 continuation/prompt 不得因为候选顺序或瞬时容量变化自动换模型；process restart 会开启新的本地容量 epoch，并重新按当前 TOML 建立 lease。

AABB 保持原代数：A/A 使用当前 Selected/fast-or-deep agent，B/B 使用其 peer；切到 peer 后只是在同一 SessionId 下首次取得/复用另一个 EffectiveAgent 的 lease。A 与 B 的 ModelTarget 可以相同，也可以不同；不得以 model 是否相同判断 peer 是否成立。

显式 Strength/assistance/fallback 改档仍通过既有 EffectiveAgent authority；model allocator 不自行发起 tier/peer 切换。

## EMR-007：session retire/delete 是容量释放边界；释放幂等且唤醒等待者

managed/user-facing session 被明确 retire/delete 时，allocator 必须释放该 SessionId 持有的全部 model lease；同一 SessionId 对同一物理模型只释放一次占用。重复 cleanup 幂等。

释放容量后，等待该资源所属 lane 的请求可以重新竞争；不得把已 retire session 的占用留在 process-shared registry。仅切换 A/B、单次 provider failure、普通 idle/completion 不释放 live session 的稳定 lease。

## EMR-008：`opencode.json` 的 managed agent model 不再具有 authority；不校验 fast/deep model 互异

Wanxiangshu 可以为 Host managed-agent config 投影必要的 mode/permission/prompt/静态 guardrail 字段，但实际 provider request 的 managed model 必须来自本包 lease。`opencode.json` 中已有 managed agent `model` 值不得被读取为 routing truth；冲突值不得覆盖 TOML 选择。

启动配置不再执行 `fast-X.model <> deep-X.model` 校验，也不因 pair 最终解析为同一物理模型而失败。peer existence/对称性仍由 `participant-identity` 保证。

## EMR-009：user-facing model 字段不是 managed model authority

真实外部用户请求仍可决定 managed EffectiveAgent/档位；但其 Host message/request 中携带的 `model` 不得成为 Wanxiangshu managed binding authority。进入 provider 前，managed request 必须使用 `(SessionId, EffectiveAgent)` 的 TOML lease；Host 观察到的实际 provider model 必须与该 lease 一致，否则 fail closed。

非 managed Host 会话不受本包接管。

## EMR-010：Host title/compaction 走 `fastest` lane 的首选模型，但不占 managed session 容量

Host `title` 与 `compaction` 不是 Wanxiangshu managed SessionId，因此不参与 `max_sessions` 的 live-session 计数。它们使用 `fastest` lane 配置的第一个 ModelTarget；不因 managed session 容量满而切换/等待其它候选。

`fast-distiller` / `fast-blogger` 虽与它们同 lane，但属于真实 managed session，完整遵守 EMR-004..007。
