﻿namespace Freya.Router

open Aether
open Aether.Operators
open Freya.Core
open Freya.Core.Operators
open Freya.Pipeline
open Freya.Typed




[<RequireQualifiedAccess>]
module Route =

    let private boxIso<'a> : Iso<obj, 'a> =
        unbox<'a>, box
        
    let internal Values =
        dictLens "dyfrig.routing.values" <--> boxIso<Map<string, string>>

    let Value key = 
        Values >-?> mapPLens key


[<AutoOpen>]
module internal Logic =

    type internal Trie =
        { Root: Node }

        static member empty =
            { Root =
                { Children = List.empty
                  Key = ""
                  Pipeline = None
                  Recognizer = Ignore "" } }

    and internal Node =
        { Children: Node list
          Key: string
          Pipeline: FreyaPipeline option
          Recognizer: Recognizer }    

    and internal Recognizer =
        | Ignore of string
        | Capture of string

    and internal Registration =
        | Registration of string list * FreyaPipeline
         
    let childrenLens =
        (fun x -> x.Children), 
        (fun c x -> { x with Children = c })

    let pipelinePLens =
        (fun x -> x.Pipeline), 
        (fun p x -> { x with Node.Pipeline = Some p })

    let rootLens =
        (fun x -> x.Root), 
        (fun r x -> { x with Root = r })


    [<AutoOpen>]
    module Paths =

        let path (p: string) =
            p.Split '/'
            |> List.ofArray
            |> List.filter ((<>) "")


    [<AutoOpen>]
    module Construction =

        let private registration p app =
            Registration (path p, app)

        let private recognizer (key: string) =
            match key.[0] with
            | ':' -> Capture (key.Substring (1))
            | _ -> Ignore (key)

        let private node key =
            { Children = List.empty
              Key = key
              Pipeline = None
              Recognizer = recognizer key }

        let private add p pipeline =
            let rec add registration root =
                match registration with
                | Registration (h :: t, app) ->
                    match List.tryFindIndex (fun x -> x.Key = h) root.Children with
                    | Some i -> 
                        modPL (childrenLens >-?> listPLens i) (add (Registration (t, app))) root
                    | _ -> 
                        modL childrenLens (fun x -> x @ [ add (Registration (t, app)) (node h) ]) root
                | Registration (_, pipeline) ->
                    setPL pipelinePLens pipeline root

            modL rootLens (add (registration p pipeline))

        let construct (routes: FreyaRoute list) =
            List.fold (fun x route -> add route.Path route.Pipeline x) Trie.empty routes


[<AutoOpen>]
module internal Routing =

    let private recognize r data value =
        match r with
        | Capture x -> true, Map.add x value data
        | Ignore x when x = value -> true, data
        | _ -> false, data

    let search p trie =
        let rec traverse data path root =
            match path with
            | h :: t -> 
                root.Children
                |> List.tryPick (fun x ->
                      match recognize x.Recognizer data h with
                      | true, map -> Some (x, map)
                      | _ -> None)
                |> fun x ->
                    match x with
                    | Some (child, map) -> traverse map t child
                    | _ -> None
            | _ -> 
                root.Pipeline |> Option.map (fun x -> x, data)

        traverse Map.empty (path p) trie.Root


[<AutoOpen>]
module Reification =

    let compileFreyaRouter (m: FreyaRouter) : FreyaPipeline =
        let trie = 
            m List.empty 
            |> snd
            |> construct

        freya {
            let! path = getLM Request.path

            match search path trie with
            | Some (app, data) -> return! setLM Route.Values data *> app
            | _ -> return Next }
