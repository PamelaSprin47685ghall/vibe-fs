namespace Wanxiangshu.Host.SecondHost

open Wanxiangshu.Foundation

/// The product composition root for the second host.
/// The host supplies ports; the product supplies the hooks (HNI P-11).
module HostPlugin =

    /// Each activation owns one timer from the factory and disposes it with its hooks.
    val create: clock: IClockPort -> createTimer: (unit -> ITimerPort) -> Wanxiangshu.Host.Ports.IHostPlugin
