# structured-workflow — WHY

## 不可替代的存在理由

宿主编程语言的调用栈已经为业务流程提供了全部结构化边界：
- `let!` / `await` 表达异步等待事实或效果；
- `do!` 表达执行副作用；
- `match` / `match!` 表达条件分支；
- `return!` 表达继续与有界递归；
- `use!` / `try-finally` 表达确定性的资源作用域与清理。

如果为了管理流程而将「程序下一步走到哪」重新编码为可持久化存储的字段（如 `CurrentStage`、`NextAction`、`InFlight`、`Parked`、`Sealed`），等于在业务层手工再造一个**第二运行时（second runtime）**。这种手写运行时会带来灾难性代价：
1. **虚假恢复**：执行位置（调用栈）是不可直接序列化的。试图恢复手写状态机的「程序计数器（PC）」往往导致在错误的历史基座上恢复，或丢失真实的执行上下文。
2. **测试退化**：测试被迫断言内部状态枚举或私有字段，而不是验证可观察的业务效果与持久事实，使验证体系丧失对真实行为的保真度。
3. **状态空间爆炸**：多个 stage/bool/option 字段的正交组合导致系统存在大量无业务意义的非法状态，类型系统不仅无法拦截，反而被迫为其兜底。

`structured-workflow` 的核心存在理由是：**确立「控制流不是领域状态」的铁律，强制业务流程直接由宿主语言原生语法结构表达，彻底消灭手写的第二运行时。**

## 核心张力与架构哲学

- **直执而不是解释（Direct CE over Interpreter）**：领域 DSL 由 CE（Computation Expression）与具名语义操作直接构成并直接执行，严禁构造内部 AST 后再通过通用解释器进行重放或轮询驱动。
- **状态标签仅表达真实事物**：DU（联合类型）与字段仅用于表达封闭领域词汇、DurableFact、Evidence/Decision、ExternalSignal、Witness、Capability、Receipt 或 PhysicalHandle，严禁充当程序计数器。同名协议碰撞必须正向分类，路径不提供豁免。
- **组合具有结构闭包（Compositional Closure）**：父 workflow 组合子 workflow 时，只能观察子流程的类型化输入、领域结果与能力证明，严禁读取、存储或驱动子流程的内部执行位置；此约束跨 module/callback 仍成立。
- **高阶 trace 必须可解释**：once-through scope 可透明组合；retry/fallback/recovery/deadline 等重复或恢复路径调用必须由 owner law、明确 trace relation 与可执行证明约束其 failure/cancel/deadline 行为，不能藏进 generic middleware。
- **root 宽而浅**：composition root 可看见大量 construction/topology/order/lifetime wiring，但不得因此拥有 foreign policy；深语义必须回到 owner，不能以 LOC/import-count 代替语义审查。
- **以可观察效果证明流程**：流程的正确性完全由领域事实、端口交互、调用 trace 与最终状态证明，不由内部解释器运行到了哪一步来定义。
- **依赖必须穿过 owner 海关**：编译引用只说明实现依赖，requirement 引用只说明命题前提；两张图不可混同。跨 owner 的生产依赖必须落到 owner 明确承诺的 published contract、physical port/adapter 或 composition-root wiring。语言层面的 `public`、目录位置和 `Surface.fs` 文件名都不构成授权。
- **语义豁免必须消费真实 production proof edge**：执行位置词汇的 semantic-evidence 豁免只能引用 `requirement-trace` 已解析的唯一 active `(path,title,WHAT)` 边，并绑定该 test callback 实际可达使用的 exact registered Surface。文件存在、注释、字符串、skip/todo、同 WHAT 的无关测试或仅在文件其他 callback 使用 production 都不能授权架构边。
- **owner 海关最终必须由 compile-input boundary + F# signature 承载**：semantic owner 仍负责命题与 contract 承诺；production file 另有恰一个 owner-boundary locality，每个 locality 恰一个 fsproj。`Wanxiangshu.fsproj` 只作为 Fable emit 的扁平副本再次列出同一 compile set，不参与 owner topology。Fable 会递归 source-merge ProjectReference closure，所以 `internal`、top-level `module private` 与 `DisableTransitiveProjectReferences` 都不是跨工程 firewall；`.fsi` 则会在 source-merge 后真实隐藏未签名 implementation symbol，但 signature-only project 不能充当可消费 header，仍需真实 contract implementation。foreign semantic owner 只能引用 dependency-inverted 的 contract/adapter locality；这些 locality 及其 transitive closure 不能反向引用 provider runtime/private locality。provider runtime 依赖 contract，而不是 contract 依赖 runtime。只有 runtime source 完全不在 foreign consumer 的 Fable closure 中，且 contract implementation 由 `.fsi` 封口，漏 reference / 越 runtime boundary / sibling implementation access 才会由 compiler 直接拒绝。`published-contracts` 继续约束语言无法表达的 per-consumer exact symbol 承诺。
- **exact cohort 毕业后由 consumer cohort 决定 locality 边界**：同 owner、同目录、同为纯类型也不等于同一 contract。新建或已完成 exact cohort cutover 的 locality 若含 foreign consumer 集不同的 source，会让窄 consumer 自动取得未授权 sibling；因此毕业单元必须 source-pure，再由确实同时需要两者的 consumer 显式引用两条边。尚未毕业的历史混用由 GAP-031 census 明示追踪，不伪装成已全局强制的 gate。
- **物理能力必须独居**：文件、进程、网络等 Host API 是 capability，不是相邻 policy module 的便利 helper。物理 port 必须只有一份 import、独立 signed adapter locality、精确公开实际调用的方法；consumer adapter 只获得它声明的 port。把文件删除能力塞进 tool-policy contract，或在另一个 consumer 内复制第二套 import，会让无关 cohort 获得未登记副作用并制造两个物理 owner。
- **局部编译只改变输入集合，不改变编译器模型**：owner/impact compile 先计算 ProjectReference closure 或 reverse-consumer impact，再按 aggregate source order 合并成一个零 ProjectReference flat fsproj，仅启动一次 Fable。实现 `.fs` 且 sibling `.fsi` 未变时不重编普通 consumer；`.fsi` 改动必须纳入全部 reverse consumers；工程/工具链输入变化保守走 full flat build。全量 release 继续编译与原始单工程完全相同的 source/config union，绝不逐 owner 启动 Fable，因此多工程边界不能给全量构建叠加工程图税。

## 核心不变量与违约状态（RED）

仓库处于 RED 状态，当且仅当出现以下任一破坏结构化工作流的违约：
1. 领域模型或持久记录中包含表达程序下一步去向的字段（程序计数器）。
2. 业务层引入 Command/Reply 总线、Step continuation AST 或调用序列回放解释器。
3. 崩溃恢复尝试恢复协程内部指针或暂停点，而非通过 Journal fold 产生事实后重入普通业务入口。
4. 控制流决策形成第二层及更深的嵌套控制金字塔（lexical pyramid），手写短路样板而未使用标准的 Result/Option 组合子。
5. 模块接缝处暴露内部阶段或运行槽位，导致父模块需要探测子模块状态以驱动下一步业务动作。
6. 使用无界并发或无界重试作为业务流程的默认行为，或把 repeated/recovery-path invocation 藏进无 owner/WHAT/relation/proof/policy 的 decorator。
7. foreign owner 直接读取 private implementation、Stage/Step/cursor/registry presence，或 composition root 匹配 foreign policy DU。
8. composition root 实现深层 semantic helper、动态 pipeline 或 generic middleware/decorator interface。
9. semantic-evidence 通过裸 proof 路径、源码字符串、错误 owner 的 WHAT，或未被 exact test callback 可达使用的 Surface 取得跨 owner 授权。
