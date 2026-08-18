相对当前 `workingOn` 执行前沿的规划分辨率，不是 status、priority、phase、ETA 或经过时间。`near` 表示一个可直接行动、可独立闭环的小单元；`mid` 表示下一层有意义结果/依赖，内部步骤暂时折叠；`far` 表示粗粒度 outcome coverage，内部 decomposition 延后。重要的是完整覆盖，不是均匀细节；只有工作接近前沿时才继续细化。
