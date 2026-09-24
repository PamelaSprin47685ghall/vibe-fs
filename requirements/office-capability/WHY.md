# office-capability — WHY

## 领域动力与核心张力

系统内协作的基础在于将工作托付给有资格产生对应后果的 Office。核心张力在于**后果模型（Consequence Model）**与**表面清单（工具白名单/Persona 名称）**之间的区分：

```text
有权产生的后果 (Entitled Consequence) ──► Engineer 调查与改变代码 / DevOps 执行并自主局部修复 / Manager 评估与编排
表面投影 (Projections)               ──► 权限矩阵 / 工具列表 / Prompt 文案
```

若以工具可达性或 Persona 名字来定义 Office，会诱发严重退化：
- **工具清单冒充权能**：认为职责只是工具白名单的堆叠，或将 DevOps 视为「纯命令执行器」而剥夺其必要的工程修复能力。
- **人为割裂造成反复交接**：强行将本地调查（Inspector）与源码实现（Coder）拆成互不信任的角色，导致简单事实确认也要跨代理往返。
- **权限与职责错位**：将 Fission 扩散至管理角色或执行角色，造成责任主体混乱与状态撕裂。

`office-capability` 的核心不变量：
- **后果定义职位**：Office 仅由其有权产生的后果（Entitled Consequence）及其明确禁止的非后果（Non-consequence）定义。
- **角色体系**：确立正交清晰的权能分工：
  - **Engineer**：负责本地事实调查与源码工作（读、写、改、移、删文件，实现、重构、编写测试源码）；不执行真实命令，不差遣 DevOps，不承担外部网络浏览职责；是**唯一具备 Fission 角色能力**的 Office。
  - **DevOps**：具备 Engineer 的全部本地工程能力，加真实命令执行、终端和进程管理；拥有**角色固有的非架构级修复授权**（执行中发现局部问题自行调查、直接修改源码、补充回归测试并重新验证，禁止任何 `allowRepair` 式逐次批准开关）；自身不 fork、不 Fission，不擅自作架构或产品决策。
  - **Manager**：负责独立评估、任务分解、派工、推进未尽账本、组织接力；不承担直接源码实现且不亲自修改工作树（仅限评审接纳前使用专用只读工具直接取证），不 Fission；通过 `fork` Engineer 获得并行，通过 `resume` 固定 DevOps 获得执行与局部修复。
  - **Orchestrator**：负责顶层战略战役统筹、独立道路委任、取舍与汇聚；不亲自修改源码，不直接调度 Engineer 或 DevOps，不 Fission。
  - **Sphinx**：程序控制探究流程、工作项、预算、续行和收束；可同步调用只读 Engineer 调研；不是 Role/Persona，无独立 Fission 身份，不保留 Inquiry 驾驶层。
  - **Blogger**：保留既有工作历史和上下文职责，不取得组织指挥权，不接管 Distiller。
  - **Bookkeeper**：根据 Engineer 轨迹整理案例，根据文件 diff 维护旧案例，更新时不读仓库、不读完整文件、不重放历史观察，不 Fission。
  - **Predictor**：内部机制专用角色，仅为 Strength 降级指定廉价 provider/model，不参与普通调度与工具门禁，无工程、执行或 Fission 权限。
- **单一语义所有权、多处投影**：同一后果事实在 Manager Role Law、fork/resume 描述、各 Office 自我模型中保持严格一致，不得漂移。
- **不可互换性**：各 Office 之间严禁作为通用代理相互替代。
- **Role 是本名**：Orchestrator/Manager/Engineer/DevOps/Blogger/Bookkeeper/Predictor 是本名 Role，每个活跃 Role 恰对应一个 Persona 且权能完全相同，不存在 fast/deep 档位差异或组合名解析。活跃路径不含 Coder、Inspector、Browser、Inquiry、Distiller。

## 破裂后果

- Office 职责边界模糊重叠，产生越权操作或无效托付。
- 将 DevOps 降格为命令包装器，迫使其在遇到微小失败时反复回退或无法修复代码。
- 将 Fission 错误赋予 Manager 或 DevOps，导致多 present 责任主体失控。
- 参与者根据工具列表反推职责，破坏系统分层保证。

## 边界与关系

- `participant-identity`：提供 Role 身份定义；本包定义各 Role 有资格产生的后果。
- `capability-enforcement`：负责将后果模型投影为 Host schema 与运行时执行 gate。
- `delegation`：消费本包的后果模型以执行按后果托付。
- `participant-horizon` 与 `action-affordance`：引用本包的后果定义组织认知视界与动作契约。

## DEPENDS ON

- `participant-identity`
