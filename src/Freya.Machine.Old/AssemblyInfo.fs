﻿namespace System
open System.Reflection

[<assembly: AssemblyDescriptionAttribute("Freya")>]
[<assembly: AssemblyFileVersionAttribute("1.0.0")>]
[<assembly: AssemblyProductAttribute("Freya.Machine")>]
[<assembly: AssemblyTitleAttribute("Freya.Machine")>]
[<assembly: AssemblyVersionAttribute("1.0.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.0"
