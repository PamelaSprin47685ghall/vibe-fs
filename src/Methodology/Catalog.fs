module Wanxiangshu.Methodology.Catalog

open Wanxiangshu.Methodology.SchemaCommon

let all: Lazy<MethodologyEntry list> =
    lazy (Catalog1.entries @ Catalog2.entries @ Catalog3.entries @ Catalog4.entries)
