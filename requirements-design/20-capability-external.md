# Capability enforcement / External investigation

## `capability-enforcement`

WHY: semantic authority 若只写在 Role Law/office model 中，而 provider schema 与 runtime execution gate 各自维护能力表，就会出现“看得见但不能做”或更危险的“看不见却能执行”的分叉。

OWNS:
- 每次 provider attempt 有一个 canonical ToolCapabilitySet/等价 capability projection。
- provider-visible action schema 与 runtime execution gate 必须读取同一 capability truth。
- capability projection 可按 office + request contract 收窄，但不得扩大 office entitlement。
- execution strength/tier 不改变同 office 的 authority；request-specific replica/leaf 可以进一步收窄 capability。
- internal-only participants/actions 不进入无资格 participant 的 enum/schema/choice surface。
- Host-native/MCP/plugin-generated 等不同技术来源的 actions 最终仍服从同一个 semantic capability policy；技术 gate 可以多层，semantic source 只能一个。

DOES NOT OWN:
- office 有资格产生什么 consequence；由 `office-capability` 拥有。
- action description 的认知合同；由 `action-affordance` 拥有。
- 当前 ToolPermission enum、Role→tool 精确表、MCP wildcard 字符串、ToolRegistry implementation。
- Persona/agent naming。

DEPENDS ON: `office-capability`, `participant-identity`。

PROVIDES: participant 能看见的 capability 与实际能执行的 capability 同构且不越权的 guarantee。

FAILURE MEANING: RED = schema/gate 漂移、某 execution tier 获得额外 authority、或 internal action 能被无资格 participant 合成/执行。

INDEPENDENT CHANGE: ToolPermission Set 改成 capability tokens/traits，并重写 Host/plugin gate，只要 office authority 与 participant-visible action contract 不变。

CURRENT EVIDENCE: AGENT-006/007/010；`AttemptExecutionProfile.ToolCapabilitySet`；`Roles.permissions`；ToolRegistry/Host permissions；agent-permission-gate；Strength read-only request projection；MCP role locks。

---

## `external-investigation`

WHY: public/external facts 来自会变化、会冲突、需要 provenance 的远方世界；网络可达性不等于 source ownership，外部可能性也不能自动变成 repository obligation。

OWNS:
- external/public-web fact acquisition 的 evidence contract。
- provenance first：选择尽量接近事实源的来源，并保留来源/时间/不确定性足以支撑 claim。
- disagreement 不静默平均；呈现冲突来源与可解释差异。
- visual-only facts 可使用视觉 observation；页面 reachability 本身不证明内容。
- external evidence 与 local repository evidence 分离。
- external facts 只建立外部世界事实，不自动产生 repository/product obligation。

DOES NOT OWN:
- Browser office entitlement canonical definition。
- network/MCP implementation、stealth-browser 具体项目/ref/config。
- repository investigation。
- epistemic synthesis。

DEPENDS ON: `office-capability`, `participant-horizon`, `host-boundary`。

PROVIDES: 带 provenance 的外部事实 evidence guarantee。

FAILURE MEANING: RED = 网络可达内容可被无 provenance 当作事实，来源冲突被抹平，或外部可能性被直接升级成 repository obligation。

INDEPENDENT CHANGE: 从当前 browser backend 换成另一 browser/search backend，而 provenance/evidence boundary 不变。

CURRENT EVIDENCE: Browser Role Law semantic anchors；ARCH-017 Browser consequence；AGENT-026 MCP integration；fork Browser boundary。
