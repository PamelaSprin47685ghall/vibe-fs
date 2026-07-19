# 收尾工作清单

## 一、Fallback 状态收口

仍待继续：

* session 的全部权威状态只能存在于一个 aggregate；
* 删除剩余 `*Transitions.fs` 薄包装，让调用方直接调用 `SessionRuntime*Pure` 的领域操作，确保调用方不能自由组合多个 setter 来维护不变量；
* continuation、nudge、compaction 必须共享统一的 episode 身份与迟到事件规则。

验收时不再接受“这个字段比较特殊，所以单独保存”。

---

## 二、拆除兼容门面

下面这些文件仍然承担“旧名字转发到新实现”的职责：

* `Hosts/OpenCode/HookExecute.fs`
* `Runtime/Fallback/FallbackConfigCodec.fs`
* `Runtime/Execution/BacklogProjectionBuild.fs`
* `Hosts/OpenCode/SubsessionHostAdapter.fs`
* `Runtime/Messaging/OpencodeSessionEventCodec.fs`
* `Runtime/Search/FuzzySearch.fs`
* `Hosts/Omp/SessionLifecycleHooks.fs`
* `Runtime/EventStore/EventLogRuntime.fs`

迁移顺序必须是：生产调用方 → 测试调用方 → facade → 旧模块文件。

不要因为“现有测试还在调用”而保留兼容层。测试应该服从架构，不应该反过来决定生产 API。

---

## 三、`tempFilesByPrompt` 完整生命周期

必须确认：

* prompt 结束时删除；
* session abort 时删除；
* session close 时删除；
* workspace dispose 时删除；
* 异常和超时路径也删除。

不能只覆盖“正常完成”路径。

---

## 四、历史补丁注释肃清

源码中仍能看到大量类似：

* `S-07 fix`
* `F-03`
* `N-01`
* `N-02`
* `R-01`
* `R-03`
* `TASK §5`
* `PRD-06`
* `Phase 7`
* `Phase 8`
* `REF.md`
* “until ... lands”
* “best effort for now”

应删除：谁修过哪个 ticket、这是第几阶段、过去曾经怎么实现、“暂时先这样”、“以后某模块落地后再改”。

历史信息应该进入 commit / issue / ADR / regression test 名称。最终源码应像“一次写成”。

---

## 五、文件拆分反向技术债

当前有大量 20～40 行文件，且若干目录极度扁平：`Hosts/OpenCode`、`Hosts/Omp`、`Kernel` 根目录、`Runtime/Tooling`、`Runtime/Messaging`、`Runtime/Subsession`、`Runtime/Fallback`。

需要合并：只有一个私有 helper 且只被同目录一个模块调用；只有别名或 re-export；只有一个 architecture-test probe；只有一个薄包装；文件名必须结合相邻文件才能理解；拆开后产生循环 `open`；没有自己的测试、不变量或生命周期。

可以保持独立：稳定领域类型；明确的 port/interface；纯状态机 transition；独立 wire codec；安全策略；可单独测试的算法；必须控制编译依赖方向的 F# 类型文件。

---

## 六、最终验收清单

* [ ] 不存在空 `.fs` 模块。
* [ ] 无未引用生产源文件。
* [ ] `.fsproj` 的编译顺序反映真实依赖，而不是历史迁移顺序。
* [ ] OpenCode 全链路 E2E 通过后，再分别验证 OMP 和 Mux。
* [ ] 最终目录和文件名不依赖阅读重构历史才能理解。

**最优先顺序不是整理文件名，而是：裁决 Continuation 双架构，统一 Backlog，再拆兼容层。** 这三件事完成以后，才可以说旧架构已经从运行路径中真正肃清，而不只是从文件名上消失。
