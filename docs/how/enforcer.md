# Enforcer — 目标实现

## ENFORCER-042：多调用防御性归并

同 run 多个 `blog` 按 PartOrdinal 防御性归并；正常协议仍是恰好一次。  
tip 选择规则见 ENFORCER-025。

## ENFORCER-046：Blogger Cycle 结果派生

Cycle 结果从归并后的 canonical call 派生。

## ENFORCER-047：Cycle 后 continuation 与单一状态机

成功进下一材料或 idle；失败进 nudge/Fallback，不分裂第二状态机。

## ENFORCER-051：物理 Prompt 与 provider view 重建

物理 prompt 与 provider-visible 历史重建分离：重建只经 durable frames + typed context（COMPANION-005）。

## ENFORCER-065：进入 InteractionNudge 的条件

进入 InteractionNudge 的条件固定表驱动。

## ENFORCER-066：InteractionNudge 是真正的 InteractionRepair

nudge 即真正 InteractionRepair（Continuation），不新建 Authority。

## ENFORCER-067：何时算 nudge 彻底失败

彻底失败判据固定；失败后接 Fallback 或终局，禁止无限 nudge。

## ENFORCER-068：状态转移

状态转移表固定，禁止按错误散文分叉。

## ENFORCER-070：RecentTips 投影

RecentTips 投影覆盖 normal / squash / restart / recovery / compaction 后路径。

## ENFORCER-071：work record 呈现 previous_enforcer_tip

work record 以低信任 `previous_enforcer_tip` 块呈现；不得伪装 parent instruction。

## ENFORCER-140：X 侧 Host Compaction

X 侧重锚与 HOST-006 对齐，不在 Enforcer 另起 epoch 算术。

## ENFORCER-141：Prefix Probe Promote

probe promote 与 CTX/HOST 提交语义一致。

## ENFORCER-142：Y 侧 Compaction

Y 侧与 squash/coverage 合同一致。

## ENFORCER-143：Compaction Transform 白名单

transform 白名单：不得借 compaction 路径注入未授权 synthetic。

## ENFORCER-150：新增持久事实

新增事实种类服从 PERSIST fold；不得旁路 Journal。

## ENFORCER-152：CommitUnknown

CommitUnknown → fail-closed reconcile（PERSIST-003）。

## ENFORCER-153：恢复来源

恢复只从 Journal + Host snapshot，不从物理 Y transcript 猜历史。

## ENFORCER-154：Cycle 恢复

Cycle 恢复：能证明 response 属于 request 才提交；否则不提交。

## ENFORCER-156：Clean Break

schema clean break：不兼容旧评分模型，但保留 schema version 字段纪律。
