using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace TestFromCSharp
{

    public static class Giulio
    {
        public class DbParam
        {
            public string Name;
            public object Value;
            public DbParam(string name, object value)
            {
                Name = name;
                Value = value;
            }
        }

        public static IEnumerable<T> queryWrapper<T>
            (
            string sql,
            DbParam[] dbParams,
            Func<IDataReader, T> map,
            IDbTransaction tran,
            out bool isOk
            )
        {
            try
            {
                Debug.WriteLine("before query");
                var query = queryTran<T>(sql, dbParams, map, tran);
                Debug.WriteLine("after query");
                isOk = true;
                return query;
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc.Message);
                isOk = false;
                return Enumerable.Empty<T>();
            }
        }
        public static IEnumerable<T> query<T>
            (
            string sql,
            DbParam[] dbParams,
            Func<IDataReader, T> map,
            IDbConnection conn
            )
        {
            using (var tran = conn.BeginTransaction())
            {
                var query = queryWrapper<T>(sql, dbParams, map, tran, out bool isOk)
                    // enumeration is need also in C# when a transactional wrapper is added
                    // as per https://github.com/dotnet/fsharp/issues/8897#issuecomment-610361459
                    .ToArray();
                if (isOk)
                {
                    tran.Commit();

                } else
                {
                    tran.Rollback();
                }

                return query;
            }
        }
        public static IEnumerable<T> queryTran<T>
            (
            string sql,
            DbParam[] dbParams,
            Func<IDataReader, T> map,
            IDbTransaction tran
            )
        {
                using (var cmd = tran.Connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    foreach (var dbpar in dbParams.Select(p =>
                        {
                            var dbpar = cmd.CreateParameter();
                            dbpar.ParameterName = p.Name;
                            dbpar.Value = p.Value;
                            return dbpar;
                        }
                        ))
                    {
                        cmd.Parameters.Add(dbpar);
                    }
                    using (var DR = cmd.ExecuteReader())
                    {
                        while (DR.Read())
                        {
                            yield return map(DR);
                        }
                    }
                }
            
        }
    }
}
