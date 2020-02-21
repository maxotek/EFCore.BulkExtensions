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
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

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
            switch (context.Database.ProviderName)
            {
                case "Pomelo.EntityFrameworkCore.MySql":
                    return new MySqlDatabaseProvider();

                default:
                    return new SqlServerDatabaseProvider();
            }
        }
    }
}