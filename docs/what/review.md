# Review — 可观察行为

条款前缀：`REVIEW-`。  
Witness / Seal 所有权见 `shape/review.md`。  
Seal 绑定与因果链算法见 `how/review.md`。

## REVIEW-001：Verdict 工具

```json
{ "verdict": "PERFECT | REVISE" }
```

工具不接受描述字段。描述由 Reviewer formal report 承担。

## REVIEW-002：REVISE

任一 durable REVISE 立即关闭当前 request 的 Reviewer continuation capability 与 cohort：无 confirmation、不等待尚未 durable 的 sibling 新 terminal / 新 effect；未完成的 PERFECT 确认链同时作废，关闭后不得补发 challenge。`FinalityRejected` 必须另行满足 GLORY-072 的 record-ready，不能在 verdict 时抢先落盘（GLORY-044/055/072）。

已 durable 的 sibling REVISE 不参与「等待新 terminal」：成功路径下先预置 rejecting primary 的 record-ready/`WriteBlob`，再入账 sibling 并物化为 Manager 的 steer continuation（instruction-only `# ` Synthetic TOML，GLORY-044 双轨交付），不得丢弃、不得并入 `FinalityRejected` 工具结果。Primary 硬物化失败 → `FinalityUndecided` 且零 `FinalitySiblingSteered`。任一 durable sibling 的 LWR 无法物化 → fail-closed `FinalityUndecided`，同样不得静默丢弃。

## REVIEW-003：PERFECT 需要因果证明

第一次 PERFECT 产生 challenge 证据（`PerfectChallengeIssued`），tool result 使用固定 skeptical 英文句子（`ChallengeTextVersion = 1`）。

第二次 PERFECT 成立必须同时满足：

1. 同一 Reviewer Session  
2. 同一 ReviewBarrier  
3. 同一 Git tree  
4. 不同 ProviderRunIdentity  
5. 不同 ToolCallId  
6. 第二次 provider input seal **包含**第一次 challenge result  
7. 中间没有 REVISE  
8. 中间没有 tree 变化  
9. verdict 工具确实成功执行  

禁止：仅凭 AuthorityRoot 或 PhysicalMessageId 确认。

ReviewConfirmation prompt 只让 Host 启动下一次 provider request，**不是**确认事实本身。

双 PERFECT 屏障完全由 Host 执行，Reviewer 提示词不灌输该流程（REVIEW-012）：Reviewer 只提交基于当前 tree 的独立 verdict，确认与计数由 Host 侧 witness / seal 完成。

## REVIEW-008：Git tree 变化使 witness 无效

任意 Git tree 变化：

- pending challenge → 拒绝  
- confirmed witness → 仍可审计，但不再满足 Guard  

不删除历史 witness。`witness.IsValid(currentBarrier, currentTree)` 是派生谓词。  
Post-rebase 必须全新双 PERFECT（即使 tree hash 碰巧相同）。

## REVIEW-009：Orchestrator 复审

Rebase 后旧 witness 无效，必须重新获得双 PERFECT，再允许 ff publish。

## REVIEW-011：8 大代码质量支柱与评估报告

Reviewer 在给出 `verdict` 前，必须在其 formal text report 中根据 8 大代码质量支柱进行评估：

1. **Language & Algorithmic Mastery**（语言与算法）
2. **Radical Simplicity**（极致简洁）
3. **Structural Elegance**（结构优雅）
4. **Bounded Granularity**（有界粒度）
5. **Imperative Test Coverage**（必要测试覆盖）
6. **Flawless Logic & Best Practices**（无瑕逻辑与最佳实践）
7. **Caller Ergonomics**（调用方与用户体验）
8. **Uncompromised Completeness**（完整性）

发现任何质量维度不达标或缺陷时必须提出 `verdict("REVISE")` ；仅当 8 维全部无瑕且需求完全满足时方可调用 `verdict("PERFECT")`。
