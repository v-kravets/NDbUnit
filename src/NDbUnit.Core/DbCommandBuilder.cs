/*
 *
 * NDbUnit
 * Copyright (C)2005 - 2011
 * http://code.google.com/p/ndbunit
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
using System.IO;
using System.Text;
using System.Data;
using System.Collections;
using System.Data.Common;

namespace NDbUnit.Core
{
    public abstract class DbCommandBuilder<TDbConnection> : IDbCommandBuilder where TDbConnection: DbConnection, new()
    {
        private DataSet _dataSet = new DataSet();

        protected DataTable _dataTableSchema;

        private Hashtable _dbCommandColl = new Hashtable();

        private bool _initialized;

        //private bool _passedconnection;

        //protected DbConnection _sqlConnection;

        protected DbConnectionManager<TDbConnection> ConnectionManager; 

        private string _xmlSchemaFile = "";

        protected DbCommandBuilder(DbConnectionManager<TDbConnection> connectionManager )
        {
            ConnectionManager = connectionManager;
        }

        //protected DbCommandBuilder(DbConnection connection)
        //{
        //    _passedconnection = true;
        //    _sqlConnection = connection;
        //}

        //protected DbCommandBuilder(string connectionString)
        //{
        //    _sqlConnection = GetConnection(connectionString);
        //}

        public int CommandTimeOutSeconds { get; set; }

        public DbConnection Connection
        {
            get { return ConnectionManager.GetConnection(); }
        }

        public virtual string QuotePrefix
        {
            get { return ""; }
        }

        public virtual string QuoteSuffix
        {
            get { return ""; }
        }

        public string XmlSchemaFile
        {
            get { return _xmlSchemaFile; }
        }

        public void BuildCommands(string xmlSchemaFile)
        {
            Stream stream = null;
            try
            {
                stream = new FileStream(xmlSchemaFile, FileMode.Open, FileAccess.Read);
                BuildCommands(stream);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
            _xmlSchemaFile = xmlSchemaFile;
            _initialized = true;
        }

        public void BuildCommands(Stream xmlSchema)
        {

            _dataSet.ReadXmlSchema(xmlSchema);
            // DataSet table rows RowState property is set to Added
            // when read in from an xml file.
            _dataSet.AcceptChanges();

            Hashtable ht = new Hashtable();

            foreach (DataTable dataTable in _dataSet.Tables)
            {
                // Virtual overrides.
                var commands = new Commands();
                commands.CreateSelectCommand = () => CreateSelectCommand(_dataSet, dataTable.TableName);
                commands.CreateInsertCommand = () =>
                {
                    using (var selectCommand = commands.CreateSelectCommand())
                    {
                        return CreateInsertCommand(selectCommand, dataTable.TableName);
                    }
                };
                commands.CreateInsertIdentityCommand = () =>
                {
                    using (var selectCommand = commands.CreateSelectCommand())
                    {
                        return CreateInsertIdentityCommand(selectCommand, dataTable.TableName);
                    }
                };
                commands.CreateDeleteCommand = () =>
                {
                    using (var selectCommand = commands.CreateSelectCommand())
                    {
                        return CreateDeleteCommand(selectCommand, dataTable.TableName);
                    }
                };
                commands.CreateDeleteAllCommand = () => CreateDeleteAllCommand(dataTable.TableName);
                commands.CreateUpdateCommand = () =>
                {
                    using (var selectCommand = commands.CreateSelectCommand())
                    {
                        return CreateUpdateCommand(selectCommand, dataTable.TableName);
                    }
                };

                ht[dataTable.TableName] = commands;
            }

            _dbCommandColl = ht;
            _initialized = true;
        }

        public DbCommand GetDeleteAllCommand(string tableName)
        {
            isInitialized();
            return ((Commands)_dbCommandColl[tableName]).CreateDeleteAllCommand();
        }

        public DbCommand GetDeleteCommand(string tableName)
        {
            isInitialized();
            return ((Commands)_dbCommandColl[tableName]).CreateDeleteCommand();
        }

        public DbCommand GetInsertCommand(string tableName)
        {
            isInitialized();
            return ((Commands)_dbCommandColl[tableName]).CreateInsertCommand();
        }

        public DbCommand GetInsertIdentityCommand(string tableName)
        {
            isInitialized();
            return ((Commands)_dbCommandColl[tableName]).CreateInsertIdentityCommand();
        }

        public void ReleaseConnection()
        {
            ConnectionManager.ReleaseConnection();
        }

        public DataSet GetSchema()
        {
            isInitialized();
            return _dataSet;
        }

        public DbCommand GetSelectCommand(string tableName)
        {
            isInitialized();
            return ((Commands)_dbCommandColl[tableName]).CreateSelectCommand();
        }

        public DbCommand GetUpdateCommand(string tableName)
        {
            isInitialized();
            return ((Commands)_dbCommandColl[tableName]).CreateUpdateCommand();
        }

        protected virtual bool ColumnOKToInclude(DataRow dataRow)
        {
            try
            {
                string columnName = (string)dataRow["ColumnName"];

                bool found = false;

                foreach (DataTable table in _dataSet.Tables)
                {
                    found = table.Columns.Contains(columnName);
                    if (found == true)
                        break;
                }

                var isTimeStamp = false;

                if (dataRow.Table.Columns.Contains("DataTypeName"))
                {
                    isTimeStamp = dataRow["DataTypeName"].ToString() == "timestamp";
                }

                return found && !(bool)dataRow["IsHidden"] && !isTimeStamp;
            }
            catch (Exception)
            {
                //if we cannot determine a reason NOT to include the column, we have to assume its OK to do so
                return true;
            }
        }

        protected abstract DbCommand CreateDbCommand();

        protected virtual DbCommand CreateDeleteAllCommand(string tableName)
        {
            DbCommand command = CreateDbCommand();
            command.CommandText = String.Format("DELETE FROM {0}", TableNameHelper.FormatTableName(tableName, QuotePrefix, QuoteSuffix));
            return command;
        }

        protected virtual DbCommand CreateDeleteCommand(DbCommand selectCommand, string tableName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(String.Format("DELETE FROM {0} WHERE ", TableNameHelper.FormatTableName(tableName, QuotePrefix, QuoteSuffix)));

            DbCommand sqlDeleteCommand = CreateDbCommand();

            int count = 1;
            foreach (DataRow dataRow in _dataTableSchema.Rows)
            {
                // A key column.
                if ((bool)dataRow["IsKey"])
                {
                    if (count != 1)
                    {
                        sb.Append(" AND ");
                    }

                    sb.Append(QuotePrefix + dataRow["ColumnName"] + QuoteSuffix);
                    sb.Append(String.Format("={0}", GetParameterDesignator(count)));

                    IDataParameter sqlParameter = CreateNewSqlParameter(count, dataRow);
                    sqlDeleteCommand.Parameters.Add(sqlParameter);

                    ++count;
                }
            }

            sqlDeleteCommand.CommandText = sb.ToString();

            return sqlDeleteCommand;
        }

        protected virtual DbCommand CreateInsertCommand(DbCommand selectCommand, string tableName)
        {
            int count = 1;
            bool notFirstColumn = false;
            StringBuilder sb = new StringBuilder();
            sb.Append(String.Format("INSERT INTO {0}(", TableNameHelper.FormatTableName(tableName, QuotePrefix, QuoteSuffix)));
            StringBuilder sbParam = new StringBuilder();
            IDataParameter sqlParameter;
            DbCommand sqlInsertCommand = CreateDbCommand();
            foreach (DataRow dataRow in _dataTableSchema.Rows)
            {
                if (ColumnOKToInclude(dataRow))
                {
                    // Not an identity column.
                    if (!((bool)dataRow[GetIdentityColumnDesignator()]))
                    {
                        if (notFirstColumn)
                        {
                            sb.Append(", ");
                            sbParam.Append(", ");
                        }

                        notFirstColumn = true;

                        sb.Append(QuotePrefix + dataRow["ColumnName"] + QuoteSuffix);
                        sbParam.Append(GetParameterDesignator(count));

                        sqlParameter = CreateNewSqlParameter(count, dataRow);
                        sqlInsertCommand.Parameters.Add(sqlParameter);

                        ++count;
                    }
                }
            }

            sb.Append(String.Format(") VALUES({0})", sbParam));

            sqlInsertCommand.CommandText = sb.ToString();

            return sqlInsertCommand;
        }

        protected virtual DbCommand CreateInsertIdentityCommand(DbCommand selectCommand, string tableName)
        {
            int count = 1;
            bool notFirstColumn = false;
            StringBuilder sb = new StringBuilder();
            sb.Append(String.Format("INSERT INTO {0}(", TableNameHelper.FormatTableName(tableName, QuotePrefix, QuoteSuffix)));
            StringBuilder sbParam = new StringBuilder();
            IDataParameter sqlParameter;
            DbCommand sqlInsertIdentityCommand = CreateDbCommand();
            foreach (DataRow dataRow in _dataTableSchema.Rows)
            {
                if (ColumnOKToInclude(dataRow))
                {

                    if (notFirstColumn)
                    {
                        sb.Append(", ");
                        sbParam.Append(", ");
                    }

                    notFirstColumn = true;

                    sb.Append(QuotePrefix + dataRow["ColumnName"] + QuoteSuffix);
                    sbParam.Append(GetParameterDesignator(count));

                    sqlParameter = CreateNewSqlParameter(count, dataRow);
                    sqlInsertIdentityCommand.Parameters.Add(sqlParameter);

                    ++count;
                }
            }

            sb.Append(String.Format(") VALUES({0})", sbParam));

            sqlInsertIdentityCommand.CommandText = sb.ToString();

            return sqlInsertIdentityCommand;
        }

        protected abstract IDataParameter CreateNewSqlParameter(int index, DataRow dataRow);

        protected virtual DbCommand CreateSelectCommand(DataSet ds, string tableName)
        {
            DbCommand sqlSelectCommand = CreateDbCommand();

            bool notFirstColumn = false;
            StringBuilder sb = new StringBuilder("SELECT ");
            DataTable dataTable = ds.Tables[tableName];
            foreach (DataColumn dataColumn in dataTable.Columns)
            {
                if (notFirstColumn)
                {
                    sb.Append(", ");
                }

                notFirstColumn = true;

                sb.Append(QuotePrefix + dataColumn.ColumnName + QuoteSuffix);
            }

            sb.Append(" FROM ");
            sb.Append(TableNameHelper.FormatTableName(tableName, QuotePrefix, QuoteSuffix));

            sqlSelectCommand.CommandText = sb.ToString();
            sqlSelectCommand.Connection = ConnectionManager.GetConnection();

            try
            {
                _dataTableSchema = GetSchemaTable(sqlSelectCommand);
            }
            catch (Exception e)
            {
                string message =
                    String.Format(
                        "DbCommandBuilder.CreateSelectCommand(DataSet, string) failed for tableName = '{0}'",
                        tableName);
                throw new NDbUnitException(message, e);
            }

            return sqlSelectCommand;
        }

        protected virtual DbCommand CreateUpdateCommand(DbCommand selectCommand, string tableName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(String.Format("UPDATE {0} SET ", TableNameHelper.FormatTableName(tableName, QuotePrefix, QuoteSuffix)));

            DbCommand sqlUpdateCommand = CreateDbCommand();

            int count = 1;
            bool notFirstKey = false;
            bool notFirstColumn = false;
            StringBuilder sbPrimaryKey = new StringBuilder();

            bool containsAllPrimaryKeys = true;
            foreach (DataRow dataRow in _dataTableSchema.Rows)
            {
                if (!(bool)dataRow["IsKey"])
                {
                    containsAllPrimaryKeys = false;
                    break;
                }
            }

            foreach (DataRow dataRow in _dataTableSchema.Rows)
            {
                if (ColumnOKToInclude(dataRow))
                {

                    // A key column.
                    IDataParameter sqlParameter;
                    if ((bool)dataRow["IsKey"])
                    {
                        if (notFirstKey)
                        {
                            sbPrimaryKey.Append(" AND ");
                        }

                        notFirstKey = true;

                        sbPrimaryKey.Append(QuotePrefix + dataRow["ColumnName"] + QuoteSuffix);
                        sbPrimaryKey.Append(String.Format("={0}", GetParameterDesignator(count)));

                        sqlParameter = CreateNewSqlParameter(count, dataRow);
                        sqlUpdateCommand.Parameters.Add(sqlParameter);

                        ++count;
                    }

                    if (containsAllPrimaryKeys || !(bool)dataRow["IsKey"])
                    {
                        if (notFirstColumn)
                        {
                            sb.Append(", ");
                        }

                        notFirstColumn = true;

                        sb.Append(QuotePrefix + dataRow["ColumnName"] + QuoteSuffix);
                        sb.Append(String.Format("={0}", GetParameterDesignator(count)));

                        sqlParameter = CreateNewSqlParameter(count, dataRow);
                        sqlUpdateCommand.Parameters.Add(sqlParameter);

                        ++count;
                    }
                }
            }

            sb.Append(String.Format(" WHERE {0}", sbPrimaryKey));

            sqlUpdateCommand.CommandText = sb.ToString();

            return sqlUpdateCommand;
        }

        protected abstract DbConnection GetConnection(string connectionString);

        protected virtual string GetIdentityColumnDesignator()
        {
            return "IsIdentity";
        }

        protected virtual string GetParameterDesignator(int count)
        {
            return String.Format("@p{0}", count);
        }

        //private DataTable GetSchemaTable(DbCommand sqlSelectCommand)
        protected virtual DataTable GetSchemaTable(DbCommand sqlSelectCommand)
        {
            DataTable dataTableSchema = new DataTable();

            var connection = ConnectionManager.GetConnection();

            var connectionWasClosed = ConnectionState.Closed == connection.State;
            try
            {
                if (connectionWasClosed)
                {
                    connection.Open();
                }

                IDataReader sqlDataReader = sqlSelectCommand.ExecuteReader(CommandBehavior.KeyInfo);
                dataTableSchema = sqlDataReader.GetSchemaTable();
                sqlSelectCommand.Cancel();
                sqlDataReader.Close();
            }
            catch (NotSupportedException)
            {
                //swallow this since .Close() op isn't supported on all DB targets (e.g., SQLCE)
            }
            finally
            {
                //Only close connection if connection was not passed to constructor
                if (!ConnectionManager.HasExternallyManagedConnection)
                {
                    if (connection.State != ConnectionState.Closed && connectionWasClosed)
                    {
                        connection.Close();
                    }
                }
            }

            return dataTableSchema;
        }

        private void isInitialized()
        {
            if (!_initialized)
            {
                string message =
                    "IDbCommandBuilder.BuildCommands(string) or IDbCommandBuilder.BuildCommands(Stream) must be called successfully";
                throw new NDbUnitException(message);
            }
        }

        private class Commands
        {
            public Func<DbCommand> CreateSelectCommand;
            public Func<DbCommand> CreateInsertCommand;
            public Func<DbCommand> CreateInsertIdentityCommand;
            public Func<DbCommand> CreateDeleteCommand;
            public Func<DbCommand> CreateDeleteAllCommand;
            public Func<DbCommand> CreateUpdateCommand;
        }

    }
}
