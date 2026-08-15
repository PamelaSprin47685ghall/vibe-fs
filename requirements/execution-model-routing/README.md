# execution-model-routing

`execution-model-routing` 拥有 Wanxiangshu managed session 的物理模型调度边界：模型策略从哪里来、scheduler 看到什么 occupancy、`null` 如何形成事件驱动等待、root/worktree 插件实例如何共享占用，以及 managed session 何时释放 ModelTarget。

阅读顺序：

1. [WHY.md](WHY.md)：为什么模型策略应从 Host/static config 与 runtime 内建 lane 中抽离成单个 MJS 函数，以及为什么缺文件时应生成可见模板而不是藏内存默认。
2. [WHAT.md](WHAT.md)：唯一 normative 合同（EMR-001..009）。
3. [HOW.md](HOW.md)：MJS ABI、process-shared occupancy actor、事件驱动 retry 与 Host 投影实现模型。
4. [PROOF.md](PROOF.md)：每条命题的可执行 proof 计划与当前 GAP。

## 一句话 WHY

ExecutionBinding 的身份选择与物理模型策略必须分离：EffectiveAgent 决定“当前是哪一个执行档位”，唯一 `~/.config/opencode/wanxiangshu.mjs` 仅凭 `role + running` 决定“现在能给它什么 ModelTarget”；文件缺失时只原子生成一次推荐模板，之后用户直接编辑该唯一 authority；runtime 只维护真实 occupancy/lease，不再内建 lane、容量表或候选算法。

## DEPENDS ON

- `participant-identity`：提供 CanonicalRole / Fast·Deep / EffectiveAgent / peer 本体。
- `managed-session-lifecycle`：提供 managed session retire/delete 生命周期边界。
- `host-boundary`：提供 plugin load、root/worktree 多实例与 Host prompt/config 适配边界。

## DOES NOT OWN

- AABB/失败预算/何时切 peer → `provider-attempt-recovery`。
- Role/Persona/权限 → `participant-identity` / `office-capability` / `capability-enforcement`。
- MJS 内具体模型池、容量、成本/能力分类 → 用户调度策略。
- Host SDK/hook 是否真的允许 managed request model/reasoning mutation → `host-boundary` canary。
- provider 本身的远端限流/计费 → 外部 provider；本包只维护当前进程的本地 lease multiset。
