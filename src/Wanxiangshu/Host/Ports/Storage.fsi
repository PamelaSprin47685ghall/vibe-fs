namespace Wanxiangshu.Host.Ports

/// HNI P-10: the only persistence touchpoint a host supplies. Product journals, ledgers and
/// leases live under StateRoot and are owned by the product, never stored in host storage.
type IHostPaths =
    abstract StateRoot: WorkspacePath
    abstract ProjectRoot: WorkspacePath
    abstract ProjectId: string
