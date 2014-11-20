﻿[<AutoOpen>]
module Freya.Inspector.Interface

open System.Text
open Aether
open Freya.Core
open Freya.Core.Operators
open Freya.Pipeline
open Freya.Typed

(* Types *)

type FreyaInspector =
    { Render: Map<string, obj> -> string option }

type internal DisplayConfiguration =
    { Inspectors: FreyaInspector list }

(* Renderers *)

let private renderHeader log (b: StringBuilder) =
    b.AppendFormat ("ID: {0}\nTimestamp: {1}\n", log.Id, log.Timestamp)

let private renderDataItem inspector data (b: StringBuilder) =
    match inspector.Render data with
    | Some s -> b.AppendFormat ("{0}\n", s)
    | _ -> b

let private renderData config log b =
    List.fold (fun b i -> renderDataItem i log.Data b) b config.Inspectors

let private renderLog config log =
    StringBuilder ()
    |> renderHeader log
    |> renderData config log
    |> string

let private renderLogs config logs =
    logs
    |> Seq.map (renderLog config)
    |> String.concat "\n"

let private render config logs =
    freya {
        let body = Encoding.UTF8.GetBytes (renderLogs config logs)
        let length = Array.length body

        do! setPLM Response.Headers.contentLength (ContentLength length)
        do! setPLM Response.Headers.contentType (ContentType (MediaTypes.Text))
        do! modLM Response.body (fun b -> b.Write (body, 0, Array.length body); b) }

(* Pipeline *)

let internal display (storage: Storage) config : FreyaPipeline =
    render config (storage.PostAndReply (fun c -> ReadAll (c))) *> halt
