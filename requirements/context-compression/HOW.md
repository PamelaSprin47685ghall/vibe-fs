# context-compression — HOW

## 架构与核心机制

### 恢复槽与候选机制

1. **FallbackLedger 先裁决**：真实失败先由唯一写入口推进 cursor；只有 `RecoveryAdvanced` 才有资格派发下一物理请求，Host observer 不提前 arm。
2. **RecoveryOpportunity 分型**：`armedByFailure ∧ primed` 先归约为 `OrdinaryAttempt | RecoveryAttempt`。材料资格不是第二个布尔开关：X 的 `PrefixProbeSelection` 返回候选或精确 `NoCandidateReason`，Y 直接从 typed request + durable frames 构造 Squash。
3. **一次性消费**：RecoveryOpportunity 只属于紧随 advance 的一个 attempt。NoCoverage / stale candidate 发送普通主请求，不跨 attempt re-arm。
4. **分派与提交**：按 RequestKind 分派后继；Probe 成功原子提升 ActivePrefixEpoch，Squash 成功则压缩前半段 frames 并递增 FrameEpoch；只有 WorkMain/BloggerMain 的有效成功清零失败计数。

### Blogger 压缩与连续追平

1. **Delta 分块**：依据 200 KiB 上限按语义 part 边界切分，cutoff 仅在完整 turn 推进。
2. **唯一 canonical-X Main 重建**：`XTraceMaterialization.currentProjection` 先从 durable current-generation XTrace 物化 canonical X；`BloggerMainContext` 再作为 normal catch-up、AABB/crash refresh、Squash 后 Main 与失败 retry 的唯一 next-main 公式，统一 Opening floor、cursor、chunk 与 digest。request-local provider presentation 永不参与 coverage proof。
3. **失败本地 Squash**：BloggerMain 失败并 advance 到 primed 槽时，若 durable frames 可 squash，则 recovery workflow 当场关闭失败 request、materialize Squash、绑定新 PromptKey 并发送；不依赖未来主 X transform。
4. **typed park event**：caught-up 不启动 timer。`AwaitMaterial` 只返回 `MaterialAvailable BloggerRequestContext | Cancelled`；offer-first material 被下一次 await 直接消费，parked offer 直接完成当前 waiter。wake 自带 material，不再返回 bool 后访问第二份 pending 状态。
5. **durable recovery event**：WorkMain recovery 只读取 `snapshotWithRevision`。若 linked Blogger 有 durable open request 且 coverage 尚未严格前进，则通过 `AgentJournal.awaitChangeFromOrCancel` 订阅下一 committed fact；commit/abandon/coverage fact 到达后重算，plugin shutdown 则注销订阅并结束等待。没有 open producer 立即 retry。process-local flight/pending 与 wall clock 不参与 correctness。
6. **连续 catch-up**：每次 cycle 提交后直接从当前 canonical coverage 与 XTrace Current 重新派生下一块；暂时无材料时等待 typed material/cancel event，不设置冻结上限或超时。
7. **Cold-horizon auxiliary retirement**：观察到外部 compaction 时，`ContextReanchored` 推进 epoch 并清空旧 auxiliary visibility；成功 prefix probe 的 `PrefixRebaseCommitted` 在同一个 projection fold 内完成 PrefixEpoch 提升与同样的 visibility retirement。probe 尚未提交时，XWire 以 `PrefixPresentationHorizon.TentativeCold` 作为当前 transform 的 typed 返回值，使 composition root 跳过后置 historical auxiliary projectors，避免旧 horizon 先把 probe 请求重新灌胖。
8. **Opening floor**：Manager Life 只以真实 Opening 后的 `WorkRecordStart` 作为压缩下界；T1 commitment 仍可属于 WorkRecord 的 constitutive Opening，但不再获得 provider-context 的 raw 常驻权。
9. **Stable-identity X 穿透**：X-wire 的 cutoff 是 canonical XTrace semantic-turn boundary，不是本次 provider 数组下标。写回时由 XTrace provenance 解析被 coverage 证明覆盖的 Host message id，明确排除 raw Opening，并在这些覆盖消息中保留 `todowrite` call/result 原始回合；request-local synthetic/presentation row 不在 covered id set 中，因此不会移动 cutoff 或被误删。
10. **Blogger materialization admission**：同一 Blogger 的 materialize / PromptKey bind / abandon 先取得 process-wide、跨 plugin instance 的 keyed admission；取得后再读 durable projection 并执行 open-request 转换。normal start 持有 admission 直到 durable materialize、原子 flight claim 与 send/bind 完成，provider retry 的 stage/bind/abandon 也复用同一 admission。flight claim 只允许空槽建立或同 RequestId 刷新；不同 RequestId 返回 conflict，不覆盖 owner。该 admission/flight 都是物理资源，不参与 recovery correctness proof。

## 依赖关系

DEPENDS ON:
- `semantic-trace`
- `provider-projection`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CONTEXT-COMPRESSION-001 | `requirements/context-compression/tests/ctx-capacity-observation-forbidden.test.mjs` |
| CONTEXT-COMPRESSION-002 | `requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-003 | `requirements/context-compression/tests/blogger-delta.test.mjs` |
| CONTEXT-COMPRESSION-004 | `requirements/context-compression/tests/terminal-validity.test.mjs` |
| CONTEXT-COMPRESSION-005 | `requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-006 | `requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-007 | `requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-008 | `requirements/context-compression/tests/recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-009 | `requirements/context-compression/tests/probe-selection.test.mjs` |
| CONTEXT-COMPRESSION-010 | `requirements/context-compression/tests/probe-selection.test.mjs` |
| CONTEXT-COMPRESSION-011 | `requirements/context-compression/tests/blog-projection.test.mjs` |
| CONTEXT-COMPRESSION-012 | `requirements/context-compression/tests/blogger-delta.test.mjs` |
| CONTEXT-COMPRESSION-013 | `requirements/context-compression/tests/ctx014.test.mjs` |
| CONTEXT-COMPRESSION-014 | `requirements/context-compression/tests/blog-projection.test.mjs` |
| CONTEXT-COMPRESSION-015 | `requirements/context-compression/tests/blog-projection.test.mjs` |
| CONTEXT-COMPRESSION-016 | `requirements/context-compression/tests/probe-selection.test.mjs` |
| CONTEXT-COMPRESSION-017 | `requirements/context-compression/tests/ctx-opening-floor.test.mjs` |
| CONTEXT-COMPRESSION-018 | `requirements/context-compression/tests/blogger-delta.test.mjs`, `requirements/context-compression/tests/companion-ordinary-material-surface.test.mjs` |
| CONTEXT-COMPRESSION-019 | `requirements/context-compression/tests/injected-context-reanchor.test.mjs`, `requirements/host-boundary/tests/ordered-transform.test.mjs` |
| CONTEXT-COMPRESSION-020 | `requirements/context-compression/tests/ctx-opening-floor.test.mjs` + `requirements/provider-projection/tests/projection.test.mjs` |
| CONTEXT-COMPRESSION-021 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-022 | `requirements/context-compression/tests/companion-recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-023 | `requirements/context-compression/tests/parked-transform.test.mjs` + `requirements/context-compression/tests/companion-recovery-slot.test.mjs` |
| CONTEXT-COMPRESSION-024 | `requirements/context-compression/tests/parked-transform.test.mjs` + `requirements/context-compression/tests/companion-recovery-slot.test.mjs` |

## GAP

- CONTEXT-COMPRESSION-017/020：`ctx-opening-floor.test.mjs` 证明 pre/post-T1 floor 等价与 todo round retention 纯判定；`provider-projection/tests/projection.test.mjs` 证明真实 Y prefix write-back 越过 `todowrite` 时 call/result 仍以原始 X Host 消息存在。CLOSED。
- CONTEXT-COMPRESSION-021/022：failure-local Y recovery 与唯一 `BloggerMainContext` 已进入 production graph；旧 recovery waiter / future-X squash 入口已删除。CLOSED。
- CONTEXT-COMPRESSION-023：park 与 recovery wait 均只由 typed/durable event 推动；correctness path 无 timer/deadline/timeout。CLOSED。
- CONTEXT-COMPRESSION-024：同一 Blogger 的 materialize / bind / abandon 已由跨 plugin instance admission 串行；normal start 在 admission 内重检 `HasFlight`，retry 对 foreign flight fail-before-write，crash recovery 取得 admission 后重读 durable open；flight claim / release 均 RequestId-aware，拒绝跨 owner 覆盖或删除。RED→GREEN、静态门禁、Fable build、3455-test verification suite、integration 与 distribution package gates 全绿。CLOSED。
