namespace Wanxiangshu.Resources

/// Packaged recommended `wanxiangshu.mjs`. Host bootstrap may copy these bytes
/// into `~/.config/opencode/wanxiangshu.mjs` when that file is absent; it must
/// not read package resources itself (DISTRIBUTION-006).
module ModelRoutingResource =

    let recommendedTemplate () : string =
        PackageResources.readText "wanxiangshu.mjs"
