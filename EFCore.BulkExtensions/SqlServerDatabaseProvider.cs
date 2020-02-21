#region Copyright

// Quantum AI CONFIDENTIAL INFORMATION
// © 2020 Quantum AI, Inc.
// All Rights Reserved
// 
// This program contains confidential and proprietary information
// of the Quantum AI, Inc.  Any reproduction, disclosure, or use
// in whole or in part is expressly prohibited, except as may be
// specifically authorized by prior written agreement.

#endregion

using System;
using System.Data.SqlClient;
using System.Text;

namespace EFCore.BulkExtensions
{
    public class SqlServerDatabaseProvider : DatabaseProvider
    {
        public override Type GetParameterType()
        {
            return typeof(SqlParameter);
        }

        public override char GetStartObjectEscaper()
        {
            return '[';
        }

        public override char GetEndObjectEscaper()
        {
            return ']';
        }

        public override string EscapeObject(string objectName)
        {
            return $"[{objectName}]";
        }

        public override string GetUpdateQuery(string fromSql, StringBuilder sqlColumns, string whereSql,
            string tableAlias)
        {
            return $"UPDATE {tableAlias} SET {sqlColumns} FROM {fromSql} {whereSql}";
        }

        public override string GetDeleteQuery(string fromSql, string whereSql, string tableAlias)
        {
            return $"DELETE {tableAlias} FROM {fromSql}{whereSql}";
        }
    }
}