# execution-model-routing

`execution-model-routing` 拥有 Wanxiangshu 的物理模型资源路由：模型配置从哪里来、EffectiveAgent 属于哪个 lane、一个 lane 如何按容量选择模型、何时等待、跨 root/worktree 插件实例如何共享容量，以及 session 何时释放模型占用。

阅读顺序：

1. [WHY.md](WHY.md)：为什么模型资源调度不能继续寄生在 `opencode.json` 的 22 个静态 agent binding 上。
2. [WHAT.md](WHAT.md)：唯一 normative 合同（EMR-001..010）。
3. [HOW.md](HOW.md)：TOML 形状、lane 映射、process-shared allocator 与 Host 投影实现模型。
4. [PROOF.md](PROOF.md)：每条命题的可执行 proof 计划与当前 GAP。

## 一句话 WHY

ExecutionBinding 的身份选择与物理模型资源调度必须分离：agent/tier 决定执行档位，Wanxiangshu 再从唯一 TOML 配置的七个有界 lane 中为 session 选择模型；Host `opencode.json` 不再拥有 managed model authority。

## DEPENDS ON

- `participant-identity`：提供 CanonicalRole / Fast·Deep / EffectiveAgent / peer 本体。
- `managed-session-lifecycle`：提供 session retire/delete 生命周期边界。
- `host-boundary`：提供 plugin load、root/worktree 多实例与 Host prompt/config 适配边界。

## DOES NOT OWN

- AABB/失败预算/何时切 peer → `provider-attempt-recovery`。
- Role/Persona/权限 → `participant-identity` / `office-capability` / `capability-enforcement`。
- Host SDK/hook 是否真的允许 request model mutation → `host-boundary` canary。
- provider 本身的限流、计费或模型能力声明 → 外部 provider；本包只执行用户配置的本地容量合同。
