namespace Wanxiangshu.Foundation

open Fable.Core

/// Process-level invariant fuse shared by infrastructure layers that sit below
/// OpenCode's diagnostic adapter.
///
/// Business/provider failures must never use this surface. It is only for a
/// state Wanxiangshu itself declares internally inconsistent and therefore
/// unsafe to continue executing in the current process.
module FatalProcess =

    [<Emit("console.error(JSON.stringify({ operation: $0, result: $1 }))")>]
    let private report (operation: string) (result: string) : unit = jsNative

    /// Kill this process hard. Unit/canary harnesses suppress the physical exit
    /// so they can assert the fatal classification and durable aftermath.
    [<Emit("""(() => {
      if (process.env.WANXIANGSHU_NO_FATAL_EXIT === '1') return;
      if (process.env.NODE_TEST_CONTEXT != null && process.env.NODE_TEST_CONTEXT !== '') return;
      try { process.kill(process.pid, 'SIGKILL'); } catch (_) { process.exit(1); }
    })()""")>]
    let kill () : unit = jsNative

    let trip (operation: string) (result: string) : unit =
        report operation result
        kill ()
