# relay-assessment — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：ScoreVector、AssessmentBinding（含 IncumbencyId 绑定）、QualityCertificate、`RetirementOutcome`。精确 replay 须 identity/binding/snapshot/authority/scores 全一致，跨迭代重放拒绝。
- `src/Wanxiangshu/Mission/Relay/Assessment/Model.fs(.fsi)`：八维校验与 obligation derivation（Quality Ledger 写入）。
- `src/Wanxiangshu/Mission/Relay/Fold.fs` 与 `OpenCode/ReviewTool.fs`：一次性 admission、快照校验与领域转换。
- `src/Wanxiangshu/Mission/Relay/OpenCode/ReviewTool.fs(.fsi)`：OpenCode schema/codec；`spec.Description` 只用 `tool/review/description`，`acceptedResult` 按 `allPerfect` 在 `runtime/manager-work` 与 `runtime/manager-finish` 之间二选一；领域判断委托给 assessment owner。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：`resourceForCurrentAction` 只从 active incumbent、accepted assessment transport 与 exact bound certificate 选择 nudge 文档：无 assessment 配 `runtime/manager-assess`，未持有效证书配 `runtime/manager-work`，exact valid certificate 配 `runtime/manager-finish`。Manager 独立评估通过只读 Engineer 实例取证，接收到 DevOps 验证自修推进的快照时触发证书失效。
- `src/Wanxiangshu/Mission/Relay/Assessment/Surface.fs(.fsi)`：唯一 JS proof surface（schema parse）。

## 编译边界

`mission-relay-workspace-snapshot` 直接引用 `runtime-platform/digest`，保留 GitSubject 与 Relay core 的真实依赖，不再因字符串摘要引入 OpenCode 消息／事件合同。`WorkspaceSnapshot.canonical` 的 HEAD tree、status、index、binary diff、untracked blob hash 及分隔符不变，`capture` 和公开签名不变。

## 角色与生命周期演进

1. **独立评估实例**：独立评估不依赖历史专职 Inspector 角色，由 Manager 派出只读 Engineer 实例建立事实；实现者的自评结论不得作为评审裁决。
2. **DevOps 自修与证书失效**：DevOps 拥有固有非架构级修复权，自修修改工作树后快照发生推进；旧快照上签发的 `QualityCertificate` 随快照失效，新改动必须由下一任独立迭代重新评估。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `obligation-ledger`
- `participant-identity`
