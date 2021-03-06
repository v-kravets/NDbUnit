/*
 *
 * NDbUnit
 * Copyright (C) 2005 - 2015
 * https://github.com/fubar-coder/NDbUnit
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 */

using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Data;

namespace NDbUnit.Core.SqlLite
{
    public class SqlLiteDbUnitTest : NDbUnitTest<SQLiteConnection>
    {
        public SqlLiteDbUnitTest(string connectionString)
            : base(connectionString)
        {
        }

        public SqlLiteDbUnitTest(SQLiteConnection connection)
            : base(connection)
        {
        }

        protected override DbDataAdapter CreateDataAdapter(DbCommand command)
        {
            return new SQLiteDataAdapter((SQLiteCommand)command);
        }

        protected override IDbCommandBuilder CreateDbCommandBuilder(DbConnectionManager<SQLiteConnection> connectionManager)
        {
            return new SqlLiteDbCommandBuilder(connectionManager);
        }
        
        //protected override IDbCommandBuilder CreateDbCommandBuilder(DbConnection connection)
        //{
        //    return new SqlLiteDbCommandBuilder(connection);
        //}

        //protected override IDbCommandBuilder CreateDbCommandBuilder(string connectionString)
        //{
        //    return new SqlLiteDbCommandBuilder(connectionString);
        //}

        protected override IDbOperation CreateDbOperation()
        {
            return new SqlLiteDbOperation();
        }

    }

    [Obsolete("Use SqlLiteDbUnitTest class in place of this.")]
    public class SqlLiteUnitTest : SqlLiteDbUnitTest
    {
        public SqlLiteUnitTest(string connectionString) : base(connectionString)
        {
        }

        public SqlLiteUnitTest(SQLiteConnection connection) : base(connection)
        {
        }
    }
}
