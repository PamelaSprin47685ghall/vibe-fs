# Projection — 边界

## PROJ-004：三层结构

实现必须分为：

```text
Effectful Coordinator
    读取 Host、等待结果、生成不可变快照

Pure Projection Planner
    汇总各功能 ProjectionIntent、排序、冲突检查

Canonical Renderer
    渲染最终 provider wire bytes、生成 digest/seal
```

## PROJ-005：ProjectionIntent

功能模块只能声明以下形态的 intent：

```text
keepPhysicalPrefix
activatePrefixEpoch
insertBlogFrames
insertRepair
suppressTransportOnly
appendReviewChallenge
reanchorAfterCompaction
```

禁止任何业务功能直接接收和修改 `Message list`。

## PROJ-006：合并与冲突

不同 intent 修改同一锚点时：

* 有明确定义的合并律；或
* 返回 `ProjectionConflict`；
* 不允许依赖注册顺序。
