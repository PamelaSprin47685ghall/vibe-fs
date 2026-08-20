# requirement-grounding — WHY

## 不可替代的存在理由

当项目规范存在于 `requirements/<package>/` 目录中时，开发者与智能体极易在未完整阅读相关规范的情况下先接触源码、形成主观假设甚至提交修改，导致规范退化为事后救火材料。单纯依赖文档提示（如“改代码前先看规范”）无法在上下文压缩与长流程执行中持续生效。

代码路径是执行过程中最早且最稳定可观测的局部事实。`requirement-grounding` 建立了“触碰路径 → 自动接入对应规范”的确定性约束：一旦代码进入视野，与其绑定的本地规范必须以受控方式进入当前执行者的认知视界。

## 核心不变量

1. **按路径自动发现与多包并存**：通过包目录与 `APPLIES-TO` 规则自动解析路径所属的 requirement packages。一个文件可同时归属多个包，命中的规范集合按确定性顺序全部接入，不强行实施单一归属假设。
2. **包外命中只注入同层 Markdown**：通过 `APPLIES-TO` 外部规则命中的包，仅注入其根目录下的同层 `*.md` 规范文档，严禁递归导入 `tests/**` 等内部测试或将 manifest 自身作为正文注入。
3. **读写分离的准入时机**：
   - **读取触发 (Direct Read)**：直接 `read` 源码文件时，相关规范作为同轮伴随的普通 read 观察进入认知视界；
   - **修改阻断 (Mutation Barrier)**：首次发生 `edit/write/rm/mv` 等写操作时，若目标路径包含未 grounding 的包，本次修改**必须延期执行并阻断文件副作用**，强制先完成规范读取，待执行者依据规范确认后发出的新意图方可真正生效。
4. **候选搜索豁免**：`grep`、`glob`、`list` 等候选发现工具不触发 `APPLIES-TO` 规范注入，避免宽泛搜索引入过度上下文噪声；只有发生明确文件读取或写操作时才激活解析。
5. **去重与稳定的 Prefix Replay**：去重以 `(workspace, package, content digest)` 及当前 horizon 为唯一凭据。已注入规范以普通 read 的 durable occurrence 形式持久化，重放时保持字节完全一致以保护 KV 缓存；仅在上下文显式重锚 (`ContextReanchored`) 时重置可见性并按需重新接入。

## 违背边界的破坏形态 (RED)

- **先写后读**：在未阅读相关规范的情况下先执行了文件修改，导致错误逻辑落盘。
- **上下文污染**：模糊搜索触发全量规范载入，或包外代码触碰导致递归载入大量测试实现代码。
- **缓存击穿**：每轮交互重复读取或重新组装规范文档，破坏 provider 稳定前缀。
- **意图伪造**：首次修改被阻断后，系统自动缓存并替执行者重放旧的修改参数，剥夺了执行者基于新阅读规范改变决策的机会。
- **权限越界**：将规范文档注入提升为指令授权或改变执行者的角色与工具权限。

## DEPENDS ON

`requirement-system`, `host-boundary`, `participant-horizon`, `provider-projection`, `interaction-authority`, `semantic-trace`, `prefix-stability`, `repository-programming`
