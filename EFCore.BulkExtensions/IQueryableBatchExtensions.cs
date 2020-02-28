using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace EFCore.BulkExtensions
{
    public static class IQueryableBatchExtensions
    {
        public static int BatchDelete<T>(this IQueryable<T> query) where T : class, new()
        {
            DbContext context = BatchUtil.GetDbContext(query);
            var provider = DatabaseProvider.Get(context);
            string sql = BatchUtil.GetSqlDelete(query, provider);
            return context.Database.ExecuteSqlCommand(sql);
        }

        public static int BatchUpdate<T>(this IQueryable<T> query, T updateValues,  List<string> updateColumns = null) where T : class, new()
        {
            DbContext context = BatchUtil.GetDbContext(query);
            var provider = DatabaseProvider.Get(context);

            var (sql, sqlParameters) = BatchUtil.GetSqlUpdate(query, context, updateValues, updateColumns, provider);
            return context.Database.ExecuteSqlCommand(sql, sqlParameters.ToArray());
        }


        public static int BatchUpdate<T>(this IQueryable<T> query, Expression<Func<T, T>> updateExpression) where T : class, new()
        {
            var context = BatchUtil.GetDbContext(query);
            var provider = DatabaseProvider.Get(context);

            var (sql, sqlParameters) = BatchUtil.GetSqlUpdate(query, updateExpression, provider);
            return  context.Database.ExecuteSqlCommand(sql, sqlParameters);
        }

        // Async methods

        public static async Task<int> BatchDeleteAsync<T>(this IQueryable<T> query) where T : class, new()
        {
            DbContext context = BatchUtil.GetDbContext(query);
            var provider = DatabaseProvider.Get(context);

            string sql = BatchUtil.GetSqlDelete(query, provider);
            return await context.Database.ExecuteSqlCommandAsync(sql);
        }

        public static async Task<int> BatchUpdateAsync<T>(this IQueryable<T> query, T updateValues, List<string> updateColumns = null) where T : class, new()
        {
            DbContext context = BatchUtil.GetDbContext(query);
            var provider = DatabaseProvider.Get(context);

            var (sql, sqlParameters) = BatchUtil.GetSqlUpdate(query, context, updateValues, updateColumns, provider);
            return await context.Database.ExecuteSqlCommandAsync(sql, sqlParameters.ToArray());
        }

        public static async Task<int> BatchUpdateAsync<T>(this IQueryable<T> query, Expression<Func<T, T>> updateExpression) where T : class, new()
        {
            var context = BatchUtil.GetDbContext(query);
            var provider = DatabaseProvider.Get(context);

            var (sql, sqlParameters) = BatchUtil.GetSqlUpdate(query, updateExpression, provider);
            Console.WriteLine(sql);
            return await context.Database.ExecuteSqlCommandAsync(sql, sqlParameters);
        }

    }
}
