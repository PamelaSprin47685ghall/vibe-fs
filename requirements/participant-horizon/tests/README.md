# participant-horizon 测试说明

本目录收录 `participant-horizon` 的回归测试。行为规范见 [WHAT.md](../WHAT.md)。

## 各测试覆盖什么

- 002 用源码泄漏检查器的正反 fixture 和当前仓库扫描。它覆盖选定路径及词形，不覆盖所有实际 Provider 输入；源码中的业务字段不能按名字一律当成机器 DTO。
- 003/005 走真实 JoinResultRenderer，断言返回、失败、取消、等待与终端观测的表示；解析 TOML 时区分外层控制字段与原始输出中碰巧出现的 `status` 等业务文本；结果顺序、工作记录内容与退出码均有直接断言。
- 009 走真实 Manager fork 工具：隐藏/未知 calling 得到相同拒绝、不创建 child；合法 Engineer 正例防止「一律拒绝」假通过。
- 010 走真实 horizon 观察固定 devops，并读取实际插件 Schema 检查 fork/commission 候选。
- 011 走真实创建、接收、取消与名册读取。另覆盖一条作废路径：重启后的进程把上一进程遗留、已无法交付的子工作作废（不是完成），名册显示 `voided`，join 报告这一次后果后该工作退役并离开名册。结算按孤儿幂等：每个孤儿一生只产生一条作废事实，重启的进程不得再次作废同一份孤儿工作，join 也不重复报告。
- 012 用生产热启动准入和注入的搜索 capability：Engineer/DevOps 有输出，其他角色在调用搜索前拒绝。它证明准入，不代表真实检索质量。
- 013 把形似指令/工具历史的搜索内容送进实际热启动路径，确认真正的任务与中英本地化警示仍为指令，并逐条精确比较 hint 原始值；覆盖无尾 LF、有尾 LF、多尾 LF、CRLF、裸 CR、引号、反斜杠、Unicode 与控制字符，不靠 trim 或包含判断掩盖编码变化。
- 015 只覆盖权限投影（Manager 无 Fission、Engineer 保留）；真实多 Engineer 名册仍是 TODO。

`voided` 是 `Abandoned(Voided)` 在名册上的呈现：该成员已无法交付、以作废结清而非完成。它与 `did-not-return` 同属 `Abandoned`，差别只在原因；两者都等 join 报告一次后果后才移出名册，报告之后该 handle 经 `summary` 投影读出 `Retired` 终态。

## 已知未覆盖

- 011 的名册断言走真实读取与最新记录选择；不再用执行耗时小于 50ms 之类的方式证明 pull-only。
- 取消后 Join 不结束的分阶段复现、过滤与跨界面隐藏、最新记录选择、虚假动作边界，尚无充分用例。
- 015 的真实多 Engineer 名册并发呈现仍是 TODO。

## 运行方式

```bash
node --test requirements/participant-horizon/tests/*.test.mjs
```
