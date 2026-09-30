# action-affordance 测试范围

本包测试调用界面的可理解契约。非空、名称和参数检查只是局部证据；不能用关键词凑成五问完整性或语义等价证明。

- 001 读取真实插件注册结果，检查描述非空及 assume 参数有描述。完整五问仍需语义审阅和实际选择场景。
- 007 核对静态工具目录不复用活跃身份名称；不据此宣称所有名称都是动词。
- 008 用同一生产检查器接受不同名称、拒绝两个被识别所有者复用同名。该检查只覆盖它可提取的所有者，不证明全系统 Schema、后果或生命周期等价。
- 009 先让实际 commission Schema 接受 lead、拒绝 coordinator，再检查中英资源不继续宣传旧的 Coordinator / Lead 选择。此回归已先红；不是所有措辞语义正确的通用判别器。
- 014 验证 assume 只有 `assumption`、成功不回显输入且不维护 canvas；todowrite 的 provider schema 把列表发布为必填 `obligations`（行 schema 不变）并新增必填 `retainCheckpoints`，before 把 `obligations` 换名为 `todos`、隐藏 `retainCheckpoints`，after 反过来恢复。插件 fixture 使用临时仓库与隔离配置，不调用外部模型。
- 原 003—005 的废止工具名称名单和 Role Law 词句不能证明调用描述完整；原 011/012/009 自建 mutant-only 正则也不是实际门禁。这些证明撤下，义务保留为 TODO。双语资源存在性由 provider-language 的实际门禁承担，语义对等仍需审阅。

通过正式构建后，选择本包、requirement-system、迁移来源 capability-enforcement 010 和受影响的 provider-language 检查运行。需启用 integration 才执行真实插件用例；未启用属于跳过。TODO 使正式入口返回非零，不宣称整包已验收。

待决见[迁移记录](../../../proposals/20模块迁移-环境与权限-2026-09-28.md)，覆盖缺口见 GAP-078。README 不新增动作权限、命名规则或固定描述格式。
