# commented-out-code — Lesson

## 评价

### 定义

注释掉的实现，是把版本控制应该保存的过去，塞回正在描述现在的 source。它让同一视觉通道同时承载“当前程序”与“历史残骸”。

### 支配原则

Source 应该描述实际存在的程序；版本控制描述的是曾经存在的程序。把这两种时间角色混在一起，每个读者都得先给代码分类、才能理解它：可执行的真相与历史残骸占据同一条视觉通道。仓库于是不再是关于当前系统的精确陈述。

### 何时触发

- 整个旧函数/branch/import 被 block comment 留着“以后也许用”；
- live implementation 旁边保留 previous version；
- 删除代码前先注释，之后永久留在仓库；
- reader 必须判断某段像代码的文本究竟是 explanation 还是 abandoned implementation。

### 不应触发

- doc comment 中的短 protocol example；
- 外部 spec 片段；
- TODO 只描述缺口、不嵌旧实现；
- 真正 feature-flagged code 仍可执行且有 activation contract，虽可能有别的问题。

### 与相邻规则区分

`dead-code-delivered` 是仍可编译、却不可达或未被使用的代码；`comment-theater` 是叙述显而易见行为的评论。本规则管的是以 comment 形式保存的旧实现。Tie-break：如果删掉这段 comment 去掉的是旧代码而不是知识，这个案子归本规则。

### 判定程序

删除这段 comment：当前程序会变吗？会丢失只有它保存的必要 rationale 吗？如果都不会，git history 才是它的 owner。

### 例子

- positive：旧 handler 被 block comment 留着“以防万一”，紧挨着 live replacement。
- near-miss：doc comment 里的一行 protocol example，它不是旧实现。
- counterexample：删掉被注释的实现；将来万一需要，从 version control 恢复。

## 建议

### 现在该做什么

删除 commented-out implementation。

### 为什么重要

旧实现留在眼前会获得虚假的现役感：搜索命中它、读者分析它、重构者担心它、自动工具甚至可能把它当语义素材。它已经不运行，却继续收取维护成本。

### 修复策略

删除时若发现其中有独特 rationale，把 rationale 改写成真正的 comment。

### 决策分支

若未来真需要旧代码，从 version control 恢复。

### 常见假修复

- 加 `OLD:` / `DO NOT USE:` 标记；尸体仍在。
- 把旧代码移到 `legacy-snippets` 文件。
- 认为“几行而已没关系”；问题是 truth channel 被污染，不是行数。

### 验证

repository search 不应再找到 abandoned implementation；必要历史仍可通过 git commit 找回。

### 完成条件

当前 source 只描述当前程序与必要 rationale，历史实现只存在于历史系统中。

### Nudge

Working tree 应该对“系统现在是什么”说真话。过去已经有更完整、更可追溯的 archive，不需要把尸体摆在活代码旁边。
