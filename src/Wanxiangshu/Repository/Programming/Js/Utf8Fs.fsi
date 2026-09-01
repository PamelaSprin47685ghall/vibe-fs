namespace Wanxiangshu.Repository.Programming.Js

module JsUtf8Fs =
    val readUtf8: path: string -> Result<string, JsFailure>
    val readUtf8Classified: path: string -> Result<string, JsFailure>
