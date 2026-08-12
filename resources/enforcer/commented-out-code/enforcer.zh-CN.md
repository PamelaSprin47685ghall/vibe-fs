# commented-out-code — Enforcer 中文版

## 定义
注释掉的实现，是把版本控制应该保存的过去，塞回正在描述现在的 source。它让同一视觉通道同时承载“当前程序”与“历史残骸”。

## 何时触发
- 整个旧函数/branch/import 被 block comment 留着“以后也许用”；
- live implementation 旁边保留 previous version；
- 删除代码前先注释，之后永久留在仓库；
- reader 必须判断某段像代码的文本究竟是 explanation 还是 abandoned implementation。

## 不要误判
- doc comment 中的短 protocol example；
- 外部 spec 片段；
- TODO 只描述缺口、不嵌旧实现；
- 真正 feature-flagged code 仍可执行且有 activation contract，虽可能有别的问题。

## 刀口
删除这段 comment：当前程序会变吗？会丢失只有它保存的必要 rationale 吗？如果都不会，git history 才是它的 owner。

## 提醒
Working tree 应该对“系统现在是什么”说真话。过去已经有更完整、更可追溯的 archive，不需要把尸体摆在活代码旁边。
