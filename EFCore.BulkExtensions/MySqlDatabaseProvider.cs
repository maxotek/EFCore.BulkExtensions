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
using MySql.Data.MySqlClient;

namespace EFCore.BulkExtensions
{
    public class MySqlDatabaseProvider : DatabaseProvider
    {
        public override Type GetParameterType()
        {
            return typeof(MySqlParameter);
        }

        public override char GetStartObjectEscaper()
        {
            return '`';
        }

        public override char GetEndObjectEscaper()
        {
            return '`';
        }

        public override string EscapeObject(string objectName)
        {
            return $"`{objectName}`";
        }
    }
}