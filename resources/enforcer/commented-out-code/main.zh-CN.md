# commented-out-code — Main 中文版

## 现在该做什么
删除 commented-out implementation。若其中有独特 rationale，把 rationale 改写成真正的 comment；若未来真需要旧代码，从 version control 恢复。

## 为什么这很重要
旧实现留在眼前会获得虚假的现役感：搜索命中它、读者分析它、重构者担心它、自动工具甚至可能把它当语义素材。它已经不运行，却继续收取维护成本。

## 常见假修复
- 加 `OLD:` / `DO NOT USE:` 标记；尸体仍在。
- 把旧代码移到 `legacy-snippets` 文件。
- 认为“几行而已没关系”；问题是 truth channel 被污染，不是行数。

## 验证
repository search 不应再找到 abandoned implementation；必要历史仍可通过 git commit 找回。

## 完成条件
当前 source 只描述当前程序与必要 rationale，历史实现只存在于历史系统中。
