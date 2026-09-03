# requirement-grounding — WHY

## 不可替代的存在理由

当项目规范存在于 `requirements/<package>/` 目录中时，开发者与智能体极易在未完整阅读相关规范的情况下先接触源码、形成主观假设甚至提交修改，导致规范退化为事后救火材料。单纯依赖文档提示（如“改代码前先看规范”）无法在上下文压缩与长流程执行中持续生效。

代码路径是执行过程中最早且最稳定可观测的局部事实。`requirement-grounding` 建立了“触碰路径 → 自动接入对应规范”的确定性约束：一旦代码进入视野，与其绑定的本地规范必须以受控方式进入当前执行者的认知视界。

## 核心不变量

1. **按路径自动发现与多包并存**：通过包目录与 `APPLIES-TO` 规则自动解析路径所属的 requirement packages。一个文件可同时归属多个包，命中的规范集合按确定性顺序全部接入，不强行实施单一归属假设。
2. **规范材料严格限制为包根目录 Markdown**：无论通过包内自身路径还是 `APPLIES-TO` 外部规则命中，接地材料集合严格限制为包根目录下直接存在的 `*.md` 规范文档，严禁导入 `tests/**`（可执行证明由验证系统执行）或将 `APPLIES-TO` 清单本身作为正文注入。
3. **Grounding 是弱介入，不是 effect gate**：读取或修改受覆盖代码时，相关规范只作为自动补充的 result-only 字节观察（终端真实工具结果上的 `NUL+BOM` 后缀）进入认知视界。`edit/write/rm/mv` 与可编程 mutation 不得因 grounding 缺失而被拒绝、延期、回滚或要求重发；grounding 只能补知识，不能改变原操作的执行资格与控制流。
4. **候选搜索豁免**：`grep`、`glob`、`list` 等候选发现工具不触发 `APPLIES-TO` 规范注入，避免宽泛搜索引入过度上下文噪声；只有发生明确文件读取或写操作时才激活解析。
5. **所有真实文件读取共享同一认知事实**：原生 `read` 与 `repository-programming` 的 `js-*` 读取只要实际把文件内容暴露给模型，就都记为已读。读取受覆盖代码会触发 grounding；读取 package 根目录 Markdown 等 grounding 材料会直接参与本轮与后续轮次去重，禁止“模型刚读过，系统又自动注入”。
6. **去重与稳定的 Prefix Replay**：去重基于当前 horizon 内实际可见的 grounding 材料事实，而不是只记“系统曾自动注入”。自动读取与执行者主动读取共享同一去重语义；内容变更产生新 digest 后才允许补读新版本。已自动注入规范以终端真实工具结果上的 result-only 字节（含 `requirement_source_path` 来源证明）形式持久化，不产生 synthetic read 调用/结果对，重放时保持字节完全一致以保护 KV 缓存；仅在上下文显式重锚 (`ContextReanchored`) 时重置可见性并按需重新接入。

## 违背边界的破坏形态 (RED)

- **Grounding 劫持控制流**：规范发现逻辑拒绝、延迟、回滚或要求重发本应执行的 write/edit 等操作，把认知辅助错误升级为 effect authority。
- **上下文污染**：模糊搜索触发全量规范载入，或包外代码触碰导致递归载入大量测试实现代码。
- **缓存击穿**：每轮交互重复读取或重新组装规范文档，破坏 provider 稳定前缀。
- **读取事实割裂**：`js-*` 已经读过代码或规范材料，但 grounding 不承认该事实，导致漏触发或重复注入。
- **权限越界**：将规范文档注入提升为指令授权或改变执行者的角色与工具权限。

## DEPENDS ON

`requirement-system`, `host-boundary`, `participant-horizon`, `provider-projection`, `interaction-authority`, `semantic-trace`, `prefix-stability`, `repository-programming`
