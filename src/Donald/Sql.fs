﻿module Donald 

open System
open System.Data

type DbConnectionFactory = unit -> IDbConnection

[<Struct>]
type DbParam = 
    { 
        Name : string
        Value : obj 
    }

let createConn (createConnection : DbConnectionFactory) =
    createConnection ()    
    
let beginTran (conn : IDbConnection) = 
    //if conn.State <> ConnectionState.Open then conn.Open()
    conn.BeginTransaction()

let rollbackTran (tran : IDbTransaction) =
    try        
        if not(isNull tran) 
           && not(isNull tran.Connection) then tran.Rollback()
    with            
        | _ -> 
            reraise() 

let commitTran (tran : IDbTransaction) =
    tran.Commit()
    //try
    //    if not(isNull tran) 
    //       && not(isNull tran.Connection) then tran.Commit() 
    //with
    //    // Is supposed to throw System.InvalidOperationException
    //    // when commmited or rolled back already, but most
    //    // implementations do not. So in all cases try rolling 
    //    // backing in all failure cases
    //    | _ -> 
    //        rollbackTran tran
    //        reraise()
    
let newCommand sql param (tran : IDbTransaction) =
    let cmd = tran.Connection.CreateCommand()
    cmd.CommandType <- CommandType.Text
    cmd.CommandText <- sql
    cmd.Transaction <- tran

    let createParam param = 
        let p = cmd.CreateParameter()
        p.ParameterName <- param.Name
        p.Value <- param.Value
        cmd.Parameters.Add(p) |> ignore
       
    param |> Seq.iter createParam

    cmd

let newCommandSimple sql param (conn:IDbConnection) =
    let cmd = conn.CreateCommand()
    cmd.CommandType <- CommandType.Text
    cmd.CommandText <- sql

    let createParam param = 
        let p = cmd.CreateParameter()
        p.ParameterName <- param.Name
        p.Value <- param.Value
        cmd.Parameters.Add(p) |> ignore
       
    param |> Seq.iter createParam

    cmd

let newParam name value =
    { Name = name; Value = value }

let tranQuerySimple sql param (map:System.Func<IDataReader, 'a>) conn =
  seq{
    use cmd = newCommandSimple sql param conn
    
    use rd = cmd.ExecuteReader()
    // https://github.com/dotnet/fsharp/issues/8897
    // workaround array or list
    while rd.Read() do
                yield map.Invoke rd }

let tranQuery sql param (map:System.Func<IDataReader, 'a>) (tran : IDbTransaction) =
  seq{
    //use cmd = newCommand sql param tran
    
    use cmd = tran.Connection.CreateCommand()
    cmd.CommandType <- CommandType.Text
    cmd.CommandText <- sql
    cmd.Transaction <- tran

    let createParam param = 
        let p = cmd.CreateParameter()
        p.ParameterName <- param.Name
        p.Value <- param.Value
        cmd.Parameters.Add(p) |> ignore
       
    param |> Seq.iter createParam

    use rd = cmd.ExecuteReader()
    // https://github.com/dotnet/fsharp/issues/8897
    // workaround array or list
    while rd.Read() do
                yield map.Invoke rd }

let tranQuerySingle sql param map tran =
    use cmd = newCommand sql param tran
    use rd = cmd.ExecuteReader()
    if rd.Read() then Some(map rd) else None

let tranExec sql param tran =
    use cmd = newCommand sql param tran    
    cmd.ExecuteNonQuery() |> ignore

let tranScalar sql param map tran =
    use cmd = newCommand sql param tran
    map (cmd.ExecuteScalar())

let queryIssue8897 sql param (map:System.Func<IDataReader, 'a>) (conn : IDbConnection) =
    //let results = //tranQuery sql param map tran
        seq {
        
        use tran = conn.BeginTransaction()
        use cmd = tran.Connection.CreateCommand()
        cmd.CommandType <- CommandType.Text
        cmd.CommandText <- sql
        cmd.Transaction <- tran

        let createParam param = 
            let p = cmd.CreateParameter()
            p.ParameterName <- param.Name
            p.Value <- param.Value
            cmd.Parameters.Add(p) |> ignore

        param |> Seq.iter createParam

        use rd = cmd.ExecuteReader()
        // https://github.com/dotnet/fsharp/issues/8897
        // workaround array or list
        yield! 
            seq {
                while rd.Read() do
                        yield map.Invoke rd }
        tran.Commit()            
        }

let query sql param map conn =
    seq {
    use tran = beginTran conn
    yield! tranQuery sql param map tran
//Notice that however try/with can't be used with sequence, 
//so the issue is closed without a perfect solution 
//(a true transactional and lazy wrapper can't be really written in pure functional style), 
//but only on the bona fide assumption that you wouldn't fix it anyway.
    commitTran tran
    }
    
    //results // see workaround  |> Array.toSeq
    // https://github.com/dotnet/fsharp/issues/8897

let querySimple sql param map conn =
    let results = tranQuerySimple sql param map conn
    results // see workaround  |> Array.toSeq
    // https://github.com/dotnet/fsharp/issues/8897

let querySingle sql param map conn =
    use tran = beginTran conn
    let result = tranQuerySingle sql param map tran
    commitTran tran
    result

let exec sql param conn =
    use tran = beginTran conn
    tranExec sql param tran
    commitTran tran

let scalar sql param convert conn =
    use tran = beginTran conn
    let v = tranScalar sql param convert tran
    commitTran tran
    v

type IDataReader with
    member this.GetBoolean(name)  = this.GetBoolean(this.GetOrdinal(name))

    member this.GetByte(name)     = this.GetByte(this.GetOrdinal(name))

    member this.GetChar(name)     = this.GetChar(this.GetOrdinal(name))

    member this.GetDateTime(name) = this.GetDateTime(this.GetOrdinal(name))

    member this.GetDecimal(name)  = this.GetDecimal(this.GetOrdinal(name))

    member this.GetDouble(name)   = this.GetDouble(this.GetOrdinal(name))

    member this.GetFloat(name)    = this.GetFloat(this.GetOrdinal(name))

    member this.GetGuid(name)     = this.GetGuid(this.GetOrdinal(name))

    member this.GetInt16(name)    = this.GetInt16(this.GetOrdinal(name))

    member this.GetInt32(name)    = this.GetInt32(this.GetOrdinal(name))

    member this.GetInt64(name)    = this.GetInt64(this.GetOrdinal(name))

    member this.GetString(name)   = this.GetString(this.GetOrdinal(name))
    
    member this.GetBooleanOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetBoolean(i))
  
    member this.GetByteOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetByte(i))
  
    member this.GetCharOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetChar(i))
  
    member this.GetDateTimeOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetDateTime(i))
  
    member this.GetDecimalOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetDecimal(i))
  
    member this.GetDoubleOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetDouble(i))
  
    member this.GetFloatOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetFloat(i))
  
    member this.GetGuidOption(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetGuid(i))
  
    member this.GetInt16Option(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetInt16(i))
  
    member this.GetInt32Option(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetInt32(i))
  
    member this.GetInt64Option(name) =
        let i = this.GetOrdinal(name)
        match this.IsDBNull(i) with 
        | true  -> None
        | false -> Some(this.GetInt64(i))
  
    member this.GetNullableBoolean(name) = 
        match this.GetBooleanOption(name) with 
        | None   -> Nullable<Boolean>() 
        | Some v -> Nullable<Boolean>(v)
  
    member this.GetNullableByte(name) = 
        match this.GetByteOption(name) with 
        | None   -> Nullable<Byte>() 
        | Some v -> Nullable<Byte>(v)
  
    member this.GetNullableChar(name) = 
        match this.GetCharOption(name) with 
        | None   -> Nullable<Char>() 
        | Some v -> Nullable<Char>(v)
  
    member this.GetNullableDateTime(name) = 
        match this.GetDateTimeOption(name) with 
        | None   -> Nullable<DateTime>() 
        | Some v -> Nullable<DateTime>(v)
  
    member this.GetNullableDecimal(name) = 
        match this.GetDecimalOption(name) with 
        | None   -> Nullable<Decimal>() 
        | Some v -> Nullable<Decimal>(v)
  
    member this.GetNullableDouble(name) = 
        match this.GetDoubleOption(name) with 
        | None   -> Nullable<Double>() 
        | Some v -> Nullable<Double>(v)
  
    member this.GetNullableFloat(name) = 
        match this.GetFloatOption(name) with 
        | None   -> Nullable<float32>() 
        | Some v -> Nullable<float32>(v)
  
    member this.GetNullableGuid(name) = 
        match this.GetGuidOption(name) with 
        | None   -> Nullable<Guid>() 
        | Some v -> Nullable<Guid>(v)
  
    member this.GetNullableInt16(name) = 
        match this.GetInt16Option(name) with 
        | None   -> Nullable<Int16>() 
        | Some v -> Nullable<Int16>(v)
  
    member this.GetNullableInt32(name) = 
        match this.GetInt32Option(name) with 
        | None   -> Nullable<Int32>() 
        | Some v -> Nullable<Int32>(v)
  
    member this.GetNullableInt64(name) = 
        match this.GetInt64Option(name) with 
        | None   -> Nullable<Int64>() 
        | Some v -> Nullable<Int64>(v)
  