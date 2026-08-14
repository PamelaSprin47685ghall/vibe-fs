# participant-horizon

> 一句话 WHY：**machine knowledge 远多于 participant experience；只有会改变合法行动的
> 最小事实穿过 horizon。**

```text
什么信息有资格进入 participant experience？
```

本包回答「什么有资格被看见」：一条信息能不能穿过机器侧（Host/Journal 墙内）到达 participant 面前。
答案由 positive admission law 决定——不是不断累加的历史黑名单。

```text
已经决定可见后，怎样确定性表示？
```

那是 [`provider-projection`](../provider-projection/README.md) 的问题（typed intent → 唯一确定性投影）。
本包与 projection 按 HANDOFF §7.2 硬拆：**horizon ≠ projection**。Horizon filter 当前落在 projection
路径里只是 implementation fact。

## 阅读顺序

1. [`WHY.md`](WHY.md) —— 为什么这个包必须独立存在、RED 长什么样、历史上发生过什么。
2. [`WHAT.md`](WHAT.md) —— 唯一 normative 合同：编号命题 `PARTICIPANT-HORIZON-0NN`。
3. [`HOW.md`](HOW.md) —— 实现模型：`src/` 里哪个工具/类型承载哪条命题；历史与弃权。
4. [`PROOF.md`](PROOF.md) —— 每条命题 → 测试落点；REUSE 文件的断言级 SPLIT 计划。
5. `tests/` —— 本包拥有的可执行 proof（`node --test requirements/participant-horizon/tests/<file>`）。

## 概览

| 层 | 内容 |
|---|---|
| WHY | 全暴露迫使 participant 解码 Host DTO/拓扑，而不是依据后果行动 |
| WHAT | `PARTICIPANT-HORIZON-001..014`：admission filter、机器拓扑禁令、DTO 禁令、hidden surface、后果优先 |
| HOW | `Infrastructure/OpenCode/Tools/{HorizonTool,JoinTool,ForkTool}.fs`、`scripts/checks/provider-leak-gate.mjs`（Gate B） |
| PROOF | 19 个包内断言（2 个 MOVE 文件 + 1 个 NEW）+ 4 处 REUSE（SPLIT@cutover） |
| 依赖 | 无（可独立定义；`provider-projection`、`guidance-delivery`、`delegation` 等消费其 guarantee） |

## RED 长什么样

- participant 看见无行动价值的机器状态（SessionId、status、worktree、fallback offset…）；
- 虚假 affordance / 不可达路径 / 无行动价值的内部身份穿过 horizon；
- 真正影响下一合法行动的事实被裁掉（该看的不给看，该给 consequence 的给 DTO）。

## 不归我（DOES NOT OWN）

- office authority、participant identity、guidance/Role Law 内容 → `office-capability` / `participant-identity` / `cognitive-environment`
- 语言/localization、TOML/JSON/wire layout、ProjectionIntent order → `provider-language` / `provider-projection`
- 当前 `SessionId/status/code/error/...` blacklist 作为永久 taxonomy（它们是 proof fixtures，见 HOW 历史与弃权）
- 已决定可见后的确定性渲染 → `provider-projection`
