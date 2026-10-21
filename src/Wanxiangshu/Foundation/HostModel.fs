namespace Wanxiangshu.Foundation.HostSession

/// Provider/model selection carried by host session ports (moved out of the OpenCode adapter, CUT-34).
type OpencodeModel =
    { providerID: string
      modelID: string
      variant: string option }
