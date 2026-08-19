# execution-model-routing — WHY

## 1. 问题

旧模型把 22 个 managed agent 的 `model` 写进 Host `opencode.json`，再由 Wanxiangshu 读取 Host-final inventory。上一版设计虽然把 authority 搬到独立 TOML，却仍把七个 lane、候选顺序、`max_sessions` 与 first-free 算法固化进 Wanxiangshu runtime。

这两种做法本质相同：资源策略仍由产品代码/schema 拥有。模型池一变、并发策略一变、某个角色要特殊调度，就必须改正式配置结构或 runtime；用户不能把“我现在有哪些模型、各能跑多少、哪些角色怎么共用”压成一个自包含策略。

## 2. 更短的充分边界：真实 occupancy 在 runtime，选择算法在 MJS

模型调度真正不可消除的 runtime 事实只有三类：

```text
当前需要哪个机器角色 role
当前已经占用了哪些 ModelTarget running
当前接续 Session 最近一次成功物理执行使用的 ModelTarget previous（新 Session = null）
```

其余都可以是纯策略：

```text
(role, running, previous) -> { model, reasoning } | null
```

Wanxiangshu 只维护真实 lease multiset、最近一次物理执行 target、串行 acquire/release 与 Host 投影；用户的 `wanxiangshu.mjs` 自己决定角色分组、模型优先级、容量、是否优先延续上一 LLM、视觉模型、fast/deep 差异等。`previous` 只是选择提示，不占容量，也不把上一 lease 复活成 session 级永久绑定。这样产品内不再存在第二份“调度知识”。

这里的 lease 还必须拆成 execution binding 与 provider capacity token。前者回答“这条 physical material 固定跑哪个 target”，后者回答“谁此刻有资格开始下一 provider step”。parent 在 tool/join 上等待时，binding 仍成立，但 capacity 可以定向借给 lineage descendant；ancestor 恢复时只在 step 边界召回，因此不会把硬 provider 限额变成瞬时软限制。

Blogger 是 Main 的 companion，不是 Main 自己的第二个 provider step。若 Child Main 借了 Parent Main 的 capacity，而 Child Blogger 仍只能看 Main lineage，它要么错误复用 Main token、把 Main/Blogger 串成一条 provider lane，要么让 Parent Blogger 对应的可借 capacity 留在原 family 上空转。正确关系是平行借用：Main 的实际 lender 决定本次 Blogger execution 可见的 companion lender；Main 没借，Blogger 也没有 companion credit。这样 Main/Blogger 两条 capacity 既一起下放，又能分别在各自 step 边界 recall。

capacity arbitration 放在独立 F# 对象中：旧式真实 token ledger 是内核，borrow/recall 是 decorator。MJS 仍只是同步纯选择函数，Host/Tool 业务流也只碰极小边界。复杂度被压在唯一 owner 内，而不是散进 join、fission、sync delegate、transform 各处。

## 3. 为什么 `running` 用 multiset，而不是容量字段

容量不是 runtime 应理解的 schema。`running` 保留完整 target occurrence，因此 scheduler 可以按 exact target、model family 或 provider 自己聚合。推荐模板按 `provider`（完整 `provider/model` 中 `/` 前的部分）计数：同一 provider 下不同 model/reasoning 的 active lease 合并占用同一 provider 并发预算。

重复项不能去重。不同 role 共用同一 `{model, reasoning}` 时，自然在同一数组中累计；同一 SessionId 的 A/B 两个 EffectiveAgent 若都取得相同 target，也必须贡献两个 occurrence，因为它们是两个独立稳定 lease。

这种表示足够表达：

- 单模型并发上限；
- provider 级并发上限（跨 model/reasoning 合并）；
- 多候选优先级；
- fast/deep 分池或共池；
- Browser 单独廉价视觉池；
- reasoning 强度独立计数或合并计数；
- 任意只依赖 `role + running` 的自定义策略。

这些都不需要 Wanxiangshu 新增字段。

## 4. 为什么 `null` 是 backpressure，不是失败

`null` 表示 scheduler 在当前 occupancy 下不愿意分配任何 target。这不是 provider 已失败，也不是业务失败，更不是让 runtime 自己猜一个备用模型的邀请。

required execution 应保持 pending；只有真实 occupancy 变化时才重新求值。这样等待完全事件驱动，没有轮询，没有跨池偷跑，也不会污染 AABB failure budget。可丢弃优化可以按自身合同在 `null` 后放弃，例如 Strength 回到 K0。

## 5. 为什么 managed lease 要稳定到 physical user execution，而不是 session

Session 是可复用容器：业务 completed、handle retired、甚至 Host close 都不能可靠回答“下一次是否还会在这个 SessionId 上执行”。反过来，`PhysicalUserMessageId` 精确命名了 Host 当前正在处理的 user material。若把槽冻结到 session，就会让 idle 后的可复用 session 永久占容量；若按 EffectiveAgent 给同一 session 同时留 A/B 两份 lease，又会把一次物理 execution 双计数。

因此 managed lease 以 `(SessionId, PhysicalUserMessageId)` 为 identity，EffectiveAgent 是该 execution 的稳定属性。同一 physical id 的 provider retry 复用 target；同一 SessionId 出现新 physical id 时，新 material 自身就足以 supersede 旧 lease，不依赖 idle 必达。AABB 切到 peer 只影响**下一条 physical execution**的 EffectiveAgent，不保留一份 session 级 B 槽。

新的 physical execution 仍然必须重新调用 scheduler；只是同一未删除 Session 的上一成功 target 会作为 `previous` 一并传入。这样 MJS 可以在角色策略仍允许、provider 仍有容量时优先保持原 LLM，减少接续对话的模型漂移；一旦原 target 不再属于当前 role 的候选或 provider 已满，仍可立即选择其它 target。exact terminal 释放容量但不删除这份无容量语义的 previous hint；session delete/scope cleanup 才清除它，所以真正新对话得到 `null`。

## 6. 为什么事件驱动状态必须 process-shared

OpenCode 会按 directory/worktree 建多个 plugin instance，但它们仍在同一 OS process 中争用同一模型资源。若每个 instance 各维护 `running`，MJS 看到的 occupancy 就是假的。

因此 lease registry 与 pending demand queue 必须是 module-level process-shared owner；root/worktree 都从同一 snapshot 调度。不同 OpenCode 进程之间不做本地协调。

## 7. 为什么不再有产品级 lane，也不要求 fast/deep model 不同

“七个 lane”可以继续作为用户 MJS 的一种写法，但不再是 Wanxiangshu 的产品语义。MJS 可以保留七档，也可以把 22 个角色做完全不同的映射，甚至让两个角色共享同一 target 和同一容量预算。

AABB 的 A/B 区别来自 EffectiveAgent，不来自 model string。`fast-X` 与 `deep-X` 最终返回完全相同的 `{model, reasoning}` 是合法世界；Strength 是否值得启动也由自己的成本/eligibility 判断，不靠全局 model 互异校验。

## 8. 为什么配置必须只有一个 authority

只要 `opencode.json`、内建 lane 表或环境变量仍能决定 managed model，就会重新出现多份 truth。新世界只承认：

```text
~/.config/opencode/wanxiangshu.mjs
```

Host `opencode.json` 仍承载 Host 自己的其它配置，但 managed agent 的 `model` 不被 Wanxiangshu 读取为 routing truth。没有 env/model fallback，也没有 runtime 内建默认模型。

## 9. 为什么缺文件要生成模板，而不是直接失败或藏默认

首次安装若要求用户先手写完整 scheduler，入口成本过高；但若 runtime 在文件缺失时偷偷使用内存默认，又会重新产生第二 authority。唯一同时满足可用性与单一真相的做法是：**缺文件 → 把当前推荐策略真实写成 `wanxiangshu.mjs` → 再加载这份文件。**

生成只发生一次。已有文件无论内容多旧都不自动覆盖，升级后的新推荐也只影响未来首次生成；用户看到的文件就是正在生效的配置。并发 root/worktree/plugin/process 初始化必须用原子 create-if-absent，避免模板写入覆盖用户或另一实例刚创建的文件。
