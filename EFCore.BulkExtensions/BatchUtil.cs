using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace EFCore.BulkExtensions
{
    static class BatchUtil
    {
        // In comment are Examples of how SqlQuery is changed for Sql Batch

        // SELECT [a].[Column1], [a].[Column2], .../r/n
        // FROM [Table] AS [a]/r/n
        // WHERE [a].[Column] = FilterValue
        // --
        // DELETE [a]
        // FROM [Table] AS [a]
        // WHERE [a].[Columns] = FilterValues
        public static string GetSqlDelete<T>(IQueryable<T> query, DatabaseProvider provider) where T : class, new()
        {
            (string fromSql, string whereSql, string tableAlias) = GetBatchSql(query);
            return $"DELETE FROM {fromSql}{whereSql}";
        }

        // SELECT [a].[Column1], [a].[Column2], .../r/n
        // FROM [Table] AS [a]/r/n
        // WHERE [a].[Column] = FilterValue
        // --
        // UPDATE [a] SET [UpdateColumns] = N'updateValues'
        // FROM [Table] AS [a]
        // WHERE [a].[Columns] = FilterValues
        public static (string, List<SqlParameter>) GetSqlUpdate<T>(IQueryable<T> query, DbContext context, T updateValues, List<string> updateColumns) where T : class, new()
        {
            (string fromSql, string whereSql, string tableAlias) = GetBatchSql(query);
            var sqlParameters = new List<SqlParameter>();
            string sqlSET = GetSqlSetSegment(context, updateValues, updateColumns, sqlParameters);
            return ($"UPDATE {fromSql} {sqlSET}{whereSql}", sqlParameters);
        }

        /// <summary>
        /// get Update Sql
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static (string, List<DbParameter>) GetSqlUpdate<T>(IQueryable<T> query, Expression<Func<T, T>> expression, DatabaseProvider provider) where T : class, new()
        {
            (string fromSql, string  whereSql, string tableAlias) = GetBatchSql(query);
            var sqlColumns = new StringBuilder();
            var sqlParameters = new List<DbParameter>();
            var columnNameValueDict = TableInfo.CreateInstance(GetDbContext(query), new List<T>(), OperationType.Read, new BulkConfig()).PropertyColumnNamesDict;
            CreateUpdateBody(columnNameValueDict, tableAlias, expression.Body, ref sqlColumns, ref sqlParameters, provider);

            return ($"UPDATE {fromSql} SET {sqlColumns.ToString()} {whereSql}", sqlParameters);
        }

        public static (string, string, string) GetBatchSql<T>(IQueryable<T> query) where T : class, new()
        {
            string sqlQuery = query.ToSql();
            string tableAlias = GetTableAlias<T>(sqlQuery);
            
            int indexFROM = sqlQuery.IndexOf(Environment.NewLine);
            var indexWHERE = sqlQuery.IndexOf(Environment.NewLine, indexFROM + 1);

            string fromSql = sqlQuery.Substring(indexFROM + 7, indexWHERE - indexFROM - 7);
            string whereSql = sqlQuery.Substring(indexWHERE, sqlQuery.Length - indexWHERE);

            return (EscapeBraces(fromSql), EscapeBraces(whereSql), tableAlias);
        }

        private static string EscapeBraces(string sql)
        {
            sql = sql.Contains("{") ? sql.Replace("{", "{{") : sql; // Curly brackets have to escaped:
            sql = sql.Contains("}") ? sql.Replace("}", "}}") : sql; // https://github.com/aspnet/EntityFrameworkCore/issues/8820
            return sql;
        }

        private static string GetTableAlias<T>(string sqlQuery) where T : class, new()
        {
            var escaper = sqlQuery[7] == '`' ? '`' : ']';
            return sqlQuery.Substring(8, sqlQuery.IndexOf(escaper, 8) - 8);
        }

        public static string GetSqlSetSegment<T>(DbContext context, T updateValues, List<string> updateColumns, List<SqlParameter> parameters) where T : class, new()
        {
            var tableInfo = TableInfo.CreateInstance<T>(context, new List<T>(), OperationType.Read, new BulkConfig());
            string sql = string.Empty;
            Type updateValuesType = typeof(T);
            var defaultValues = new T();
            foreach (var propertyNameColumnName in tableInfo.PropertyColumnNamesDict)
            {
                string propertyName = propertyNameColumnName.Key;
                string columnName = propertyNameColumnName.Value;
                var pArray = propertyName.Split(new char[] { '.' });
                Type lastType = updateValuesType;
                PropertyInfo property = lastType.GetProperty(pArray[0]);
                object propertyUpdateValue = property.GetValue(updateValues);
                object propertyDefaultValue = property.GetValue(defaultValues);
                for (int i = 1; i < pArray.Length; i++)
                {
                    lastType = property.PropertyType;
                    property = lastType.GetProperty(pArray[i]);
                    propertyUpdateValue = propertyUpdateValue != null ? property.GetValue(propertyUpdateValue) : propertyUpdateValue;
                    var lastDefaultValues = lastType.Assembly.CreateInstance(lastType.FullName);
                    propertyDefaultValue = property.GetValue(lastDefaultValues);
                }

                bool isDifferentFromDefault = propertyUpdateValue != null && propertyUpdateValue?.ToString() != propertyDefaultValue?.ToString();
                if (isDifferentFromDefault || (updateColumns != null && updateColumns.Contains(propertyName)))
                {
                    sql += $"[{columnName}] = @{columnName}, ";
                    propertyUpdateValue = propertyUpdateValue ?? DBNull.Value;
                    parameters.Add(new SqlParameter($"@{columnName}", propertyUpdateValue));
                }
            }
            if (String.IsNullOrEmpty(sql))
            {
                throw new InvalidOperationException("SET Columns not defined. If one or more columns should be updated to theirs default value use 'updateColumns' argument.");
            }
            sql = sql.Remove(sql.Length - 2, 2); // removes last excess comma and space: ", "
            return $"SET {sql}";
        }

        /// <summary>
        /// Recursive analytic expression 
        /// </summary>
        /// <param name="tableAlias"></param>
        /// <param name="expression"></param>
        /// <param name="sqlColumns"></param>
        /// <param name="sqlParameters"></param>
        /// <summary>
        /// Recursive analytic expression 
        /// </summary>
        /// <param name="tableAlias"></param>
        /// <param name="expression"></param>
        /// <param name="sqlColumns"></param>
        /// <param name="sqlParameters"></param>
        public static void CreateUpdateBody(Dictionary<string, string> columnNameValueDict, string tableAlias, Expression expression, ref StringBuilder sqlColumns, ref List<DbParameter> sqlParameters, DatabaseProvider provider)
        {
            if (expression is MemberInitExpression memberInitExpression)
            {
                foreach (var item in memberInitExpression.Bindings)
                {
                    if (item is MemberAssignment assignment)
                    {
                        if (columnNameValueDict.TryGetValue(assignment.Member.Name, out string value))
                            sqlColumns.Append($" {provider.EscapeObject(tableAlias)}.{provider.EscapeObject(value)}");
                        else
                            sqlColumns.Append($" {provider.EscapeObject(tableAlias)}.{provider.EscapeObject(assignment.Member.Name)}");

                        sqlColumns.Append(" =");

                        CreateUpdateBody(columnNameValueDict, tableAlias, assignment.Expression, ref sqlColumns, ref sqlParameters, provider);

                        if (memberInitExpression.Bindings.IndexOf(item) < (memberInitExpression.Bindings.Count - 1))
                            sqlColumns.Append(" ,");
                    }
                }
            }
            else if (expression is MemberExpression memberExpression && memberExpression.Expression is ParameterExpression)
            {
                if (columnNameValueDict.TryGetValue(memberExpression.Member.Name, out string value))
                    sqlColumns.Append($" {provider.EscapeObject(tableAlias)}.{provider.EscapeObject(value)}");
                else
                    sqlColumns.Append($" {provider.EscapeObject(tableAlias)}.{provider.EscapeObject(memberExpression.Member.Name)}");
            }
            else if (expression is ConstantExpression constantExpression)
            {
                var parmName = $"param_{sqlParameters.Count}";

                sqlParameters.Add(CreateParameter(provider, parmName, constantExpression.Value ?? DBNull.Value));
                sqlColumns.Append($" @{parmName}");
            }
            else if (expression is UnaryExpression unaryExpression)
            {
                switch (unaryExpression.NodeType)
                {
                    case ExpressionType.Convert:
                        CreateUpdateBody(columnNameValueDict, tableAlias, unaryExpression.Operand, ref sqlColumns, ref sqlParameters, provider);
                        break;
                    case ExpressionType.Not:
                        sqlColumns.Append(" ~");//this way only for SQL Server 
                        CreateUpdateBody(columnNameValueDict, tableAlias, unaryExpression.Operand, ref sqlColumns, ref sqlParameters, provider);
                        break;
                    default: break;
                }
            }
            else if (expression is BinaryExpression binaryExpression)
            {
                CreateUpdateBody(columnNameValueDict, tableAlias, binaryExpression.Left, ref sqlColumns, ref sqlParameters, provider);

                switch (binaryExpression.NodeType)
                {
                    case ExpressionType.Add:
                        sqlColumns.Append(" +");
                        break;
                    case ExpressionType.Divide:
                        sqlColumns.Append(" /");
                        break;
                    case ExpressionType.Multiply:
                        sqlColumns.Append(" *");
                        break;
                    case ExpressionType.Subtract:
                        sqlColumns.Append(" -");
                        break;
                    default: break;
                }

                CreateUpdateBody(columnNameValueDict, tableAlias, binaryExpression.Right, ref sqlColumns, ref sqlParameters, provider);
            }
            else
            {
                var value = Expression.Lambda(expression).Compile().DynamicInvoke();
                var parmName = $"param_{sqlParameters.Count}";
                sqlParameters.Add(CreateParameter(provider, parmName, value ?? DBNull.Value));
                sqlColumns.Append($" @{parmName}");
            }
        }

        private static DbParameter CreateParameter(DatabaseProvider provider, string parmName, object value)
        {
            var parameter = (DbParameter) Activator.CreateInstance(provider.GetParameterType());
            parameter.ParameterName = parmName;
            parameter.Value = value;
            return parameter;
        }


        public static DbContext GetDbContext(IQueryable query)
        {
            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            var queryCompiler = typeof(EntityQueryProvider).GetField("_queryCompiler", bindingFlags).GetValue(query.Provider);
            var queryContextFactory = queryCompiler.GetType().GetField("_queryContextFactory", bindingFlags).GetValue(queryCompiler);

            var dependencies = typeof(RelationalQueryContextFactory).GetProperty("Dependencies", bindingFlags).GetValue(queryContextFactory);
            var queryContextDependencies = typeof(DbContext).Assembly.GetType(typeof(QueryContextDependencies).FullName);
            var stateManagerProperty = queryContextDependencies.GetProperty("StateManager", bindingFlags | BindingFlags.Public).GetValue(dependencies);
            var stateManager = (IStateManager)stateManagerProperty;

            return stateManager.Context;
        }
    }
}
