namespace Wanxiangshu.Resources

open Wanxiangshu.Participant.Provider

module RuntimeResourceAssembly =
    val loadFor: lang: ProviderLanguage -> RuntimeResources
    val load: unit -> RuntimeResources
