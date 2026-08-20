# provider-language — HOW

## 架构与实现机制

1. **类型绑定与继承模型**：
   - `ProviderLanguage`（`English | SimplifiedChinese`）定义支持的语言集合。
   - `ProviderLanguageBinding` 负责会话语言的绑定与继承：根会话首触达时读取全局配置并执行 `bindOnce`；子会话通过 `ensureInherited` 直接复制父级语言，拒绝二次读取全局环境。

2. **资源加载管线（Class A 入口）**：
   - 领域文本通过 `ProviderResources.readText(lang, semanticPath)` 唯一定位加载。
   - `ProviderProse` 提供模板占位符校验与安全替换（`substitute`），确保无残留占位符并移交投影层。
   - 加载缺失任一语言文件时直接抛出异常，杜绝静默回退。

3. **结构对称性与防退化门禁**：
   - `language-parity-gate` 检查资源文件成对存在、占位符集合一致、标识符不翻译以及语义锚点双语覆盖。
   - `provider-prose-ownership` 扫描源码，禁止在业务逻辑中硬编码散落的自然语言字面量。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PROVIDER-LANGUAGE-001 | `requirements/provider-language/tests/provider-language.test.mjs` |
| PROVIDER-LANGUAGE-002 | `requirements/provider-language/tests/provider-language.test.mjs` |
| PROVIDER-LANGUAGE-003 | `requirements/provider-language/tests/provider-language.test.mjs` |
| PROVIDER-LANGUAGE-004 | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs` |
| PROVIDER-LANGUAGE-005 | `requirements/provider-language/tests/provider-prose-ownership.test.mjs` |
| PROVIDER-LANGUAGE-006 | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs` |
| PROVIDER-LANGUAGE-007 | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs` |
| PROVIDER-LANGUAGE-008 | `requirements/provider-language/tests/provider-language.test.mjs` |
| PROVIDER-LANGUAGE-009 | `requirements/provider-language/tests/provider-prose-ownership.test.mjs` |
| PROVIDER-LANGUAGE-010 | `requirements/provider-language/tests/language-parity-gate.test.mjs` |
| PROVIDER-LANGUAGE-011 | `requirements/provider-language/tests/language-parity-gate.test.mjs` |
