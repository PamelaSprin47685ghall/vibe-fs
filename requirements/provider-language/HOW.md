# provider-language — HOW

## 架构与实现机制

1. **类型绑定与继承模型**：
   - owner-local `ProviderLanguage`（`English | SimplifiedChinese`）定义语言值与解析策略，`SessionProviderLanguage` 负责会话的 bind-once、继承与查询策略。
   - raw-observation Host `ProviderLanguageBinding` 仅把主机会话文本或全局偏好转换为 owner 策略调用：根会话通过 `ensureRoot` 绑定，子会话通过 `ensureInherited` 复制父级语言，拒绝二次读取全局环境。

2. **资源加载管线（Class A 入口）**：
   - owner-local `ProviderResources` 通过 `relativePath`、`requireLanguagePair` 与 `readText` 唯一定位和读取领域文本；`ProviderResourceBytes` 是窄物理适配器，仅把 owner 已批准的相对路径交给 `PackageResources`。
   - owner-local `ProviderProse` 提供模板渲染与安全替换（`render`、`substitute`），确保无残留占位符并移交投影层。
   - 加载缺失任一语言文件时直接抛出异常，杜绝静默回退。

3. **结构对称性与防退化门禁**：
   - `language-parity-gate` 检查资源文件成对存在、占位符集合一致、标识符不翻译以及语义锚点双语覆盖。
