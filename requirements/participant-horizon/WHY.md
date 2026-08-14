# WHY —— 不可替代的存在理由

> `The machine may know everything required to keep the world coherent.
> A person should be told only what belongs in their horizon.`（ARCH-014）

## 为什么必须独立存在

机器侧为了把世界保持一致，持有远超 participant 需要的状态：session 身份、job 身份、lane 索引、
worktree 路径、fallback offset、工具执行状态、隐藏的 review 编排、内部参与者（Blogger/Distiller/
Bookkeeper）。这些**不是** participant 体验世界的方式。

如果全部暴露：

- participant 被迫解码 Host DTO 和拓扑（`status/code/TIMED_OUT`、`session_id`、`pty_id`），把注意力
  花在机器内部如何组织上，而不是花在「下一步合法行动是什么」上；
- 隐藏编排（reviewer 身份/barrier/witness/2N）一旦可见，Manager 会把它当作可调度的资源，
  终结协议（Finality）就从不可抵赖的机制退化成可操纵的对话；
- 模型会从词汇（`fast-coder`、工具名、「看起来能干」）推断 authority——那是 `office-capability`
  明令禁止的推断路径（PROMPT-021）。

**唯一不可替代的 WHY**：信息准入是一道独立于「如何表示已准入信息」的决策边界。它决定**什么**穿过，
不决定穿过之后长什么样。`provider-projection` 可以整体重写而不碰本包；反过来，允许一种新的 runtime
measurement 进入 horizon（例如新的终态码）也不应逼 renderer 改版。

## RED 长什么样（失败模式）

| 症状 | 历史出处 |
|---|---|
| provider 输出出现 `SessionId / AgentId / ManagerJobId / PtyId / FissionGroupId`、`lane_index`、worktree、fallback offset、`fast-\|deep-` 自称、spool path | EXEC-030；GrandRewrite 前真实发生——机器 DTO 借 join/horizon 回传 |
| join 超时暴露 `TIMED_OUT` / `status="failed"` / `code=...` | AGENT-013 / EXEC-004：10s 预算必须转自然语言后果 |
| Manager 看见 reviewer/witness/barrier/2N | GLORY-002/030：只有窄例外（outcome + concrete report）可穿过 |
| 工具 result 重述工具 success 已证明的事实（echo 当 observation） | ARCH-014：`An echo is not an observation.` |
| 一个已经不存在的东西还显示路径 | ARCH-014：`Never show a path to something that no longer exists.` |

## 为什么不是「安全/隐私」包

本包不是安全包：它不裁决 authority（`interaction-authority`）、不裁决后果资格（`office-capability`）、
不做 capability 同构（`capability-enforcement`）。它只回答一件事：**给定机器已知的一切，什么值得成为
participant experience。** 安全相关的不变量各自归它们的 semantic owner；本包提供这些 owner 共同消费的
准入保证。

## 被拒方案（考古）

- **DTO 黑名单即永久规范。** 拒绝：token 名单会随每次新泄漏无限累加，且无法回答「新信息该不该进」。
  黑名单只作迁移期 ratchet（HOW 历史与弃权），规范本体是 positive decision filter（ARCH-014）。
- **「一切皆可看，模型自己过滤」。** 拒绝：模型没有能力区分「机器内部」与「行动相关事实」，等于
  把 horizon 交给 prompt 自觉。
- **把 horizon 塞进 projection 包。** 拒绝：projection 是 renderer，可以重写而不改变准入决策；
  独立 change test 成立（HANDOFF §7.2）。
