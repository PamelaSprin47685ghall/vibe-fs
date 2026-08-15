# execution-model-routing — WHY

## 1. 问题

旧模型把 22 个 managed agent 的 `model` 写进 Host `opencode.json`，再由 Wanxiangshu 读取 Host-final inventory。这个结构把两件不同的事绑死：

- `fast-*` / `deep-*` 是 ExecutionBinding 的机器身份；
- `provider/model + variant + 并发上限` 是可替换的物理资源。

结果是每换一组模型都要复制修改大量 agent 条目；同一物理模型被多个角色共用时没有全局容量语义；模型资源紧张时也没有 lane 内有序选择与等待机制。更严重的是，Host 配置因此成为 managed model authority，Wanxiangshu 无法保证自身 fallback/Strength/session binding 与实际 provider model 一致。

## 2. 必须分开的两个轴

Participant identity 已规定 Role / Persona / ExecutionBinding 三轴分离。这里继续把 ExecutionBinding 拆成两层：

```text
EffectiveAgent = fast-coder / deep-coder / ...
        ↓ fixed routing law
Lane = medium / higher / ...
        ↓ runtime capacity admission
ModelTarget = provider/model + optional variant
```

AABB、peer、Strength 仍只改变 EffectiveAgent；它们不直接选择 provider model。模型选择是资源调度，不是身份判断。

## 3. 为什么需要 lane

模型资源不是逐 agent 独立配置，而是按成本/能力档位复用：

- 最快非推理通道承载 title/compaction 与 Distiller/Blogger fast side；
- Distiller/Blogger deep side 有独立最快-II 通道；
- Inspector/Bookkeeper fast side 使用快速但更可靠的只读模型；
- 常规 fast 工作使用中档模型；
- 常规 deep 工作使用高档模型；
- `fast-browser` 与 `deep-browser` 分别拥有第 6 / 第 7 lane，可独立配置视觉模型资源；fast side 偏廉价非推理识图，deep side 可使用更强视觉模型。

lane 是稳定产品语义；具体 provider/model、顺序与并发数是用户配置。

## 4. 为什么容量按物理模型共享

若 `fast-coder` 与 `fast-reviewer` 都落到同一物理模型，却各自按 agent 计数，就会把同一 provider 资源重复预算。容量必须按 `providerID/modelID` 聚合，并跨角色、跨 lane、跨 root/worktree 插件实例共享。`variant` 改变请求档位，但不把同一物理模型伪装成两份独立容量。

## 5. 为什么满载要等待而不是降级

lane 已表达最低能力/成本边界。满载时跨 lane spill 会悄悄改变产品质量档位；超卖会使 `max_sessions` 失去意义；失败则把正常资源竞争误报成业务错误。因此唯一一致行为是：保持 lane，等待其中任一候选释放容量，再按原顺序重新选择。

## 6. 为什么不再要求 fast/deep model 不同

AABB 的 A/B 区别来自 `EffectiveAgent` 与其 lane，不来自 model string 必须不同。用户可以让两个档位最终落到同一物理模型，也可以用同一模型的不同 variant；Browser 的 fast/deep 更可能共享同一廉价视觉模型。强制 `fast-X.model <> deep-X.model` 会把资源配置偶然性误升格成身份不变量。

Strength 是否值得执行由自己的成本/eligibility 规则判断，不应靠全局“两个 model string 必须不同”校验替代。

## 7. 为什么配置必须只有一个 authority

只要 `opencode.json` 与 `wanxiangshu.toml` 都能决定 managed model，就会出现两套可写 truth。新世界只承认：

```text
~/.config/opencode/wanxiangshu.toml
```

Host `opencode.json` 仍可承载 Host 自己的其它配置，但其中 managed agent 的 `model` 既不被 Wanxiangshu 读取，也不得覆盖 lane allocator 的选择。没有 env/model fallback，没有“缺 TOML 就沿用旧 inventory”的兼容路径。
