using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;

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
                    tran.Commit();
                }
            }
        }
    }
}
