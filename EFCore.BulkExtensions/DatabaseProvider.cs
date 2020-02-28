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
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace EFCore.BulkExtensions
{
    public abstract class DatabaseProvider
    {
        public abstract Type GetParameterType();

        public abstract char GetStartObjectEscaper();
        public abstract char GetEndObjectEscaper();

        public abstract string EscapeObject(string objectName);

        public static DatabaseProvider Get(DbContext context)
        {
            var providerName = context.Database.ProviderName;
            switch (providerName)
            {
                case "Microsoft.EntityFrameworkCore.SqlServer":
                    return new SqlServerDatabaseProvider();

                case "Pomelo.EntityFrameworkCore.MySql":
                    return new MySqlDatabaseProvider();

                case "FirebirdSql.EntityFrameworkCore.Firebird":
                    return new FbDatabaseProvider();

                default:
                    throw new Exception($"Unsupported database provider: {providerName}");
            }
        }

        public abstract string GetUpdateQuery(string fromSql, StringBuilder sqlColumns, string whereSql,
            string tableAlias);

        public abstract string GetDeleteQuery(string fromSql, string whereSql, string tableAlias);
    }
}