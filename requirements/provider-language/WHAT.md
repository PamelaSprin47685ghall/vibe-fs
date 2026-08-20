# provider-language — WHAT

## PROVIDER-LANGUAGE-001: ProviderLanguage 是二元封闭类型

`ProviderLanguage` 是封闭的枚举类型（`English | SimplifiedChinese`）。语言是强类型而非任意字符串，Locale 资源文件名与目录结构由类型确定（`English → en.md`，`SimplifiedChinese → zh-CN.md`）。

## PROVIDER-LANGUAGE-002: 语言在会话创建时单次绑定且不可变

`SessionProviderLanguage` 在 Session 创建瞬间完成绑定，此后严格不可变。对同一会话重复绑定相同语言保持幂等，尝试绑定不同语言必须立即报错（fail-closed）。任何运行期事件（重试、故障转移、压缩、恢复）均严禁修改已绑定的语言。

## PROVIDER-LANGUAGE-003: 子会话严格继承父级语言而不重读全局偏好

所有派生会话（child / attached / internal execution）直接继承其 owner 绑定的语言。继承过程严禁重新读取全局环境配置，确保整个协作链条在同一自然语言环境中运行。

## PROVIDER-LANGUAGE-004: 全局偏好变更仅作用于未来新建会话

用户在运行期修改全局语言偏好，仅对变更后新建的根会话生效。已绑定语言的既有会话不受影响，确保历史上下文与前缀缓存的字节连续性。

## PROVIDER-LANGUAGE-005: 语言呈现分为 Class A、Class B 与 Class C

进入 participant horizon 的内容分为三类：
1. **Class A（Provider Prose）**：面向模型的自然语言文本（System Prompt、Role Law、工具描述、执行后果、Finality 文案），必须完整进行本地化。
2. **Class B（Technical Literals）**：机器与协议标识符（工具名、参数名、Wire 字段名、枚举字面量、代码路径、命令等），严格保持原样，永不翻译。
3. **Class C（Internal Diagnostics）**：内部诊断与日志，不进入模型感知范围，不参与 Provider 多语言体系。

## PROVIDER-LANGUAGE-006: 资源成对存在且缺失本地化时严格失败

所有 Provider 语义资源目录必须同时包含 `en.md` 与 `zh-CN.md`。已绑定语言的会话在请求缺失的本地化资源时必须立即失败，严禁静默回退到英文。

## PROVIDER-LANGUAGE-007: 模板占位符结构对称且填值不翻译

参数化文本模板中的 `{{name}}` 占位符集合在各语言版本间必须严格一致。运行时注入的具体参数值不进行二次翻译；模板中存在未替换的占位符时必须立即报错。

## PROVIDER-LANGUAGE-008: 同一参与者面对的工具文本与会话语言一致

参与者感知到的所有工具描述与调用契约，必须与其 `SessionProviderLanguage` 严格一致，严禁出现系统提示词与工具描述语言混杂的情况。

## PROVIDER-LANGUAGE-009: 散文文本三向所有权分离与集中装载

语义内容归属于各领域的 Semantic Owner，语言归属于 Session 绑定，渲染布局归属于通用机制。严禁在业务代码中使用 `match lang` 分支硬编码自然语言字面量；所有 Class A 文本统一经由 `ProviderResources` 加载。

## PROVIDER-LANGUAGE-010: Role Law 语义锚点跨语言成对命中

同一语义锚点标识（Semantic Anchor ID）必须在英文与中文版本的 Role Law 中同时命中，机械化保证不同语言版本表达完全等价的认知边界。

## PROVIDER-LANGUAGE-011: 协议标识符在所有语言中保持全局唯一不变

工具名称、参数名称、协议字段、枚举值等机器标识符在所有语言环境中保持不变。相同的标识符在任何语言下均严格指向完全一致的契约。
