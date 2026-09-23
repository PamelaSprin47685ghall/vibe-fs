# provider-language — WHAT

## [001] ProviderLanguage 是二元封闭类型

`ProviderLanguage` 是封闭的枚举类型（`English | SimplifiedChinese`）。语言是强类型而非任意字符串，Locale 资源文件名与目录结构由类型确定（`English → en.md`，`SimplifiedChinese → zh-CN.md`）。

## [002] 语言在会话创建时单次绑定且不可变

`SessionProviderLanguage` 在 Session 创建瞬间完成绑定，此后严格不可变。对同一会话重复绑定相同语言保持幂等，尝试绑定不同语言必须立即报错（fail-closed）。任何运行期事件（重试、故障转移、压缩、恢复）均严禁修改已绑定的语言。

## [003] 子会话严格继承父级语言而不重读全局偏好

所有派生会话（child / attached / internal execution）直接继承其 owner 绑定的语言。继承过程严禁重新读取全局环境配置，确保整个协作链条在同一自然语言环境中运行。

## [004] 全局偏好阶梯解析与新建会话绑定

全局语言偏好按固定阶梯解析判定：
1. **显式环境变量**：`WANXIANGSHU_PROVIDER_LANGUAGE` 具最高优先级；
2. **宿主配置**：`opencode.json` 等宿主配置中的 `language` 字段；
3. **系统与宿主 IDE 本地化环境探测**：在未显式配置时，若宿主 IDE 语言环境（`VSCODE_NLS_CONFIG`）、POSIX 环境变量（`LC_ALL`、`LC_MESSAGES`、`LANG`）或 Node.js `Intl` 判定为中文系，自动判定为 `SimplifiedChinese`；
4. **最终兜底**：若上述均未指定或非中文，默认兜底为 `English`。

显式配置具有绝对覆盖权（例如系统环境为中文但显式配置为 `English` 时，严格遵循显式配置）。用户在运行期修改全局语言偏好，仅对变更后新建的根会话生效。已绑定语言的既有会话不受影响，确保历史上下文与前缀缓存的字节连续性。

## [005] 语言呈现分为 Class A、Class B 与 Class C

进入 participant horizon 的内容分为三类：
1. **Class A（Provider Prose）**：面向模型的自然语言文本（System Prompt、Role Law、工具描述、执行后果、Finality 文案），必须完整进行本地化。
2. **Class B（Technical Literals）**：机器与协议标识符（工具名、参数名、Wire 字段名、枚举字面量、代码路径、命令等），严格保持原样，永不翻译。
3. **Class C（Internal Diagnostics）**：内部诊断与日志，不进入模型感知范围，不参与 Provider 多语言体系。

## [006] 资源成对存在且缺失本地化时严格失败

所有 Provider 语义资源目录必须同时包含 `en.md` 与 `zh-CN.md`。已绑定语言的会话在请求缺失的本地化资源时必须立即失败，严禁静默回退到英文。

## [007] 模板占位符结构对称且填值不翻译

参数化文本模板中的 `{{name}}` 占位符集合在各语言版本间必须严格一致。运行时注入的具体参数值不进行二次翻译；模板中存在未替换的占位符时必须立即报错。

## [008] 同一参与者面对的工具文本与会话语言一致

参与者感知到的所有工具描述与调用契约，必须与其 `SessionProviderLanguage` 严格一致，严禁出现系统提示词与工具描述语言混杂的情况。

## [013] 中文偏好下所有 Class A 文本必须抵达模型即为中文

环境变量 `WANXIANGSHU_PROVIDER_LANGUAGE` 一经观测为中文，任何进入模型感知范围的 Class A 文本都必须是中文。以下路径严禁出现英文文案：
1. **Host 配置投影**：写入 `config.agent.<role>.prompt` 与 bookkeeper prompt 的提示词，必须取自全局偏好所选语言，不得取自安装时固化的英文视图；
2. **Host 系统段修复**：`ProviderSystemTransform` 必须把 Wanxiangshu 自有的角色提示词段改写为会话绑定语言，无论该段来自规范英文、规范中文还是安装视图；
3. **JS 边界模块**：面向模型的指令平面（Companion 指令、horizon 名册、todowrite schema 描述）必须绑定语言，严禁硬编码 `ProviderLanguage.English`。

凡语言未绑定或偏好未设置时才退居英文默认值；一旦偏好为中文，英文文案不得抵达模型。已绑定的会话即使偏好随后改为中文，也必须保持自身语言。

## [009] 散文文本三向所有权分离与集中装载

语义内容归属于各领域的 Semantic Owner，语言归属于 Session 绑定，渲染布局归属于通用机制。严禁在业务代码中使用 `match lang` 分支硬编码自然语言字面量；所有 Class A 文本统一经由 `ProviderResources` 加载。

## [010] Role Law 语义锚点跨语言成对命中

同一语义锚点标识（Semantic Anchor ID）必须在英文与中文版本的 Role Law 中同时命中，机械化保证不同语言版本表达完全等价的认知边界。

## [011] 协议标识符在所有语言中保持全局唯一不变

工具名称、参数名称、协议字段、枚举值等机器标识符在所有语言环境中保持不变。相同的标识符在任何语言下均严格指向完全一致的契约。

## [012] 核心角色双语 Prompt 语义一致与同源认知

核心角色（Engineer、DevOps、Manager 及 Sphinx 内部调研 Engineer）的 System Prompt、Role Law、工具描述及示例在中英文双语版本间必须严格保持语义一致与同源认知：
1. **Engineer**：明确负责本地事实调查与源码工作，无真实执行权，完成即返回；明确是唯一允许使用 Fission 的角色；
2. **DevOps**：明确具备完整本地工程能力与真实执行能力，拥有非架构级直接修复授权；明确自身不能 Fission，不创建/差遣其他工程代理；
3. **Manager**：明确管理任意数量 Engineer 与唯一固定 DevOps；明确自身不能 Fission，不创建管理分身；
4. **Sphinx 内部 Engineer**：明确本次调用仅调研现有本地事实，无修改、无执行、无 Fission 权；
5. **删除违规示例**：严禁在任何双语提示词或说明中保留「Manager 可分身」「DevOps 可分身」等违背权限矩阵的文字或示例。
