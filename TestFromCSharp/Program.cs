using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SQLite;

namespace TestFromCSharp
{
    class Program
    {
        public class MyAuthor
        {
            public int AuthorId;
            public string FullName;
            public static MyAuthor FromReader(IDataReader rd)
            {
                return new MyAuthor()
                {
                    AuthorId = rd.GetInt32(rd.GetOrdinal("author_id")),
                    FullName = rd.GetString(rd.GetOrdinal("full_name"))
                };
            }
    }
        static void Main(string[] args)
        {
            var realConnStr = @"Data Source=E:\giulio-vs-so\Donald\my_test_db.db;Version=3;";
            using (IDbConnection conn = new SQLiteConnection(realConnStr))
            {
                var authors =
                    Donald.query<MyAuthor>(
                            @"SELECT author_id, full_name
                         FROM   author
                         WHERE  author_id IN(@one, @two); ",
                            new Donald.DbParam[2] {
                                new Donald.DbParam("@one",1),
                                new Donald.DbParam("@two",2)
                            },
                        MyAuthor.FromReader, conn);
                foreach (MyAuthor author in authors)
                {
                    Console.WriteLine($" {author.AuthorId} {author.FullName} " );
                }
                Console.ReadLine();
            }

        }
    }
}
