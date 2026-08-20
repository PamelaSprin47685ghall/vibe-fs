# context-compression — HOW

## 架构与核心机制

### 恢复槽与候选机制

1. **RecoverySlot 门控**：基于 `armedByFailure ∧ primed ∧ hasMaterial` 判定是否激活恢复操作。arming 为单次执行内存状态，不写入持久化 Journal。
2. **PrefixProbe 选择**：候选前缀仅存在于 attempt profile 中，仅当 cutoff 严格前进且 digest 校验一致时才允许作为 probe 发送。
3. **分派与提交**：按 RequestKind 分派后继；Probe 成功原子提升 ActivePrefixEpoch，Squash 成功则压缩前半段 frames 并递增 FrameEpoch。

### Blogger 压缩与连续追平

1. **Delta 分块**：依据 200 KiB 上限按语义 part 边界切分，cutoff 仅在完整 turn 推进。
2. **连续 catch-up**：每次 cycle 提交后直接从当前 canonical coverage 与 XTrace Current 重新派生下一块；暂时无材料时进入 park 等待，新材料到达后恢复追平，不设置冻结上限。
3. **Host Compaction 收容**：观察到外部 compaction 时触发 `ContextReanchored`，推进 epoch 并将旧 horizon 的辅助注入可见性清空。
4. **Opening floor**：Manager Life 只以真实 Opening 后的 `WorkRecordStart` 作为压缩下界；T1 commitment 仍可属于 WorkRecord 的 constitutive Opening，但不再获得 provider-context 的 raw 常驻权。
5. **TodoWrite X 穿透**：X-wire 应用 synthetic prefix 时，在 `dropLeading` 范围内先收集 `todowrite` call id，再把含该 call 或对应 result 的原始 Host 消息穿透到 synthetic memory 与未覆盖 tail 之间；其余 dropped 历史继续由 Y replacement 取代。

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
| CONTEXT-COMPRESSION-018 | `requirements/context-compression/tests/blogger-delta.test.mjs` |
| CONTEXT-COMPRESSION-019 | `requirements/context-compression/tests/injected-context-reanchor.test.mjs` |
| CONTEXT-COMPRESSION-020 | `requirements/context-compression/tests/ctx-opening-floor.test.mjs` + `requirements/provider-projection/tests/projection.test.mjs` |

## GAP

- CONTEXT-COMPRESSION-017/020：`ctx-opening-floor.test.mjs` 证明 pre/post-T1 floor 等价与 todo round retention 纯判定；`provider-projection/tests/projection.test.mjs` 证明真实 Y prefix write-back 越过 `todowrite` 时 call/result 仍以原始 X Host 消息存在。CLOSED。
