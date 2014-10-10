﻿//----------------------------------------------------------------------------
//
// Copyright (c) 2013-2014 Ryan Riley (@panesofglass)
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
namespace Dyfrig

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Net
open System.Security.Principal
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Core

type OwinHeaders = IDictionary<string, string[]>

module StreamEx =
    let copyStream bufferSize =
        let buffer = Array.zeroCreate bufferSize
        let rec moveTo (outs: System.IO.Stream) (ins: System.IO.Stream) = async {
            let! bytes = ins.AsyncRead(buffer)
            if bytes > 0 then
                do! outs.AsyncWrite(buffer, 0, bytes)
                return! moveTo outs ins }
        moveTo

    type System.IO.Stream with
        member x.AsyncCopyTo (out: System.IO.Stream, ?bufferSize) = async {
            let bufferSize = defaultArg bufferSize 1024
            let copyTo = copyStream bufferSize
            return! copyTo out x }

open StreamEx

type Environment (env: OwinEnv) =
    inherit Dictionary<string, obj>(env)

    let mutable disposed = false

    static member Create (requestMethod: string, requestScheme: string, requestPathBase: string, requestPath: string, requestQueryString: string, requestProtocol: string, requestHeaders: OwinHeaders, ?requestBody, ?responseHeaders: OwinHeaders, ?responseBody, ?callCancelled) =
        let env = Dictionary<string, obj>(StringComparer.Ordinal)
        env.Add(Constants.requestMethod, requestMethod)
        env.Add(Constants.requestScheme, requestScheme)
        env.Add(Constants.requestPathBase, requestPathBase)
        env.Add(Constants.requestPath, requestPath)
        env.Add(Constants.requestQueryString, requestQueryString)
        env.Add(Constants.requestProtocol, requestProtocol)
        env.Add(Constants.requestHeaders, requestHeaders)
        env.Add(Constants.requestBody, defaultArg requestBody Stream.Null)
        env.Add(Constants.responseHeaders, defaultArg responseHeaders (new Dictionary<_,_>(HashIdentity.Structural) :> OwinHeaders))
        env.Add(Constants.responseBody, defaultArg responseBody (new MemoryStream() :> Stream))
        env.Add(Constants.callCancelled, defaultArg callCancelled (let cts = new CancellationTokenSource() in cts.Token))
        env.Add(Constants.owinVersion, "1.1")
        new Environment(env)

    static member inline Get<'a> (environment: OwinEnv, key: string) =
        if environment.ContainsKey(key) then
            Some(environment.[key] :?> 'a)
        else None

    member x.RequestMethod
        with get() : string = unbox x.[Constants.requestMethod]
        and set(v : string) = x.[Constants.requestMethod] <- v

    member x.RequestScheme
        with get() : string = unbox x.[Constants.requestScheme]
        and set(v : string) = x.[Constants.requestScheme] <- v

    member x.RequestPathBase
        with get() : string = unbox x.[Constants.requestPathBase]
        and set(v : string) = x.[Constants.requestPathBase] <- v

    member x.RequestPath
        with get() : string = unbox x.[Constants.requestPath]
        and set(v : string) = x.[Constants.requestPath] <- v

    member x.RequestQueryString
        with get() : string = unbox x.[Constants.requestQueryString]
        and set(v : string) = x.[Constants.requestQueryString] <- v

    member x.RequestProtocol
        with get() : string = unbox x.[Constants.requestProtocol]
        and set(v : string) = x.[Constants.requestProtocol] <- v
    
    member x.RequestHeaders = unbox<OwinHeaders> x.[Constants.requestHeaders]

    member x.GetBaseUri() =
        if x.RequestHeaders.ContainsKey("Host") then
            x.RequestScheme + "://" +
            (x.RequestHeaders.["Host"].[0]) +
            if String.IsNullOrEmpty x.RequestPathBase then "/" else x.RequestPathBase
            |> Some
        else None

    member x.GetRequestUri() =
        if x.RequestHeaders.ContainsKey("Host") then
            x.RequestScheme + "://" +
            (x.RequestHeaders.["Host"].[0]) +
            x.RequestPathBase +
            x.RequestPath +
            if String.IsNullOrEmpty x.RequestQueryString then "" else "?" + x.RequestQueryString
            |> Some
        else None
 
    member x.RequestBody = unbox<Stream> x.[Constants.requestBody]

    member x.RequestId
        with get() : string option =
            if x.ContainsKey(Constants.requestId) then
                Some (unbox x.[Constants.requestId])
            else None
        and set(v: string option) = x.[Constants.requestId] <- v

    member x.RequestUser
        with get() : IPrincipal =
            if x.ContainsKey(Constants.requestUser) then
                unbox x.[Constants.requestUser]
            else Unchecked.defaultof<_>
        and set(v: IPrincipal) = x.[Constants.requestUser] <- v

    member x.ResponseStatusCode
        with get() : int =
            if x.ContainsKey(Constants.responseStatusCode) then
                unbox x.[Constants.responseStatusCode]
            else 200 // Default for HTTP 200 OK
        and set(v : int) = x.[Constants.responseStatusCode] <- v

    member x.ResponseReasonPhrase
        with get() : string =
            if x.ContainsKey(Constants.responseReasonPhrase) then
                unbox x.[Constants.responseReasonPhrase]
            elif x.ContainsKey(Constants.responseStatusCode) then
                unbox x.[Constants.responseStatusCode] |> enum<HttpStatusCode> |> string
            else "OK" // Default for HTTP 200 OK
        and set(v : string) = x.[Constants.responseReasonPhrase] <- v

    member x.ResponseProtocol
        with get() : string option =
            if x.ContainsKey(Constants.responseProtocol) then
                Some(unbox x.[Constants.responseProtocol])
            else None
        and set(v : string option) =
            match v with
            | Some v -> x.[Constants.responseProtocol] <- v
            | None -> x.Remove(Constants.responseProtocol) |> ignore

    member x.ResponseHeaders = unbox<OwinHeaders> x.[Constants.responseHeaders]

    member x.ResponseBody = unbox<Stream> x.[Constants.responseBody]

    member x.CallCancelled
        with get() : CancellationToken = unbox x.[Constants.callCancelled]
        and set(v : CancellationToken) = x.[Constants.callCancelled] <- v

    member x.OwinVersion = unbox<string> x.[Constants.owinVersion]

    member x.With(key: string, value: #obj) =
        // Copy the current environment to the new environment.
        let env = new Environment(x)
        // Set the new value.
        env.[key] <- value
        env

    abstract Dispose : bool -> unit
    default x.Dispose(disposing) =
        if disposing then
            x.Values.OfType<IDisposable>()
            |> Seq.filter (fun x -> x <> Unchecked.defaultof<_>)
            |> Seq.iter (fun x -> try x.Dispose() with | _ -> ()) // TODO: Log any failed disposals.

    member x.Dispose() =
        if not disposed then
            GC.SuppressFinalize(x)
            x.Dispose(true)
            disposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Environment =
    [<CompiledName("ToEnvironment")>]
    let toEnvironment (environment: OwinEnv) =
        match environment with
        | :? Environment as e -> e
        | _ as d -> new Environment(d)

    [<CompiledName("Flush")>]
    let flush (oldEnv: OwinEnv) (newEnv: Environment) = async {
        // If the original environment is the same as the current reference, nothing further is necessary.
        if obj.ReferenceEquals(oldEnv, newEnv) then () else
        // Otherwise, copy the last dictionary back onto the original.
        for KeyValue(key, value) in newEnv do
            // Add keys not present in the original.
            if not (oldEnv.ContainsKey key) then
                oldEnv.Add(key, value)
            // Update existing keys if the value is different.
            // TODO: What elements might we not want to copy? Are all safe to copy?
            elif oldEnv.ContainsKey(key) && oldEnv.[key] <> value then
                match value with
                | :? System.IO.Stream as stream ->
                    let out = unbox<System.IO.Stream> oldEnv.[key]
                    // TODO: asynchronously copy to the out stream
                    do! stream.AsyncCopyTo(out)
                | :? OwinHeaders as headers ->
                    let oldEnvHeaders : OwinHeaders = unbox oldEnv.[key]
                    for KeyValue(k, v) in headers do
                        oldEnvHeaders.[k] <- v
                    // TODO: should remove any headers removed from the current instance
                | _ -> oldEnv.[key] <- value }
            // TODO: should remove any values not in the current instance
