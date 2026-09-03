namespace Wanxiangshu.Sphinx

open Wanxiangshu.Sphinx.Core
open Wanxiangshu.Sphinx.Runtime

module GecDecode =
    val decodeManifest: raw: obj -> Result<PluginManifest, CoreError>
    val decodeLockEntries: raw: obj -> Result<PluginLockEntry list, CoreError>
    val decodeEventAt: state: InquiryState option -> raw: obj -> position: int -> Result<InquiryEvent, CoreError>
