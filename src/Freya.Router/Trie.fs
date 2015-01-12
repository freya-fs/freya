﻿//----------------------------------------------------------------------------
//
// Copyright (c) 2014
//
//    Ryan Riley (@panesofglass) and Andrew Cherry (@kolektiv)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------

[<AutoOpen>]
module internal Freya.Router.Trie

open Aether
open Aether.Operators
open Freya.Pipeline

(* Trie *)

type internal FreyaRouterTrie =
    { Children: FreyaRouterTrie list
      Key: string
      Pipelines: (FreyaRouteMethod * FreyaPipeline) list
      Recognizer: RouterRecognizer }

    static member ChildrenLens =
        (fun x -> x.Children), 
        (fun c x -> { x with Children = c })

    static member PipelinesLens =
        (fun x -> x.Pipelines), 
        (fun p x -> { x with Pipelines = p })

and internal RouterRecognizer =
    | Ignore of string
    | Capture of string

(* Constructors *)

let private recognizer (key: string) =
    match key.StartsWith ":" with
    | true -> Capture (key.Substring (1))
    | _ -> Ignore (key)

let private node key =
    { Children = List.empty
      Key = key
      Pipelines = List.empty
      Recognizer = recognizer key }

let segmentize (path: string) =
    path.Split '/'
    |> List.ofArray
    |> List.filter ((<>) "")

(* Lenses *)

let private childPLens i =
    FreyaRouterTrie.ChildrenLens >-?> listPLens i

(* Constructors *)

let rec private add node =
    function | (segment :: path, pipeline, meth) -> 
                (find segment node |> update segment path pipeline meth) node
             | (_, pipeline, meth) -> 
                modL FreyaRouterTrie.PipelinesLens (fun ps -> ps @ [ (meth, pipeline) ]) node

and private find segment =
    function | { Children = x } -> List.tryFindIndex (fun x -> x.Key = segment) x

and private update segment path pipeline meth =
    function | Some i -> extend i path pipeline meth
             | _ -> append segment path pipeline meth

and private extend i path pipeline meth =
    modPL (childPLens i) (flip add (path, pipeline, meth))

and private append segment path pipeline meth =
    modL FreyaRouterTrie.ChildrenLens (flip (@) [ add (node segment) (path, pipeline, meth) ])

let private addRoute route =
    (flip add) (segmentize route.Path, route.Pipeline, route.Method)

let routerTrie =
    List.fold (flip addRoute) (node "")