# provider-language 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含语言类型、偏好解析、资源结构与双语呈现各环节的测试。

## 测试覆盖重点

- **001**：验证内部封闭语言枚举（`English | SimplifiedChinese`）与资源文件映射，验证非法语言输入的拒绝。
- **002—004**：验证无会话语言绑定、派生会话共享全局设置，以及环境变量、宿主配置、本地环境与回退 English 的阶梯解析顺序。
- **005/013**：验证 Class A 提示词转换与中途语言切换后的呈现更新，确保外部系统提示段落字节保持不变。
- **006**：验证 Provider 资源成对存在性检查，确保缺少任一语言资源或请求缺失路径时立即报错。
- **007/011**：验证双语模板参数占位符一致性及协议标识符不翻译策略。
- **008/009**：验证工具描述 Class A 文本的多语言呈现与资源统一装载归属。
- **010/012**：验证 Role Law 双语锚点对应关系及职责语义一致性检查。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/provider-language/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
