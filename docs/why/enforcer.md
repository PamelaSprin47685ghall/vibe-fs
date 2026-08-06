# Enforcer — 理由

规则曾由规范生成 F#：变更绑编译、多份清单漂移。规则是数据：打包 JSON + Domain 校验。拒绝 dist 双副本与代码内 fallback catalog——后者掩盖打包错误。

tip 取代 score-vector：把「评分并集」从控制流里挖掉，只保留 catalog 字段枚举与 cycle 原子提交。blog 立即返回，是避免工具路径变成隐藏的第二会话循环。
