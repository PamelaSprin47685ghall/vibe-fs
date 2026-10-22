namespace Wanxiangshu.Foundation.HostSession

/// Provider/model selection carried by host session ports (moved out of the OpenCode adapter, CUT-34).
type HostModel =
    { providerID: string
      modelID: string
      variant: string option }

/// Reflection alias used by emitted OpenCode contract surfaces.
type OpencodeModel =
    { providerID: string
      modelID: string
      variant: string option }
