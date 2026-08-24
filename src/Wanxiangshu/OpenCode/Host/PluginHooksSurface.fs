namespace Wanxiangshu.OpenCode

/// JS-native entry for the Host fatal hook membrane.
/// The returned function is an opaque callable capability; its arguments and
/// returned Promise/value remain Host-owned data.
module PluginHooksSurface =
    let fatalHook operation (adaptedHook: obj) : obj =
        PluginHostInterop.fatalHook operation adaptedHook

    let classifiedRejectionHook operation isExpected (adaptedHook: obj) : obj =
        PluginHostInterop.classifiedRejectionHook operation isExpected adaptedHook
