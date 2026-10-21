namespace Wanxiangshu.Host.SecondHost

/// The product composition root for the second host.
/// The host supplies ports; the product supplies the hooks (HNI P-11).
module HostPlugin =

    /// The activated product for the second host.
    val plugin: Wanxiangshu.Host.Ports.IHostPlugin
